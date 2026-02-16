/**
 * Enhanced Anomaly Detection Controller
 * Integrates Azure ML real-time inference with Azure OpenAI explanations
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ExciseTaxAudit.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnomalyDetectionController : ControllerBase
    {
        private readonly IAnomalyExplanationService _explanationService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AnomalyDetectionController> _logger;

        public AnomalyDetectionController(
            IAnomalyExplanationService explanationService,
            IAuditService auditService,
            ILogger<AnomalyDetectionController> logger)
        {
            _explanationService = explanationService ?? throw new ArgumentNullException(nameof(explanationService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Detect anomalies in a single transaction with GenAI explanation
        /// </summary>
        [HttpPost("detect-with-explanation")]
        [ProducesResponseType(typeof(AnomalyDetectionWithExplanationResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<AnomalyDetectionWithExplanationResponse>> DetectAnomalyWithExplanationAsync(
            [FromBody] FuelTransactionRequest request)
        {
            if (request == null)
            {
                _logger.LogWarning("Null transaction request received");
                return BadRequest("Transaction data is required");
            }

            try
            {
                _logger.LogInformation(
                    "Processing anomaly detection with explanation for transaction {TransactionId}",
                    request.TransactionId);

                // Step 1: Convert request to transaction object
                var transaction = new FuelTransaction
                {
                    TransactionId = request.TransactionId,
                    MerchantName = request.MerchantName,
                    MerchantState = request.MerchantState,
                    FuelType = request.FuelType,
                    Quantity = request.Quantity,
                    Price = request.Price,
                    StateAveragePrice = request.StateAveragePrice,
                    MerchantCount = request.MerchantCount ?? 1,
                    DayOfWeek = request.DayOfWeek ?? (int)DateTime.UtcNow.DayOfWeek,
                    MonthOfYear = request.MonthOfYear ?? DateTime.UtcNow.Month,
                    SupplierDaysActive = request.SupplierDaysActive ?? 365,
                    TransactionDate = request.TransactionDate ?? DateTime.UtcNow
                };

                            var explainRequest = new AnomalyExplanationRequest
                            {
                                TransactionId = transaction.TransactionId,
                                MerchantName = transaction.MerchantName,
                                MerchantState = transaction.MerchantState,
                                FuelType = transaction.FuelType,
                                TransactionPrice = transaction.Price,
                                StateAveragePrice = transaction.StateAveragePrice,
                                Quantity = transaction.Quantity,
                                TransactionDate = transaction.TransactionDate,
                                SupplierName = request.SupplierName ?? "Unknown"
                            };

                            var genAIResult = await _explanationService.GenerateDetectionAndExplanationAsync(explainRequest);

                            var response = new AnomalyDetectionWithExplanationResponse
                            {
                                TransactionId = transaction.TransactionId,
                                IsAnomaly = genAIResult.IsAnomaly,
                                AnomalyScore = genAIResult.AnomalyScore,
                                Confidence = genAIResult.Confidence,
                                Explanation = genAIResult.Explanation,
                                ProcessedAt = DateTime.UtcNow,
                                MerchantName = transaction.MerchantName,
                                MerchantState = transaction.MerchantState,
                                PriceDeviation = ((transaction.Price - transaction.StateAveragePrice) /
                                                 transaction.StateAveragePrice * 100)
                            };

                            _logger.LogInformation(
                                "GenAI anomaly detection complete. IsAnomaly: {IsAnomaly}, Score: {Score:F3}",
                                genAIResult.IsAnomaly,
                                genAIResult.AnomalyScore);

                            return Ok(response);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing anomaly detection");
                            return StatusCode(500, new { error = "Error processing anomaly detection", details = ex.Message });
                        }
                    }

                    /// <summary>
                    /// Batch detect anomalies with explanations
                    /// Returns detailed report of all anomalies found
                    /// </summary>
                    [HttpPost("detect-batch-with-explanations")]
                    [ProducesResponseType(typeof(BatchDetectionWithExplanationsResponse), 200)]
                    [ProducesResponseType(400)]
                    [ProducesResponseType(401)]
                    [ProducesResponseType(500)]
                    public async Task<ActionResult<BatchDetectionWithExplanationsResponse>> DetectAnomaliesBatchAsync(
                        [FromBody] BatchTransactionRequest request)
                    {
                        if (request?.Transactions == null || request.Transactions.Count == 0)
                        {
                            return BadRequest("At least one transaction is required");
                        }

                        try
                        {
                            _logger.LogInformation(
                                "Processing batch anomaly detection for {Count} transactions",
                                request.Transactions.Count);

                            var response = new BatchDetectionWithExplanationsResponse
                            {
                                RequestId = Guid.NewGuid().ToString(),
                                ProcessedAt = DateTime.UtcNow,
                                Detections = new List<AnomalyDetectionWithExplanationResponse>()
                            };

                            foreach (var txn in request.Transactions)
                            {
                                var transaction = new FuelTransaction
                                {
                                    TransactionId = txn.TransactionId,
                                    MerchantName = txn.MerchantName,
                                    MerchantState = txn.MerchantState,
                                    FuelType = txn.FuelType,
                                    Quantity = txn.Quantity,
                                    Price = txn.Price,
                                    StateAveragePrice = txn.StateAveragePrice,
                                    MerchantCount = txn.MerchantCount ?? 1,
                                    DayOfWeek = txn.DayOfWeek ?? (int)DateTime.UtcNow.DayOfWeek,
                                    MonthOfYear = txn.MonthOfYear ?? DateTime.UtcNow.Month,
                                    SupplierDaysActive = txn.SupplierDaysActive ?? 365,
                                    TransactionDate = txn.TransactionDate ?? DateTime.UtcNow
                                };

                                var explainRequest = new AnomalyExplanationRequest
                                {
                                    TransactionId = transaction.TransactionId,
                                    MerchantName = transaction.MerchantName,
                                    MerchantState = transaction.MerchantState,
                                    FuelType = transaction.FuelType,
                                    TransactionPrice = transaction.Price,
                                    StateAveragePrice = transaction.StateAveragePrice,
                                    Quantity = transaction.Quantity,
                                    TransactionDate = transaction.TransactionDate,
                                    SupplierName = txn.SupplierName ?? "Unknown"
                                };

                                var genAIResult = await _explanationService.GenerateDetectionAndExplanationAsync(explainRequest);

                                response.Detections.Add(new AnomalyDetectionWithExplanationResponse
                                {
                                    TransactionId = transaction.TransactionId,
                                    IsAnomaly = genAIResult.IsAnomaly,
                                    AnomalyScore = genAIResult.AnomalyScore,
                                    Confidence = genAIResult.Confidence,
                                    Explanation = genAIResult.Explanation,
                                    ProcessedAt = DateTime.UtcNow,
                                    MerchantName = transaction.MerchantName,
                                    MerchantState = transaction.MerchantState,
                                    PriceDeviation = ((transaction.Price - transaction.StateAveragePrice) /
                                                     transaction.StateAveragePrice * 100)
                                });

                                if (genAIResult.IsAnomaly)
                                    response.AnomaliesDetected++;
                            }

                            response.TotalProcessed = request.Transactions.Count;
                            response.SuccessfulTransactions = response.TotalProcessed - response.FailedTransactions;
                            response.AnomalyPercentage = response.SuccessfulTransactions > 0
                                ? (double)response.AnomaliesDetected / response.SuccessfulTransactions
                                : 0;

                            _logger.LogInformation(
                                "Batch processing complete: {Total} total, {Anomalies} anomalies, {Failed} failed",
                                response.TotalProcessed,
                                response.AnomaliesDetected,
                                response.FailedTransactions);

                            return Ok(response);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing batch");
                            return StatusCode(500, new { error = "Error processing batch", details = ex.Message });
                        }
                    }

                    /// <summary>
                    /// Generate audit summary and recommendations using GenAI
                    /// </summary>
                    [HttpPost("summary")]
                    [ProducesResponseType(typeof(SummaryGenerationResponse), 200)]
                    [ProducesResponseType(400)]
                    [ProducesResponseType(401)]
                    [ProducesResponseType(500)]
                    public async Task<ActionResult<SummaryGenerationResponse>> GenerateSummaryAsync(
                        [FromBody] SummaryGenerationResponse request)
                    {
                        if (request == null)
                        {
                            return BadRequest("Summary generation request is required");
                        }

                        try
                        {
                            _logger.LogInformation(
                                "Generating audit summary for period {StartDate} to {EndDate}",
                                request.PeriodStart,
                                request.PeriodEnd);

                            var serviceRequest = new Services.SummaryGenerationRequest
                            {
                                StartDate = request.PeriodStart,
                                EndDate = request.PeriodEnd,
                                TotalTransactionsProcessed = request.TotalTransactionsProcessed,
                                AnomaliesDetected = request.AnomaliesDetected,
                                DuplicatesFound = 0,
                                AveragePriceDeviation = 0,
                                HighRiskMerchants = new string[] { },
                                SummaryType = "executive"
                            };

                            var summary = await _explanationService.GenerateSummaryAsync(serviceRequest);
                            var recommendations = await _explanationService.GenerateRecommendationsAsync(serviceRequest);

                            var response = new SummaryGenerationResponse
                            {
                                Summary = summary,
                                Recommendations = recommendations,
                                GeneratedAt = DateTime.UtcNow,
                                PeriodStart = serviceRequest.StartDate,
                                PeriodEnd = serviceRequest.EndDate,
                                TotalTransactionsProcessed = serviceRequest.TotalTransactionsProcessed,
                                AnomaliesDetected = serviceRequest.AnomaliesDetected
                            };

                            return Ok(response);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating summary");
                            return StatusCode(500, new { error = "Error generating summary", details = ex.Message });
                        }
                    }

                    /// <summary>
                    /// Health check for GenAI endpoint
                    /// </summary>
                    [HttpGet("health")]
                    [AllowAnonymous]
                    public ActionResult<object> GetHealthAsync()
                    {
                        return Ok(new { status = "ML endpoint removed, using GenAI only." });
                    }
                }

    // Request/Response DTOs
    public class FuelTransactionRequest
    {
        public string TransactionId { get; set; }
        public string MerchantName { get; set; }
        public string MerchantState { get; set; }
        public string FuelType { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
        public double StateAveragePrice { get; set; }
        public int? MerchantCount { get; set; }
        public int? DayOfWeek { get; set; }
        public int? MonthOfYear { get; set; }
        public int? SupplierDaysActive { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string SupplierName { get; set; }
    }

    public class BatchTransactionRequest
    {
        public List<FuelTransactionRequest> Transactions { get; set; }
        public bool IncludeExplanations { get; set; } = true;
    }

    public class AnomalyDetectionWithExplanationResponse
    {
        public string TransactionId { get; set; }
        public bool IsAnomaly { get; set; }
        public double AnomalyScore { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string MerchantName { get; set; }
        public string MerchantState { get; set; }
        public double PriceDeviation { get; set; }
    }

    public class BatchDetectionWithExplanationsResponse
    {
        public string RequestId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessfulTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public int AnomaliesDetected { get; set; }
        public double AnomalyPercentage { get; set; }
        public List<AnomalyDetectionWithExplanationResponse> Detections { get; set; }
    }

    public class SummaryGenerationResponse
    {
        public string Summary { get; set; }
        public List<string> Recommendations { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalTransactionsProcessed { get; set; }
        public int AnomaliesDetected { get; set; }
    }
}