using DotnetAgents.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using DotnetAgents.AgentApi.Services;

namespace DotnetAgents.Agent.Controllers;

/// <summary>
/// Controller for agent operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly ILogger<AgentController> _logger;
    private readonly IAgentService _agentService;

    public AgentController(ILogger<AgentController> logger, IAgentService agentService)
    {
        _logger = logger;
        _agentService = agentService;
    }

    /// <summary>
    /// Send a prompt to the agent
    /// </summary>
    /// <param name="request">The prompt request containing the prompt and optional parameters</param>
    /// <returns>The agent's response</returns>
    [HttpPost("prompt")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PromptAgent([FromBody] PromptAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt cannot be empty" });
        }

        _logger.LogInformation("Processing prompt with {Length} characters", request.Prompt.Length);

        try
        {
            var response = await _agentService.PromptAgentAsync(request.Prompt);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing prompt");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred processing the prompt" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
