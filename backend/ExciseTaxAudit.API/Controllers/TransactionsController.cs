using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for querying and reviewing flagged transactions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly AuditContext _dbContext;
    private readonly AnomalyDetectionService _anomalyService;
    private readonly GenAIExplanationService _genAIService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        AuditContext dbContext,
        AnomalyDetectionService anomalyService,
        GenAIExplanationService genAIService,
        ILogger<TransactionsController> logger)
    {
        _dbContext = dbContext;
        _anomalyService = anomalyService;
        _genAIService = genAIService;
        _logger = logger;
    }

    /// <summary>
    /// Get all flagged transactions (anomaly score > threshold), ordered by severity.
    /// </summary>
    [HttpGet("flagged")]
    public async Task<ActionResult<List<FlaggedTransactionDTO>>> GetFlaggedTransactionsAsync(
        float scoreThreshold = 0.5f,
        int pageSize = 50,
        int pageNumber = 1,
        int? engagementId = null)
    {
        try
        {
            var query = _dbContext.TransactionRecords.AsQueryable();
            if (engagementId.HasValue)
            {
                query = query.Where(t => t.EngagementId == engagementId.Value);
            }

            var flagged = await query
                .Where(t => t.AnomalyScore > scoreThreshold)
                .OrderByDescending(t => t.AnomalyScore)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = new List<FlaggedTransactionDTO>();
            foreach (var record in flagged)
            {
                var expectedTax = _anomalyService.CalculateExpectedTax(record);
                var dto = new FlaggedTransactionDTO
                {
                    RecordID = record.RecordID,
                    TransactionNumber = record.TransactionNumber,
                    TransactionDate = record.TransactionDate,
                    MerchantName = record.MerchantName,
                    MerchantState = record.MerchantState,
                    FuelType = record.FuelType,
                    Quantity = record.Quantity,
                    PricePerUnit = record.PricePerUnit,
                    NetCost = record.NetCost,
                    TotalTaxAmount = record.TotalTaxAmount,
                    AnomalyScore = record.AnomalyScore ?? 0,
                    AnomalyReason = record.AnomalyReason,
                    ExpectedTaxAmount = expectedTax,
                    TaxDifference = record.TotalTaxAmount - expectedTax,
                    PredictedClaimType = record.Label_OverUnder,
                    Confidence = record.Confidence,
                    ExplanationText = record.GenAIExplanation,
                    IsReviewed = record.IsReviewed,
                    AuditorNotes = record.AuditorNotes,
                    Status = record.Status,
                    ReviewedBy = record.ReviewedBy,
                    ReviewedAt = record.ReviewedDate,
                    ClaimAmount = Math.Abs(record.TotalTaxAmount - expectedTax)
                };
                dtos.Add(dto);
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving flagged transactions: {ex.Message}");
            return StatusCode(500, "Error retrieving transactions.");
        }
    }

    /// <summary>
    /// Get a specific transaction by ID with full details and explanation.
    /// </summary>
    [HttpGet("{recordId}")]
    public async Task<ActionResult<FlaggedTransactionDTO>> GetTransactionAsync(long recordId, int? engagementId = null)
    {
        try
        {
            var query = _dbContext.TransactionRecords.AsQueryable();
            if (engagementId.HasValue)
            {
                query = query.Where(t => t.EngagementId == engagementId.Value);
            }

            var record = await query.FirstOrDefaultAsync(t => t.RecordID == recordId);
            if (record == null)
                return NotFound();

            var expectedTax = _anomalyService.CalculateExpectedTax(record);
            var explanation = await _genAIService.GenerateExplanationAsync(
                record, record.AnomalyReason ?? "", expectedTax);

            var dto = new FlaggedTransactionDTO
            {
                RecordID = record.RecordID,
                TransactionNumber = record.TransactionNumber,
                TransactionDate = record.TransactionDate,
                MerchantName = record.MerchantName,
                MerchantState = record.MerchantState,
                FuelType = record.FuelType,
                Quantity = record.Quantity,
                PricePerUnit = record.PricePerUnit,
                NetCost = record.NetCost,
                TotalTaxAmount = record.TotalTaxAmount,
                AnomalyScore = record.AnomalyScore ?? 0,
                AnomalyReason = record.AnomalyReason,
                ExpectedTaxAmount = expectedTax,
                TaxDifference = record.TotalTaxAmount - expectedTax,
                PredictedClaimType = record.Label_OverUnder,
                Confidence = record.Confidence,
                ExplanationText = record.GenAIExplanation ?? explanation,
                IsReviewed = record.IsReviewed,
                AuditorNotes = record.AuditorNotes
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving transaction {recordId}: {ex.Message}");
            return StatusCode(500, "Error retrieving transaction.");
        }
    }

    /// <summary>
    /// Mark a transaction as reviewed with auditor notes and decision.
    /// </summary>
    [HttpPost("{recordId}/review")]
    public async Task<IActionResult> ReviewTransactionAsync(long recordId, [FromBody] ReviewRequest request, int? engagementId = null)
    {
        try
        {
            var query = _dbContext.TransactionRecords.AsQueryable();
            if (engagementId.HasValue)
            {
                query = query.Where(t => t.EngagementId == engagementId.Value);
            }

            var record = await query.FirstOrDefaultAsync(t => t.RecordID == recordId);
            if (record == null)
                return NotFound();

            record.IsReviewed = true;
            record.AuditorNotes = request.Notes;
            record.Label_OverUnder = request.Decision;
            record.Status = request.Decision; // Update Status field based on decision
            record.ReviewedBy = "Auditor"; // Default value, can be updated from auth context
            record.ReviewedDate = DateTime.UtcNow;
            if (request.AdjustmentAmount.HasValue)
            {
                record.Label_AdjustmentAmount = request.AdjustmentAmount.Value;
            }

            _dbContext.TransactionRecords.Update(record);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Transaction reviewed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reviewing transaction {recordId}: {ex.Message}");
            return StatusCode(500, "Error reviewing transaction.");
        }
    }

    /// <summary>
    /// Query transactions using natural language
    /// </summary>
    [HttpPost("query/natural-language")]
    public async Task<ActionResult<object>> QueryByNaturalLanguageAsync([FromBody] NaturalLanguageQueryRequest request, int? engagementId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Query))
                return BadRequest("Query is required");

            _logger.LogInformation($"Processing natural language query: {request.Query}");

            // Parse natural language query to filter criteria
            var queryLower = request.Query.ToLower();
            var query = _dbContext.TransactionRecords.AsQueryable();

            if (engagementId.HasValue)
            {
                query = query.Where(t => t.EngagementId == engagementId.Value);
            }

            var anyFilterApplied = false;

            // Example: "Show me transactions over $500"
            if (queryLower.Contains("over $"))
            {
                var amountStr = System.Text.RegularExpressions.Regex.Match(queryLower, @"\$(\d+(?:\.\d{2})?)");
                if (amountStr.Success && decimal.TryParse(amountStr.Groups[1].Value, out var amount))
                {
                    query = query.Where(t => t.NetCost > amount);
                    anyFilterApplied = true;
                }
            }

            // Example: "Find anomalies in Texas"
            if (queryLower.Contains("in ") && queryLower.Contains("texas"))
            {
                query = query.Where(t => t.MerchantState == "TX");
                anyFilterApplied = true;
            }

            // Example: "List high anomaly score transactions"
            if (queryLower.Contains("high anomaly") || queryLower.Contains("> 0.8"))
            {
                query = query.Where(t => t.AnomalyScore > 0.8);
                anyFilterApplied = true;
            }

            // Example: "Show unreviewed flagged transactions"
            if (queryLower.Contains("unreviewed"))
            {
                query = query.Where(t => !t.IsReviewed && t.AnomalyScore > 0.5);
                anyFilterApplied = true;
            }

            // Example: "Find price outliers"
            if (queryLower.Contains("outliers") || queryLower.Contains("deviation"))
            {
                query = query.Where(t => t.AnomalyScore > 0.7);
                anyFilterApplied = true;
            }

            // Default: flagged transactions
            if (!anyFilterApplied)
            {
                query = query.Where(t => t.AnomalyScore > 0.5f);
            }

            var results = await query
                .OrderByDescending(t => t.AnomalyScore)
                .Take(100)
                .ToListAsync();

            var dtos = new List<FlaggedTransactionDTO>();
            foreach (var record in results)
            {
                var expectedTax = _anomalyService.CalculateExpectedTax(record);
                var dto = new FlaggedTransactionDTO
                {
                    RecordID = record.RecordID,
                    TransactionNumber = record.TransactionNumber,
                    TransactionDate = record.TransactionDate,
                    MerchantName = record.MerchantName,
                    MerchantState = record.MerchantState,
                    FuelType = record.FuelType,
                    Quantity = record.Quantity,
                    PricePerUnit = record.PricePerUnit,
                    NetCost = record.NetCost,
                    TotalTaxAmount = record.TotalTaxAmount,
                    AnomalyScore = record.AnomalyScore ?? 0,
                    AnomalyReason = record.AnomalyReason,
                    ExpectedTaxAmount = expectedTax,
                    TaxDifference = record.TotalTaxAmount - expectedTax,
                    IsReviewed = record.IsReviewed,
                    AuditorNotes = record.AuditorNotes
                };
                dtos.Add(dto);
            }

            return Ok(new { results = dtos, count = dtos.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing natural language query: {ex.Message}");
            return StatusCode(500, "Error processing query.");
        }
    }

    /// <summary>
    /// Get summary statistics of flagged transactions.
    /// </summary>
    [HttpGet("summary/stats")]
    public async Task<ActionResult<object>> GetSummaryStatsAsync(int? engagementId = null)
    {
        try
        {
            var query = _dbContext.TransactionRecords.AsQueryable();
            if (engagementId.HasValue)
            {
                query = query.Where(t => t.EngagementId == engagementId.Value);
            }

            var totalRecords = await query.CountAsync();
            var flaggedRecords = await query
                .Where(t => t.AnomalyScore > 0.5f)
                .CountAsync();
            var reviewedRecords = await query
                .Where(t => t.IsReviewed)
                .CountAsync();

            var overPayments = await query
                .Where(t => t.AnomalyScore > 0.5f && t.Label_OverUnder == "OVER")
                .SumAsync(t => Math.Abs(t.Label_AdjustmentAmount ?? 0));

            var underPayments = await query
                .Where(t => t.AnomalyScore > 0.5f && t.Label_OverUnder == "UNDER")
                .SumAsync(t => Math.Abs(t.Label_AdjustmentAmount ?? 0));

            return Ok(new
            {
                totalRecords,
                flaggedRecords,
                reviewedRecords,
                flaggingRate = totalRecords > 0 ? (double)flaggedRecords / totalRecords : 0,
                estimatedOverPayments = overPayments,
                estimatedUnderPayments = underPayments,
                potentialRecovery = overPayments + underPayments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving stats: {ex.Message}");
            return StatusCode(500, "Error retrieving statistics.");
        }
    }

    /// <summary>
    /// Get per-state summary counts for geographic analytics.
    /// Uses MerchantState and the same anomaly score threshold used elsewhere.
    /// </summary>
    [HttpGet("summary/states")]
    public async Task<ActionResult<IEnumerable<object>>> GetStateSummaryAsync(float scoreThreshold = 0.5f, int? engagementId = null)
    {
        try
        {
            var scoped = _dbContext.TransactionRecords.AsNoTracking();
            if (engagementId.HasValue)
            {
                scoped = scoped.Where(t => t.EngagementId == engagementId.Value);
            }

            var raw = await scoped
                .GroupBy(t => t.MerchantState)
                .Select(g => new
                {
                    MerchantState = g.Key,
                    TotalCount = g.Count(),
                    FlaggedCount = g.Count(t => t.AnomalyScore > scoreThreshold),
                    ReviewedCount = g.Count(t => t.IsReviewed)
                })
                .ToListAsync();

            var normalized = raw
                .Select(r => new
                {
                    StateCode = NormalizeStateCode(r.MerchantState),
                    r.TotalCount,
                    r.FlaggedCount,
                    r.ReviewedCount
                })
                .Where(r => !string.IsNullOrWhiteSpace(r.StateCode))
                .GroupBy(r => r.StateCode)
                .Select(g => new
                {
                    stateCode = g.Key,
                    totalCount = g.Sum(x => x.TotalCount),
                    flaggedCount = g.Sum(x => x.FlaggedCount),
                    reviewedCount = g.Sum(x => x.ReviewedCount)
                })
                .OrderBy(x => x.stateCode)
                .ToList();

            return Ok(normalized);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving state summary: {ex.Message}");
            return StatusCode(500, "Error retrieving state summary.");
        }
    }

    private static string NormalizeStateCode(string? merchantState)
    {
        if (string.IsNullOrWhiteSpace(merchantState))
            return "";

        // Keep only the first token to avoid values like "CA - California".
        var token = merchantState.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        token = token.Trim().ToUpperInvariant();

        // Common case: already a 2-letter code.
        if (token.Length == 2)
            return token;

        // If the token contains punctuation like "CA," strip trailing punctuation.
        token = token.TrimEnd(',', '.', ';', ':');
        if (token.Length == 2)
            return token;

        return "";
    }
}

/// <summary>
/// Request body for reviewing a transaction.
/// </summary>
public class ReviewRequest
{
    public string? Decision { get; set; } // "OVER", "UNDER", "OK"
    public string? Notes { get; set; }
    public decimal? AdjustmentAmount { get; set; }
}

/// <summary>
/// Request for natural language query
/// </summary>
public class NaturalLanguageQueryRequest
{
    public string Query { get; set; }
}
