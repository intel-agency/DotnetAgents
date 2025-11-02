using DotnetAgents.Agent.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotnetAgents.Agent.Controllers;

/// <summary>
/// Controller for agent operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly ILogger<AgentController> _logger;

    public AgentController(ILogger<AgentController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Send a prompt to the agent
    /// </summary>
    /// <param name="request">The prompt request containing the prompt and optional parameters</param>
    /// <returns>The agent's response</returns>
    [HttpPost("prompt")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult PromptAgent([FromBody] PromptAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt cannot be empty" });
        }

        _logger.LogInformation("Processing prompt with {Length} characters", request.Prompt.Length);

        try
        {
            // TODO: Implement actual agent logic here
            var response = new
            {
                success = true,
                prompt = request.Prompt,
                response = $"Agent received prompt: {request.Prompt}",
                timestamp = DateTime.UtcNow,
                metadata = new
                {
                    maxTokens = request.MaxTokens,
                    temperature = request.Temperature,
                    contextKeys = request.Context?.Keys.ToArray()
                }
            };

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
