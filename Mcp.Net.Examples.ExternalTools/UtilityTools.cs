using System.Text;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.ExternalTools;

[McpTool("Utilities", "Common utility functions")]
public class UtilityTools
{
    [McpTool("string_reverse", "Reverse a string")]
    public string ReverseString(
        [McpParameter(required: true, description: "The string to reverse")] string input
    )
    {
        return new string(input.Reverse().ToArray());
    }

    [McpTool("string_base64Encode", "Encode a string to Base64")]
    public string Base64Encode(
        [McpParameter(required: true, description: "The string to encode")] string input
    )
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }

    [McpTool("string_base64Decode", "Decode a Base64 string")]
    public string Base64Decode(
        [McpParameter(required: true, description: "The Base64 string to decode")] string input
    )
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return "Error: Invalid Base64 string";
        }
    }
}
