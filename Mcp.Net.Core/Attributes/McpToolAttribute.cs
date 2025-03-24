namespace Mcp.Net.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class McpToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public Type? InputSchemaType { get; set; }

    public McpToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
