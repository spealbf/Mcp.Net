using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core;
using Mcp.Net.Core.Attributes;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Core.Models.Messages;
using Mcp.Net.Core.Models.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Extensions;

public static class McpServerExtensions
{
    /// <summary>
    /// Registers all tools defined in an assembly with the MCP server
    /// </summary>
    /// <param name="server">The MCP server to register tools with</param>
    /// <param name="assembly">The assembly containing tool classes</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies</param>
    public static void RegisterToolsFromAssembly(
        this McpServer server,
        Assembly assembly,
        IServiceProvider serviceProvider
    )
    {
        // Get logger from service provider if available, otherwise create a temporary one
        ILogger logger;
        if (serviceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger(nameof(McpServerExtensions));
        }
        else
        {
            var tempLoggerFactory = new LoggerFactory();
            logger = tempLoggerFactory.CreateLogger(nameof(McpServerExtensions));
        }
        
        logger.LogInformation("Scanning assembly for tools: {AssemblyName}", assembly.FullName);
        
        var toolTypes = assembly
            .GetTypes()
            .Where(t =>
                t.GetCustomAttributes<McpToolAttribute>().Any()
                || t.GetMethods().Any(m => m.GetCustomAttribute<McpToolAttribute>() != null)
            );

        var foundTypes = toolTypes.ToList();
        logger.LogInformation("Found {Count} tool classes in assembly {AssemblyName}", 
            foundTypes.Count, assembly.GetName().Name);
        
        foreach (var toolType in foundTypes)
        {
            logger.LogInformation("Registering tool class: {ToolTypeName}", toolType.FullName);
            RegisterClassTools(server, toolType, serviceProvider, logger);
        }
    }

    private static void RegisterClassTools(
        McpServer server,
        Type toolType,
        IServiceProvider serviceProvider,
        ILogger logger
    )
    {
        var methods = toolType
            .GetMethods()
            .Where(m => m.GetCustomAttribute<McpToolAttribute>() != null)
            .ToList();

        foreach (var method in methods)
        {
            var methodTool = method.GetCustomAttribute<McpToolAttribute>();
            if (methodTool == null)
            {
                continue; // Skip if null, shouldn't happen due to the Where filter
            }
            
            logger.LogInformation("Registering tool method: {MethodName} -> {ToolName}", method.Name, methodTool.Name);

            // Generate input schema for multiple parameters
            var inputSchema = GenerateInputSchema(method);

            server.RegisterTool(
                name: methodTool.Name,
                description: methodTool.Description,
                inputSchema: inputSchema,
                handler: async (arguments) =>
                {
                    try
                    {
                        var instance = ActivatorUtilities.CreateInstance(serviceProvider, toolType);

                        // Prepare parameters
                        var methodParams = method.GetParameters();
                        var invokeParams = new object?[methodParams.Length];

                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            var param = methodParams[i];
                            var paramName = param.Name?.ToLowerInvariant() ?? "";
                            if (
                                arguments.HasValue
                                && !string.IsNullOrEmpty(paramName)
                                && arguments.Value.TryGetProperty(paramName, out var paramValue)
                            )
                            {
                                try
                                {
                                    invokeParams[i] = JsonSerializer.Deserialize(
                                        paramValue.GetRawText(),
                                        param.ParameterType
                                    );
                                }
                                catch (JsonException ex)
                                {
                                    throw new McpException(
                                        ErrorCode.InvalidParams,
                                        $"Invalid value for parameter '{paramName}': {ex.Message}"
                                    );
                                }
                            }
                            else if (param.HasDefaultValue)
                            {
                                invokeParams[i] = param.DefaultValue;
                            }
                            else if (
                                param.GetCustomAttribute<McpParameterAttribute>()?.Required == true
                            )
                            {
                                throw new McpException(
                                    ErrorCode.InvalidParams,
                                    $"Required parameter '{paramName}' was not provided"
                                );
                            }
                            else
                            {
                                invokeParams[i] = param.ParameterType.IsValueType
                                    ? Activator.CreateInstance(param.ParameterType)
                                    : null;
                            }
                        }

                        // Invoke method
                        object? result;
                        if (method.ReturnType.IsAssignableTo(typeof(Task)))
                        {
                            var task = method.Invoke(instance, invokeParams) as Task;
                            if (task != null)
                            {
                                await task;
                                // Only try to get result if it's a Task<T> (not just Task)
                                var resultProperty = task.GetType().GetProperty("Result");
                                result = resultProperty?.GetValue(task);
                            }
                            else
                            {
                                result = null;
                            }
                        }
                        else
                        {
                            result = method.Invoke(instance, invokeParams);
                        }

                        // If the method already returns a CallToolResult, return it directly
                        if (result is ToolCallResult callToolResult)
                        {
                            return callToolResult;
                        }

                        // Otherwise, wrap the result in a CallToolResult
                        string resultText;
                        if (result != null)
                        {
                            Type resultType = result.GetType();
                            // Serialize to JSON for non-primitive types (complex objects)
                            if (
                                !resultType.IsPrimitive
                                && resultType != typeof(string)
                                && resultType != typeof(decimal)
                                && !resultType.IsEnum
                            )
                            {
                                // Properly serialize complex objects to JSON
                                resultText = JsonSerializer.Serialize(
                                    result,
                                    new JsonSerializerOptions
                                    {
                                        WriteIndented = true,
                                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    }
                                );
                            }
                            else
                            {
                                // Use ToString for primitive types
                                resultText = result.ToString() ?? string.Empty;
                            }
                        }
                        else
                        {
                            resultText = string.Empty;
                        }

                        return new ToolCallResult
                        {
                            Content = new[] { new TextContent { Text = resultText } },
                            IsError = false,
                        };
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException != null)
                    {
                        // Unwrap the TargetInvocationException to get the actual exception
                        var innerException = ex.InnerException;
                        if (innerException is McpException mcpEx)
                        {
                            throw mcpEx; // Let the McpServer handle this specially
                        }

                        // Return a formatted error response for other exceptions
                        return new ToolCallResult
                        {
                            IsError = true,
                            Content = new[]
                            {
                                new TextContent
                                {
                                    Text = $"Error in tool execution: {innerException.Message}",
                                },
                                new TextContent
                                {
                                    Text = $"Stack trace:\n{innerException.StackTrace}",
                                },
                            },
                        };
                    }
                    catch (Exception ex)
                    {
                        // Handle all other exceptions
                        if (ex is McpException mcpEx)
                        {
                            throw; // Let the McpServer handle this specially
                        }

                        return new ToolCallResult
                        {
                            IsError = true,
                            Content = new[]
                            {
                                new TextContent { Text = $"Error in tool execution: {ex.Message}" },
                                new TextContent { Text = $"Stack trace:\n{ex.StackTrace}" },
                            },
                        };
                    }
                }
            );
        }
    }

    public static JsonElement GenerateInputSchema(MethodInfo method)
    {
        var properties = new Dictionary<string, JsonElement>();
        var requiredProperties = new List<string>();

        foreach (var param in method.GetParameters())
        {
            var paramName = param.Name?.ToLowerInvariant() ?? $"param{param.Position}";
            var paramSchema = JsonSchemaGenerator.GenerateParameterSchema(param);
            properties[paramName] = paramSchema;

            var paramAttr = param.GetCustomAttribute<McpParameterAttribute>();
            if (paramAttr?.Required == true)
            {
                requiredProperties.Add(paramName);
            }
        }

        // Convert JsonElement dictionary to object dictionary to match the SchemaObject property type
        var objectProperties = properties.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);

        var schema = new JsonSchemaGenerator.SchemaObject
        {
            Properties = objectProperties,
            Required = requiredProperties,
        };

        return JsonSerializer.SerializeToElement(schema);
    }
}
