using Microsoft.AspNetCore.Mvc;
using ExciseTaxAudit.API.Services;
using ExciseTaxAudit.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// Health check endpoints for external services
/// </summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IAzureOpenAIConfigService _azureOpenAIService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IAzureOpenAIConfigService azureOpenAIService,
        ILogger<HealthController> logger)
    {
        _azureOpenAIService = azureOpenAIService;
        _logger = logger;
    }

    /// <summary>
    /// Check Azure OpenAI endpoint health
    /// </summary>
    [HttpGet("azure-openai")]
    public async Task<ActionResult<HealthStatus>> GetAzureOpenAIHealth()
    {
        try
        {
            var healthStatus = await _azureOpenAIService.CheckEndpointHealthAsync();
            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check Azure OpenAI health");
            return StatusCode(500, new HealthStatus
            {
                IsHealthy = false,
                LastCheckedAt = DateTime.UtcNow,
                ErrorMessage = "Internal server error"
            });
        }
    }
}