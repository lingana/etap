using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExciseTaxAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RefundClaimsController : ControllerBase
{
    private readonly RefundClaimService _claimService;
    private readonly ILogger<RefundClaimsController> _logger;

    public RefundClaimsController(
        RefundClaimService claimService,
        ILogger<RefundClaimsController> logger)
    {
        _claimService = claimService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new refund claim directly (manual entry)
    /// POST api/refundclaims
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RefundClaim>> CreateClaim([FromBody] CreateClaimRequest request)
    {
        try
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var claim = await _claimService.CreateClaimAsync(request, username);
            return Ok(claim);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refund claim");
            return StatusCode(500, new { error = "Failed to create claim" });
        }
    }

    /// <summary>
    /// Generate a new refund claim from flagged transactions
    /// POST api/refundclaims/generate
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<RefundClaim>> GenerateClaim([FromBody] GenerateClaimRequest request)
    {
        try
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            
            var claim = await _claimService.GenerateClaimFromTransactionsAsync(
                request.EngagementId,
                request.TransactionIds,
                username,
                request.AuditCaseId
            );

            return Ok(claim);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating refund claim");
            return StatusCode(500, new { error = "Failed to generate claim" });
        }
    }

    /// <summary>
    /// Get all refund claims, optionally filtered by engagement
    /// GET api/refundclaims?engagementId=123
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RefundClaim>>> GetClaims([FromQuery] int? engagementId = null)
    {
        try
        {
            var claims = await _claimService.GetClaimsAsync(engagementId);
            return Ok(claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving refund claims");
            return StatusCode(500, new { error = "Failed to retrieve claims" });
        }
    }

    /// <summary>
    /// Get a specific refund claim by ID
    /// GET api/refundclaims/123
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RefundClaim>> GetClaim(int id)
    {
        try
        {
            var claim = await _claimService.GetClaimByIdAsync(id);
            if (claim == null)
                return NotFound(new { error = $"Claim {id} not found" });

            return Ok(claim);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving claim {ClaimId}", id);
            return StatusCode(500, new { error = "Failed to retrieve claim" });
        }
    }

    /// <summary>
    /// Update claim status
    /// PUT api/refundclaims/123/status
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<RefundClaim>> UpdateStatus(
        int id, 
        [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            
            var claim = await _claimService.UpdateClaimStatusAsync(
                id, 
                request.Status, 
                username,
                request.Notes
            );

            return Ok(claim);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating claim status");
            return StatusCode(500, new { error = "Failed to update status" });
        }
    }

    /// <summary>
    /// Update claim details
    /// PUT api/refundclaims/123
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<RefundClaim>> UpdateClaim(
        int id,
        [FromBody] UpdateClaimRequest request)
    {
        try
        {
            var claim = await _claimService.UpdateClaimAsync(id, request);
            return Ok(claim);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating claim");
            return StatusCode(500, new { error = "Failed to update claim" });
        }
    }

    /// <summary>
    /// Get refund claims summary/statistics
    /// GET api/refundclaims/summary?engagementId=123
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<RefundClaimSummary>> GetSummary([FromQuery] int? engagementId = null)
    {
        try
        {
            var summary = await _claimService.GetClaimSummaryAsync(engagementId);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving claims summary");
            return StatusCode(500, new { error = "Failed to retrieve summary" });
        }
    }

    /// <summary>
    /// Delete a draft claim
    /// DELETE api/refundclaims/123
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteClaim(int id)
    {
        try
        {
            await _claimService.DeleteClaimAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting claim");
            return StatusCode(500, new { error = "Failed to delete claim" });
        }
    }

    /// <summary>
    /// Generate IRS Form 8849 PDF for a claim
    /// POST api/refundclaims/123/generate-form
    /// </summary>
    [HttpPost("{id}/generate-form")]
    public async Task<ActionResult> GenerateForm8849(int id)
    {
        try
        {
            var pdfPath = await _claimService.GenerateForm8849Async(id);
            return Ok(new { formPath = pdfPath });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Form 8849");
            return StatusCode(500, new { error = "Failed to generate form" });
        }
    }

    /// <summary>
    /// Download Form 8849 PDF
    /// GET api/refundclaims/123/download-form
    /// </summary>
    [HttpGet("{id}/download-form")]
    public async Task<ActionResult> DownloadForm8849(int id)
    {
        try
        {
            var (fileBytes, fileName) = await _claimService.GetForm8849FileAsync(id);
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "Form PDF not found. Generate it first." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading Form 8849");
            return StatusCode(500, new { error = "Failed to download form" });
        }
    }
}

// DTOs
public record GenerateClaimRequest(
    int EngagementId,
    List<int> TransactionIds,
    int? AuditCaseId = null
);

public record UpdateStatusRequest(
    ClaimStatus Status,
    string? Notes = null
);

public record UpdateClaimRequest(
    string? EIN,
    string? NameOfClaimant,
    string? ClaimantAddress,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    decimal? ApprovedAmount,
    decimal? PaidAmount,
    string? IRSResponseNotes,
    string? InternalNotes,
    int? Priority
);

public record CreateClaimRequest(
    int EngagementId,
    string? EIN,
    string? NameOfClaimant,
    string? ClaimantAddress,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string? TaxType,
    RefundType? RefundType,
    decimal? ClaimedAmount,
    int TaxYear = 0,
    string? TaxQuarter = null,
    DateTime? TaxPeriodStart = null,
    DateTime? TaxPeriodEnd = null,
    string? Justification = null,
    string? InternalNotes = null,
    int? Priority = 3
);
