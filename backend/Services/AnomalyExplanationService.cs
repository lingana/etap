/**
 * Anomaly Explanation Service
 * Integrates with Azure OpenAI to generate natural language explanations
 * for detected fuel transaction anomalies
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExciseTaxAudit.API.Services
{
    public interface IAnomalyExplanationService
    {
        Task<string> GenerateExplanationAsync(
            AnomalyExplanationRequest request);

        Task<Dictionary<string, string>> GenerateBulkExplanationsAsync(
            IEnumerable<AnomalyExplanationRequest> requests);

        Task<string> GenerateSummaryAsync(
            SummaryGenerationRequest request);

        Task<List<string>> GenerateRecommendationsAsync(
            SummaryGenerationRequest request);

        Task ClearCacheAsync(string transactionId);

        // New: GenAI-based detection and explanation
        Task<GenAIAnomalyResult> GenerateDetectionAndExplanationAsync(
            AnomalyExplanationRequest request);
    }
    public class GenAIAnomalyResult
    {
        public bool IsAnomaly { get; set; }
        public double AnomalyScore { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; }
    }
        /// <summary>
        /// Use GenAI to detect anomaly and generate explanation in one call
        /// </summary>
        public async Task<GenAIAnomalyResult> GenerateDetectionAndExplanationAsync(
            AnomalyExplanationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var prompt = $@"Analyze the following US fuel tax transaction and answer in strict JSON format:
{{
  \"isAnomaly\": true/false, // true if this is an overpayment or underpayment
  \"anomalyScore\": 0.0-1.0, // confidence score (1=very likely anomaly)
  \"explanation\": \"short explanation of why this is or isn't an anomaly, and if over/under payment\"
}}

Transaction:
Merchant: {request.MerchantName}
State: {request.MerchantState}
FuelType: {request.FuelType}
Price: {request.TransactionPrice}
StateAveragePrice: {request.StateAveragePrice}
Quantity: {request.Quantity}
Date: {request.TransactionDate:yyyy-MM-dd}
Supplier: {request.SupplierName}
";

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentId,
                    Messages =
                    {
                        new ChatCompletionRequestSystemMessage(
                            "You are a fuel tax audit expert. Analyze transactions for over/under payment anomalies. Respond ONLY in valid JSON as specified."),
                        new ChatCompletionRequestUserMessage(prompt)
                    },
                    MaxTokens = 300,
                    Temperature = 0.2f
                };

                var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                var content = response.Value.Choices[0].Message.Content;

                // Parse JSON response
                var result = System.Text.Json.JsonSerializer.Deserialize<GenAIAnomalyResult>(content,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Calculate confidence as abs(anomalyScore-0.5)*2
                if (result != null)
                    result.Confidence = Math.Abs(result.AnomalyScore - 0.5) * 2;

                return result ?? new GenAIAnomalyResult { IsAnomaly = false, AnomalyScore = 0, Confidence = 0, Explanation = "GenAI did not return a valid result." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenAI detection/explanation failed for transaction {TransactionId}", request.TransactionId);
                return new GenAIAnomalyResult { IsAnomaly = false, AnomalyScore = 0, Confidence = 0, Explanation = "GenAI error: " + ex.Message };
            }
        }

    public class AnomalyExplanationService : IAnomalyExplanationService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly IMemoryCache _cache;
        private readonly IPromptTemplateService _promptService;
        private readonly ILogger<AnomalyExplanationService> _logger;
        private readonly string _deploymentId;
        private const int CacheDurationDays = 30;
        private const string CacheKeyPrefix = "anomaly_explanation_";

        public AnomalyExplanationService(
            IConfiguration configuration,
            IMemoryCache cache,
            IPromptTemplateService promptService,
            ILogger<AnomalyExplanationService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Get configuration from Key Vault
            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            _deploymentId = configuration["AzureOpenAI:DeploymentId"] ?? "gpt-35-turbo";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI configuration is missing. " +
                    "Please set AzureOpenAI:Endpoint and AzureOpenAI:ApiKey in configuration.");
            }

            _openAIClient = new OpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(apiKey));

            _logger.LogInformation("AnomalyExplanationService initialized");
        }

        /// <summary>
        /// Generate a natural language explanation for a single anomaly
        /// Uses caching to reduce API costs
        /// </summary>
        public async Task<string> GenerateExplanationAsync(
            AnomalyExplanationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Check cache first (cost optimization)
                var cacheKey = GetCacheKey(request.TransactionId);
                if (_cache.TryGetValue(cacheKey, out string cachedExplanation))
                {
                    _logger.LogInformation(
                        "Cache hit for transaction {TransactionId}", 
                        request.TransactionId);
                    return cachedExplanation;
                }

                // Generate prompt from template
                var prompt = _promptService.GetAnomalyExplanationPrompt(request);

                _logger.LogInformation(
                    "Generating explanation for transaction {TransactionId}",
                    request.TransactionId);

                // Call Azure OpenAI
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentId,
                    Messages =
                    {
                        new ChatCompletionRequestSystemMessage(
                            "You are a fuel tax audit expert. Explain detected anomalies " +
                            "in fuel transactions in 1-2 concise sentences. Focus on why " +
                            "the transaction might indicate fraud or compliance issues."),
                        new ChatCompletionRequestUserMessage(prompt)
                    },
                    MaxTokens = 200,
                    Temperature = 0.3f // Lower temperature for consistency
                };

                var response = await _openAIClient.GetChatCompletionsAsync(
                    chatCompletionsOptions);

                var explanation = response.Value.Choices[0].Message.Content;

                // Cache for 30 days
                _cache.Set(
                    cacheKey,
                    explanation,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = 
                            TimeSpan.FromDays(CacheDurationDays)
                    });

                _logger.LogInformation(
                    "Generated explanation for transaction {TransactionId}: {Length} chars",
                    request.TransactionId,
                    explanation.Length);

                return explanation;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating explanation for transaction {TransactionId}",
                    request.TransactionId);

                // Fallback explanation if Gen AI fails
                return GenerateFallbackExplanation(request);
            }
        }

        /// <summary>
        /// Generate explanations for multiple anomalies efficiently
        /// Batches requests to Azure OpenAI when possible
        /// </summary>
        public async Task<Dictionary<string, string>> GenerateBulkExplanationsAsync(
            IEnumerable<AnomalyExplanationRequest> requests)
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));

            var results = new Dictionary<string, string>();
            var requestsList = new List<AnomalyExplanationRequest>(requests);

            _logger.LogInformation(
                "Generating {Count} explanations in bulk",
                requestsList.Count);

            // Process in parallel with throttling
            var semaphore = new System.Threading.SemaphoreSlim(3); // 3 concurrent requests
            var tasks = new List<Task>();

            foreach (var request in requestsList)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var explanation = await GenerateExplanationAsync(request);
                        lock (results)
                        {
                            results[request.TransactionId] = explanation;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Bulk explanation generation complete: {Count} explanations",
                results.Count);

            return results;
        }

        /// <summary>
        /// Generate a summary of anomalies found during a period
        /// Useful for audit reports and executive summaries
        /// </summary>
        public async Task<string> GenerateSummaryAsync(
            SummaryGenerationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var prompt = _promptService.GetSummaryPrompt(request);

                _logger.LogInformation(
                    "Generating summary for period {StartDate} to {EndDate}",
                    request.StartDate,
                    request.EndDate);

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentId,
                    Messages =
                    {
                        new ChatCompletionRequestSystemMessage(
                            "You are a fuel tax audit expert preparing executive summaries. " +
                            "Create concise, actionable summaries of audit findings. " +
                            "Focus on risk assessment and recommended actions."),
                        new ChatCompletionRequestUserMessage(prompt)
                    },
                    MaxTokens = 500,
                    Temperature = 0.3f
                };

                var response = await _openAIClient.GetChatCompletionsAsync(
                    chatCompletionsOptions);

                var summary = response.Value.Choices[0].Message.Content;

                _logger.LogInformation(
                    "Generated summary: {Length} chars",
                    summary.Length);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating summary");
                
                return "Summary generation failed. Please check logs for details.";
            }
        }

        /// <summary>
        /// Generate audit recommendations based on findings
        /// </summary>
        public async Task<List<string>> GenerateRecommendationsAsync(
            SummaryGenerationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogInformation(
                    "Generating recommendations for period {StartDate} to {EndDate}",
                    request.StartDate,
                    request.EndDate);

                var prompt = $@"Based on the following audit period metrics, provide 3-5 specific, actionable recommendations:

Audit Period: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}
Total Transactions Processed: {request.TotalTransactionsProcessed}
Anomalies Detected: {request.AnomaliesDetected}
Anomaly Rate: {(request.TotalTransactionsProcessed > 0 ? ((double)request.AnomaliesDetected / request.TotalTransactionsProcessed * 100).ToString("F2") : "0")}%
Duplicates Found: {request.DuplicatesFound}
Average Price Deviation: {request.AveragePriceDeviation}%

Provide recommendations in this format:
- [PRIORITY: HIGH/MEDIUM/LOW] [CATEGORY] - Specific action item";

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentId,
                    Messages =
                    {
                        new ChatCompletionRequestSystemMessage(
                            "You are a fuel tax audit expert. Generate prioritized, specific recommendations based on audit metrics. " +
                            "Focus on actionable items that reduce risk and improve compliance."),
                        new ChatCompletionRequestUserMessage(prompt)
                    },
                    MaxTokens = 400,
                    Temperature = 0.3f
                };

                var response = await _openAIClient.GetChatCompletionsAsync(
                    chatCompletionsOptions);

                var recommendationsText = response.Value.Choices[0].Message.Content;
                
                // Parse recommendations into list
                var recommendations = new List<string>();
                foreach (var line in recommendationsText.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("- "))
                    {
                        recommendations.Add(trimmed.Substring(2));
                    }
                }

                _logger.LogInformation(
                    "Generated {Count} recommendations",
                    recommendations.Count);

                return recommendations.Count > 0 ? recommendations : GetDefaultRecommendations(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating recommendations");
                
                return GetDefaultRecommendations(request);
            }
        }

        /// <summary>
        /// Clear cached explanation for a transaction
        /// </summary>
        public Task ClearCacheAsync(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
                throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));

            var cacheKey = GetCacheKey(transactionId);
            _cache.Remove(cacheKey);

            _logger.LogInformation(
                "Cleared cache for transaction {TransactionId}",
                transactionId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Default recommendations when GenAI fails
        /// </summary>
        private List<string> GetDefaultRecommendations(SummaryGenerationRequest request)
        {
            var recommendations = new List<string>();
            var anomalyRate = request.TotalTransactionsProcessed > 0 
                ? ((double)request.AnomaliesDetected / request.TotalTransactionsProcessed * 100) 
                : 0;

            if (anomalyRate > 5)
                recommendations.Add("[PRIORITY: HIGH] Compliance - Review merchant pricing policies and contracts");

            if (request.DuplicatesFound > 10)
                recommendations.Add("[PRIORITY: HIGH] Data Quality - Implement duplicate detection system");

            if (request.AveragePriceDeviation > 15)
                recommendations.Add("[PRIORITY: MEDIUM] Price Control - Negotiate better rates with suppliers");

            recommendations.Add("[PRIORITY: MEDIUM] Audit - Schedule manual review of top 10% anomalies");
            recommendations.Add("[PRIORITY: LOW] Process - Implement quarterly audit schedule");

            return recommendations;
        }

        /// <summary>
        /// Fallback explanation when Gen AI is unavailable
        /// Generates rule-based explanation
        /// </summary>
        private string GenerateFallbackExplanation(AnomalyExplanationRequest request)
        {
            var priceDeviation = (request.TransactionPrice - request.StateAveragePrice) / 
                                 request.StateAveragePrice * 100;

            if (Math.Abs(priceDeviation) > 25)
            {
                return $"Significant price deviation: {Math.Abs(priceDeviation):F1}% " +
                       $"from state average. Review merchant pricing.";
            }

            if (request.AnomalyScore > 0.8)
            {
                return "High anomaly score indicates unusual transaction pattern. " +
                       "Recommend manual audit review.";
            }

            return "Transaction flagged as anomalous. Review for compliance.";
        }

        private static string GetCacheKey(string transactionId) =>
            $"{CacheKeyPrefix}{transactionId}";
    }

    /// <summary>
    /// Request object for anomaly explanation
    /// </summary>
    public class AnomalyExplanationRequest
    {
        public string TransactionId { get; set; }
        public string MerchantName { get; set; }
        public string MerchantState { get; set; }
        public string FuelType { get; set; }
        public double TransactionPrice { get; set; }
        public double StateAveragePrice { get; set; }
        public double AnomalyScore { get; set; }
        public double Quantity { get; set; }
        public DateTime TransactionDate { get; set; }
        public string SupplierName { get; set; }
    }

    /// <summary>
    /// Request object for summary generation
    /// </summary>
    public class SummaryGenerationRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalTransactionsProcessed { get; set; }
        public int AnomaliesDetected { get; set; }
        public int DuplicatesFound { get; set; }
        public decimal AveragePriceDeviation { get; set; }
        public string[] HighRiskMerchants { get; set; }
        public string SummaryType { get; set; } // "executive", "detailed", "quick"
    }
}
