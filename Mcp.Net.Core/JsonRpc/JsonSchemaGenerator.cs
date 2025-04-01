using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Core.JsonRpc;

public static class JsonSchemaGenerator
{
    public static JsonElement GenerateJsonSchema(Type type)
    {
        var properties = new Dictionary<string, object>();
        var requiredProperties = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var paramAttr = prop.GetCustomAttribute<McpParameterAttribute>();

            properties[prop.Name.ToLowerInvariant()] = GetPropertySchema(
                prop.PropertyType,
                paramAttr?.Description ?? ""
            );

            if (paramAttr?.Required == true)
            {
                requiredProperties.Add(prop.Name.ToLowerInvariant());
            }
        }

        var schema = new SchemaObject
        {
            Schema = "https://json-schema.org/draft/2020-12/schema",
            Type = "object",
            Properties = properties,
            Required = requiredProperties, // Always use the array, even if empty
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static object GetPropertySchema(Type type, string description)
    {
        var jsonType = GetJsonType(type);

        // Basic schema for primitive types
        var schema = new Dictionary<string, object>
        {
            { "type", jsonType },
            { "description", description },
        };

        // For arrays, add items schema
        if (jsonType == "array" && typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            var itemType = type.GetGenericArguments()[0];
            schema["items"] = GetPropertySchema(itemType, "");
        }

        return schema;
    }

    public static JsonElement GenerateParameterSchema(ParameterInfo parameter)
    {
        var paramAttr = parameter.GetCustomAttribute<McpParameterAttribute>();
        return JsonSerializer.SerializeToElement(
            GetPropertySchema(parameter.ParameterType, paramAttr?.Description ?? "")
        );
    }

    private static string GetJsonType(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Int32
            or TypeCode.Int64
            or TypeCode.Decimal
            or TypeCode.Double
            or TypeCode.Single => "number",
            TypeCode.String => "string",
            TypeCode.Boolean => "boolean",
            _ => type.IsArray || typeof(IEnumerable).IsAssignableFrom(type) ? "array" : "object",
        };
    }

    private class SchemaObject
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } =
            new Dictionary<string, object>();

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }
    }
}
