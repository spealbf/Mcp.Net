using Mcp.Net.LLM;
using Microsoft.AspNetCore.Mvc;

namespace Mcp.Net.Examples.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private readonly ILogger<ToolsController> _logger;
    private readonly ToolRegistry _toolRegistry;

    public ToolsController(ILogger<ToolsController> logger, ToolRegistry toolRegistry)
    {
        _logger = logger;
        _toolRegistry = toolRegistry;
    }

    /// <summary>
    /// Get all available tools
    /// </summary>
    [HttpGet]
    public IActionResult GetTools()
    {
        var tools = _toolRegistry.AllTools;
        return Ok(tools);
    }

    /// <summary>
    /// Get all enabled tools
    /// </summary>
    [HttpGet("enabled")]
    public IActionResult GetEnabledTools()
    {
        var tools = _toolRegistry.EnabledTools;
        return Ok(tools);
    }

    /// <summary>
    /// Enable specific tools by name
    /// </summary>
    [HttpPost("enabled")]
    public IActionResult EnableTools([FromBody] string[] toolNames)
    {
        try
        {
            _toolRegistry.SetEnabledTools(toolNames);
            _logger.LogInformation("Enabled tools: {ToolNames}", string.Join(", ", toolNames));
            return Ok(_toolRegistry.EnabledTools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling tools");
            return StatusCode(500, new { error = "Error enabling tools", message = ex.Message });
        }
    }
}
