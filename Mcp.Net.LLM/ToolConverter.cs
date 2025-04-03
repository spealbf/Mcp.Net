using Mcp.Net.Core.Models.Tools;
using OpenAI.Chat;

namespace Mcp.Net.LLM;

public static class ToolConverter
{
    public static ChatTool ConvertToChatTool(Tool mcpTool)
    {
        string schemaString = mcpTool.InputSchema.ToString();

        return ChatTool.CreateFunctionTool(
            functionName: mcpTool.Name,
            functionDescription: mcpTool.Description ?? string.Empty,
            functionParameters: BinaryData.FromString(schemaString)
        );
    }

}
