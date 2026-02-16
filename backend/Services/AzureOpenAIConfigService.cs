/**
 * Azure OpenAI Configuration Service
 * Manages connection to Azure OpenAI and provides cost tracking
 */

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    }

    public class AzureOpenAIConfigService : IAzureOpenAIConfigService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIConfigService> _logger;
        private int _totalPromptTokens = 0;
        private int _totalCompletionTokens = 0;
        private readonly object _lockObject = new object();

        // Pricing for gpt-3.5-turbo-16k (adjust based on actual deployment)
        private const decimal PromptTokenPrice = 0.003m / 1000m; // $0.003 per 1k tokens
        private const decimal CompletionTokenPrice = 0.004m / 1000m; // $0.004 per 1k tokens

        public AzureOpenAIConfigService(
            IConfiguration configuration,
            ILogger<AzureOpenAIConfigService> logger)
        {
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
