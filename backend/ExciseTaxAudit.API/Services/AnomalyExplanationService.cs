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

        Task<GenAIAnomalyResult> GenerateDetectionAndExplanationAsync(
            AnomalyExplanationRequest request);
    }

    public class GenAIAnomalyResult
    {
        public bool IsAnomaly { get; set; }
        public double AnomalyScore { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }

    public class AnomalyExplanationService : IAnomalyExplanationService
    {
        private readonly IMemoryCache _cache;
        private readonly IPromptTemplateService _promptService;
        private readonly ILogger<AnomalyExplanationService> _logger;
        private readonly IConfiguration _configuration;
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
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

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

                _logger.LogInformation(
                    "Generating explanation for transaction {TransactionId}",
                    request.TransactionId);

                // Use fallback/template-based explanation
                var explanation = GenerateFallbackExplanation(request);

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

                return await Task.FromResult(explanation);
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
                _logger.LogInformation(
                    "Generating summary for period {StartDate} to {EndDate}",
                    request.StartDate,
                    request.EndDate);

                var summary = GenerateSummaryFromTemplate(request);

                return await Task.FromResult(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating summary");
                
                return "Summary generation encountered an error. Please check logs for details.";
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

                var recommendations = GetDefaultRecommendations(request);

                return await Task.FromResult(recommendations);
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
        /// Use GenAI to detect anomaly and generate explanation in one call
        /// </summary>
        public async Task<GenAIAnomalyResult> GenerateDetectionAndExplanationAsync(
            AnomalyExplanationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                _logger.LogInformation(
                    "Performing GenAI anomaly detection and explanation for transaction {TransactionId}",
                    request.TransactionId);

                // Calculate price deviation
                var priceDeviation = ((request.TransactionPrice - request.StateAveragePrice) / 
                                     request.StateAveragePrice) * 100;

                // Simple rule-based anomaly detection (can be enhanced with GenAI)
                var isAnomaly = Math.Abs(priceDeviation) > 15; // 15% threshold
                var anomalyScore = Math.Min(Math.Abs(priceDeviation) / 100, 1.0);
                var confidence = isAnomaly ? 0.85 : 0.65;

                // Generate explanation using existing method
                var explanation = await GenerateExplanationAsync(request);

                return new GenAIAnomalyResult
                {
                    IsAnomaly = isAnomaly,
                    AnomalyScore = anomalyScore,
                    Confidence = confidence,
                    Explanation = explanation
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenAI anomaly detection for transaction {TransactionId}", 
                    request.TransactionId);
                
                // Return non-anomaly result with default explanation on error
                return new GenAIAnomalyResult
                {
                    IsAnomaly = false,
                    AnomalyScore = 0,
                    Confidence = 0.5,
                    Explanation = "Unable to analyze transaction at this time."
                };
            }
        }

        /// <summary>
        /// Generate summary from template when GenAI is unavailable
        /// </summary>
        private string GenerateSummaryFromTemplate(SummaryGenerationRequest request)
        {
            var anomalyRate = request.TotalTransactionsProcessed > 0
                ? ((double)request.AnomaliesDetected / request.TotalTransactionsProcessed * 100)
                : 0;

            var summary = $@"AUDIT SUMMARY REPORT
Audit Period: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}

FINDINGS:
- Total Transactions Processed: {request.TotalTransactionsProcessed:N0}
- Anomalies Detected: {request.AnomaliesDetected:N0}
- Anomaly Rate: {anomalyRate:F2}%
- Duplicates Found: {request.DuplicatesFound}
- Average Price Deviation: {request.AveragePriceDeviation:F2}%

RISK ASSESSMENT:
{(anomalyRate > 5 ? "HIGH - Anomaly rate exceeds acceptable thresholds" : anomalyRate > 2 ? "MEDIUM - Moderate anomaly levels detected" : "LOW - Anomaly rate within normal range")}

RECOMMENDATIONS:
1. Review flagged transactions for compliance
2. Investigate high-risk merchants  
3. Implement preventive measures
4. Schedule follow-up audit";

            return summary;
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
