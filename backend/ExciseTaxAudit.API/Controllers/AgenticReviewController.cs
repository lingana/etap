using ExciseTaxAudit.API.DTOs;
using ExciseTaxAudit.API.Services;
using ExciseTaxAudit.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgenticReviewController : ControllerBase
{
    private readonly AgenticReviewService _agenticService;
    private readonly AuditContext _dbContext;
    private readonly ILogger<AgenticReviewController> _logger;

    public AgenticReviewController(
        AgenticReviewService agenticService,
        AuditContext dbContext,
        ILogger<AgenticReviewController> logger)
    {
        _agenticService = agenticService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Triggers autonomous AI agent review of a flagged transaction
    /// </summary>
    /// <param name="recordId">Transaction record ID</param>
    /// <param name="request">Review configuration options</param>
    /// <returns>Agentic review result with reasoning steps</returns>
    [HttpPost("review/{recordId}")]
    public async Task<ActionResult<AgenticReviewResult>> ReviewTransaction(
        int recordId,
        [FromBody] AgenticReviewRequest? request = null)
    {
        try
        {
            _logger.LogInformation("Starting agentic review for transaction {RecordId}", recordId);

            var result = await _agenticService.ReviewTransactionAsync(recordId, request);

            _logger.LogInformation(
                "Agentic review completed for {RecordId}: {Recommendation} with {Confidence:P0} confidence in {Duration}ms",
                recordId,
                result.Recommendation,
                result.ConfidenceScore,
                result.ProcessingTime.TotalMilliseconds
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agentic review of transaction {RecordId}", recordId);
            return StatusCode(500, new { error = "Failed to perform agentic review", details = ex.Message });
        }
    }

    /// <summary>
    /// Batch review multiple transactions using AI agent with parallel processing
    /// </summary>
    /// <param name="recordIds">List of transaction IDs to review</param>
    /// <returns>List of agentic review results with errors tracked separately</returns>
    [HttpPost("batch-review")]
    public async Task<ActionResult<BatchReviewResponse>> BatchReview(
        [FromBody] List<int> recordIds)
    {
        try
        {
            if (recordIds.Count > 100)
            {
                return BadRequest(new { error = "Maximum 100 transactions per batch" });
            }

            if (recordIds.Count == 0)
            {
                return BadRequest(new { error = "No transaction IDs provided" });
            }

            _logger.LogInformation("Starting batch agentic review for {Count} transactions", recordIds.Count);
            var startTime = DateTime.UtcNow;

            var results = new List<AgenticReviewResult>();
            var errors = new List<BatchReviewError>();

            // Parallel processing with max degree of parallelism to avoid overwhelming Azure OpenAI
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = 5 // Process 5 at a time to stay within API rate limits
            };

            var resultBag = new System.Collections.Concurrent.ConcurrentBag<AgenticReviewResult>();
            var errorBag = new System.Collections.Concurrent.ConcurrentBag<BatchReviewError>();

            await Parallel.ForEachAsync(recordIds, parallelOptions, async (recordId, ct) =>
            {
                try
                {
                    var result = await _agenticService.ReviewTransactionAsync(recordId);
                    resultBag.Add(result);
                    _logger.LogDebug("Completed review for transaction {RecordId}", recordId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to review transaction {RecordId} in batch", recordId);
                    errorBag.Add(new BatchReviewError
                    {
                        RecordId = recordId,
                        ErrorMessage = ex.Message,
                        ErrorType = ex.GetType().Name
                    });
                }
            });

            results = resultBag.OrderBy(r => r.RecordID).ToList();
            errors = errorBag.OrderBy(e => e.RecordId).ToList();

            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "Batch review completed: {Successful}/{Total} transactions in {Duration}ms",
                results.Count, 
                recordIds.Count,
                duration.TotalMilliseconds);

            var response = new BatchReviewResponse
            {
                TotalRequested = recordIds.Count,
                SuccessfulReviews = results.Count,
                FailedReviews = errors.Count,
                ProcessingTimeMs = (int)duration.TotalMilliseconds,
                Results = results,
                Errors = errors,
                Summary = new BatchReviewSummary
                {
                    ApprovalCount = results.Count(r => r.Recommendation.Contains("APPROVE", StringComparison.OrdinalIgnoreCase)),
                    RejectionCount = results.Count(r => r.Recommendation.Contains("REJECT", StringComparison.OrdinalIgnoreCase)),
                    ManualReviewCount = results.Count(r => r.Recommendation.Contains("MANUAL", StringComparison.OrdinalIgnoreCase)),
                    AverageConfidence = results.Any() ? results.Average(r => r.ConfidenceScore) : 0,
                    TotalPotentialRecovery = results.Sum(r => r.RecommendedClaimAmount ?? 0)
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch agentic review");
            return StatusCode(500, new { error = "Failed to perform batch review", details = ex.Message });
        }
    }

    /// <summary>
    /// Auto-review all flagged transactions for an engagement
    /// </summary>
    /// <param name="engagementId">Engagement ID</param>
    /// <param name="minScore">Minimum anomaly score (default: 0.5)</param>
    /// <param name="maxTransactions">Maximum transactions to review (default: 50)</param>
    /// <returns>Batch review results</returns>
    [HttpPost("review-flagged/{engagementId}")]
    public async Task<ActionResult<BatchReviewResponse>> ReviewFlaggedTransactions(
        int engagementId,
        [FromQuery] float minScore = 0.5f,
        [FromQuery] int maxTransactions = 50)
    {
        try
        {
            // Fetch flagged transactions that haven't been reviewed yet
            var flaggedTransactions = await _dbContext.TransactionRecords
                .Where(t => t.EngagementId == engagementId 
                         && t.AnomalyScore >= minScore
                         && t.Status == "FLAGGED") // Not yet reviewed
                .OrderByDescending(t => t.AnomalyScore)
                .Take(maxTransactions)
                .Select(t => t.RecordID)
                .ToListAsync();

            if (!flaggedTransactions.Any())
            {
                return Ok(new BatchReviewResponse
                {
                    TotalRequested = 0,
                    SuccessfulReviews = 0,
                    FailedReviews = 0,
                    ProcessingTimeMs = 0,
                    Results = new List<AgenticReviewResult>(),
                    Errors = new List<BatchReviewError>(),
                    Summary = new BatchReviewSummary()
                });
            }

            _logger.LogInformation(
                "Auto-reviewing {Count} flagged transactions for engagement {EngagementId}",
                flaggedTransactions.Count,
                engagementId);

            // Use the existing batch review logic
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/AgenticReview/batch-review");
            return await BatchReview(flaggedTransactions.Select(id => (int)id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-review of flagged transactions");
            return StatusCode(500, new { error = "Failed to auto-review flagged transactions", details = ex.Message });
        }
    }

    /// <summary>
    /// Get statistics about agentic review performance
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<object> GetStats()
    {
        // This would be enhanced with actual tracking in production
        return Ok(new
        {
            agentVersion = "1.0",
            modelProvider = "Azure OpenAI",
            plannerType = "Semantic Kernel",
            pluginsEnabled = new[] { "TaxRate", "CalculationValidator", "HistoricalApproval", "IRSTaxRateRAG" },
            averageProcessingTime = "3.2s",
            confidenceThreshold = 0.85m,
            ragEnabled = true,
            irsIntegration = "Active"
        });
    }
}

// DTOs for batch review
public class BatchReviewResponse
{
    public int TotalRequested { get; set; }
    public int SuccessfulReviews { get; set; }
    public int FailedReviews { get; set; }
    public int ProcessingTimeMs { get; set; }
    public List<AgenticReviewResult> Results { get; set; } = new();
    public List<BatchReviewError> Errors { get; set; } = new();
    public BatchReviewSummary Summary { get; set; } = new();
}

public class BatchReviewError
{
    public int RecordId { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string ErrorType { get; set; } = "";
}

public class BatchReviewSummary
{
    public int ApprovalCount { get; set; }
    public int RejectionCount { get; set; }
    public int ManualReviewCount { get; set; }
    public decimal AverageConfidence { get; set; }
    public decimal TotalPotentialRecovery { get; set; }
}
