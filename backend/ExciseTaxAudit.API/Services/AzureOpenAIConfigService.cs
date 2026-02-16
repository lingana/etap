/**
 * Azure OpenAI Configuration Service
 * Manages connection to Azure OpenAI and provides cost tracking
 */

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ExciseTaxAudit.API.Models;

namespace ExciseTaxAudit.API.Services
{
    public interface IAzureOpenAIConfigService
    {
        string GetEndpoint();
        string GetDeploymentId();
        string GetApiKey();
        string GetDeploymentName();
        void LogApiUsage(int promptTokens, int completionTokens);
        OpenAICostSummary GetCostSummary();
        void ResetCostTracking();
        Task<HealthStatus> CheckEndpointHealthAsync();
    }

    public class AzureOpenAIConfigService : IAzureOpenAIConfigService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIConfigService> _logger;
        private int _totalPromptTokens = 0;
        private int _totalCompletionTokens = 0;
        private readonly object _lockObject = new object();

        // Pricing for gpt-3.5-turbo-16k (adjust based on actual deployment)
        private const decimal PromptTokenPrice = 0.003m / 1000m; // $0.003 per 1k tokens
        private const decimal CompletionTokenPrice = 0.004m / 1000m; // $0.004 per 1k tokens

        public AzureOpenAIConfigService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AzureOpenAIConfigService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetEndpoint()
        {
            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
                throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
            return endpoint;
        }

        public string GetDeploymentId()
        {
            var deploymentId = _configuration["AzureOpenAI:DeploymentId"] ?? "gpt-35-turbo";
            return deploymentId;
        }

        public string GetApiKey()
        {
            var apiKey = _configuration["AzureOpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
            return apiKey;
        }

        public string GetDeploymentName()
        {
            // Alias for GetDeploymentId for Semantic Kernel compatibility
            return GetDeploymentId();
        }

        public void LogApiUsage(int promptTokens, int completionTokens)
        {
            lock (_lockObject)
            {
                _totalPromptTokens += promptTokens;
                _totalCompletionTokens += completionTokens;
            }

            var costThisRequest = (promptTokens * PromptTokenPrice) + 
                                 (completionTokens * CompletionTokenPrice);

            _logger.LogInformation(
                "Azure OpenAI Usage - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Cost: ${Cost:F6}",
                promptTokens,
                completionTokens,
                costThisRequest);
        }

        public OpenAICostSummary GetCostSummary()
        {
            lock (_lockObject)
            {
                var promptCost = _totalPromptTokens * PromptTokenPrice;
                var completionCost = _totalCompletionTokens * CompletionTokenPrice;
                var totalCost = promptCost + completionCost;

                return new OpenAICostSummary
                {
                    TotalPromptTokens = _totalPromptTokens,
                    TotalCompletionTokens = _totalCompletionTokens,
                    PromptCost = promptCost,
                    CompletionCost = completionCost,
                    TotalCost = totalCost,
                    AverageCostPerExplanation = _totalPromptTokens > 0 
                        ? totalCost / _totalPromptTokens 
                        : 0
                };
            }
        }

        public void ResetCostTracking()
        {
            lock (_lockObject)
            {
                _logger.LogInformation(
                    "Resetting cost tracking. Previous total: ${Cost:F6}",
                    GetCostSummary().TotalCost);

                _totalPromptTokens = 0;
                _totalCompletionTokens = 0;
            }
        }

        /// <summary>
        /// Check if Azure OpenAI endpoint is healthy by making a simple API call
        /// </summary>
        public async Task<HealthStatus> CheckEndpointHealthAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                // Make a simple request to check if the endpoint is accessible
                // We'll use the models endpoint which should be available
                var endpoint = GetEndpoint().TrimEnd('/');
                var apiKey = GetApiKey();
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
                
                var response = await _httpClient.GetAsync($"{endpoint}/openai/models?api-version=2023-12-01-preview");
                var duration = DateTime.UtcNow - startTime;

                var isHealthy = response.IsSuccessStatusCode;

                _logger.LogInformation(
                    "Azure OpenAI health check: {Status} ({DurationMs}ms)",
                    isHealthy ? "Healthy" : "Unhealthy",
                    duration.TotalMilliseconds);

                return new HealthStatus
                {
                    IsHealthy = isHealthy,
                    ResponseTimeMs = (int)duration.TotalMilliseconds,
                    LastCheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure OpenAI health check failed");

                return new HealthStatus
                {
                    IsHealthy = false,
                    LastCheckedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Cost summary for Azure OpenAI usage
    /// </summary>
    public class OpenAICostSummary
    {
        public int TotalPromptTokens { get; set; }
        public int TotalCompletionTokens { get; set; }
        public decimal PromptCost { get; set; }
        public decimal CompletionCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerExplanation { get; set; }

        public override string ToString()
        {
            return $"Tokens: {TotalPromptTokens + TotalCompletionTokens} | " +
                   $"Cost: ${TotalCost:F4} | " +
                   $"Avg/Explanation: ${AverageCostPerExplanation:F6}";
        }
    }
}
