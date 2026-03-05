using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ExciseTaxAudit.API.Services;

namespace ExciseTaxAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]  // No auth required for diagnostics
public class DiagnosticsController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly IAzureOpenAIConfigService _config;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        Kernel kernel,
        IAzureOpenAIConfigService config,
        ILogger<DiagnosticsController> logger)
    {
        _kernel = kernel;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Test Azure OpenAI connection and configuration
    /// </summary>
    [HttpGet("test-azure-openai")]
    public async Task<ActionResult<object>> TestAzureOpenAI()
    {
        var diagnostics = new
        {
            Endpoint = _config.GetEndpoint(),
            DeploymentId = _config.GetDeploymentId(),
            ApiKeyConfigured = !string.IsNullOrEmpty(_config.GetApiKey()),
            ApiKeyLength = _config.GetApiKey()?.Length ?? 0,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Testing Azure OpenAI - Endpoint: {Endpoint}, DeploymentId: {DeploymentId}, ApiKey Length: {KeyLength}",
            diagnostics.Endpoint, diagnostics.DeploymentId, diagnostics.ApiKeyLength);

        try
        {
            // Try to get chat service
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            if (chatService == null)
            {
                return StatusCode(500, new
                {
                    status = "FAILED",
                    error = "Chat completion service is null",
                    configuration = diagnostics
                });
            }

            // Try a simple chat completion
            var testMessage = "Say 'Hello' if you can hear me.";
            _logger.LogInformation("Sending test message to Azure OpenAI...");

            var response = await chatService.GetChatMessageContentAsync(testMessage);

            return Ok(new
            {
                status = "SUCCESS",
                message = "Azure OpenAI connection is working!",
                testResponse = response.Content,
                configuration = diagnostics
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get chat completion service");
            return StatusCode(500, new
            {
                status = "FAILED",
                error = "Chat completion service not available",
                details = ex.Message,
                innerError = ex.InnerException?.Message,
                configuration = diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure OpenAI connection");
            return StatusCode(500, new
            {
                status = "FAILED",
                error = ex.Message,
                type = ex.GetType().Name,
                innerError = ex.InnerException?.Message,
                stackTrace = ex.StackTrace,
                configuration = diagnostics
            });
        }
    }

    /// <summary>
    /// Get Azure OpenAI configuration (without exposing full API key)
    /// </summary>
    [HttpGet("config")]
    public ActionResult<object> GetConfiguration()
    {
        return Ok(new
        {
            endpoint = _config.GetEndpoint(),
            deploymentId = _config.GetDeploymentId(),
            apiKeyConfigured = !string.IsNullOrEmpty(_config.GetApiKey()),
            apiKeyPreview = MaskApiKey(_config.GetApiKey()),
            enabled = true,
            note = "If you're getting DeploymentNotFound errors, check that the deploymentId matches your actual deployment name in Azure OpenAI Studio"
        });
    }

    /// <summary>
    /// Try common deployment names to help identify the correct one
    /// </summary>
    [HttpGet("test-deployments")]
    public async Task<ActionResult<object>> TestCommonDeployments()
    {
        var endpoint = _config.GetEndpoint();
        var apiKey = _config.GetApiKey();

        // Common deployment names to try
        var commonDeploymentNames = new[]
        {
            "gpt-4",
            "gpt-4o",
            "gpt-4-turbo",
            "gpt-35-turbo",
            "gpt-35-turbo-16k",
            "gpt-4-32k",
            "gpt-4o-mini",
            _config.GetDeploymentId() // Current configured one
        };

        var results = new List<object>();

        foreach (var deploymentName in commonDeploymentNames.Distinct())
        {
            try
            {
                var client = new Azure.AI.OpenAI.OpenAIClient(
                    new Uri(endpoint),
                    new Azure.AzureKeyCredential(apiKey));

                var chatOptions = new Azure.AI.OpenAI.ChatCompletionsOptions
                {
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new Azure.AI.OpenAI.ChatRequestUserMessage("Hi")
                    },
                    MaxTokens = 5
                };

                var response = await client.GetChatCompletionsAsync(chatOptions);

                results.Add(new
                {
                    deploymentName = deploymentName,
                    status = "SUCCESS",
                    message = "✅ This deployment exists and works!"
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    deploymentName = deploymentName,
                    status = "FAILED",
                    error = ex.Message.Contains("DeploymentNotFound") ? "DeploymentNotFound" : ex.Message.Substring(0, Math.Min(100, ex.Message.Length))
                });
            }
        }

        return Ok(new
        {
            endpoint = endpoint,
            testedDeployments = results,
            suggestion = "Use a deployment name with status=SUCCESS in your appsettings.json under AzureOpenAI:DeploymentId"
        });
    }

    /// <summary>
    /// Test Azure OpenAI connection directly using Azure SDK (bypasses Semantic Kernel)
    /// </summary>
    [HttpGet("test-azure-openai-direct")]
    public async Task<ActionResult<object>> TestAzureOpenAIDirect()
    {
        try
        {
            var endpoint = _config.GetEndpoint();
            var apiKey = _config.GetApiKey();
            var deploymentId = _config.GetDeploymentId();

            _logger.LogInformation("Testing direct Azure OpenAI connection - Endpoint: {Endpoint}, Deployment: {Deployment}", 
                endpoint, deploymentId);

            // Use Azure OpenAI SDK directly
            var client = new Azure.AI.OpenAI.OpenAIClient(
                new Uri(endpoint),
                new Azure.AzureKeyCredential(apiKey));

            var chatOptions = new Azure.AI.OpenAI.ChatCompletionsOptions
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new Azure.AI.OpenAI.ChatRequestUserMessage("Say 'Hello' if you can hear me.")
                }
            };

            var response = await client.GetChatCompletionsAsync(chatOptions);
            var firstChoice = response.Value.Choices[0];

            return Ok(new
            {
                status = "SUCCESS",
                message = "Direct Azure OpenAI connection is working!",
                testResponse = firstChoice.Message.Content,
                endpoint = endpoint,
                deploymentId = deploymentId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct Azure OpenAI connection test failed");

            return StatusCode(500, new
            {
                status = "FAILED",
                error = ex.Message,
                type = ex.GetType().Name,
                innerError = ex.InnerException?.Message,
                stackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))
            });
        }
    }

    private string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "NOT_SET";

        if (apiKey.Length < 10)
            return "***";

        return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
    }
}
