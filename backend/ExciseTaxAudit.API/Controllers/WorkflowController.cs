using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using ExciseTaxAudit.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for transaction approval workflow.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly ApprovalWorkflowService _workflowService;
    private readonly AuditContext _context;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        ApprovalWorkflowService workflowService,
        AuditContext context,
        ILogger<WorkflowController> logger)
    {
        _workflowService = workflowService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Review a transaction.
    /// </summary>
    [HttpPost("review/{transactionId}")]
    public async Task<ActionResult<TransactionRecord>> ReviewTransaction(
        long transactionId,
        [FromBody] WorkflowReviewRequest request)
    {
        try
        {
            var transaction = await _workflowService.ReviewTransactionAsync(
                transactionId,
                request.UserId,
                request.UserName,
                request.UserRole,
                request.Comments
            );
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Approve a transaction for claim.
    /// </summary>
    [HttpPost("approve/{transactionId}")]
    public async Task<ActionResult<TransactionRecord>> ApproveTransaction(
        long transactionId,
        [FromBody] WorkflowApprovalRequest request)
    {
        try
        {
            var transaction = await _workflowService.ApproveTransactionAsync(
                transactionId,
                request.UserId,
                request.UserName,
                request.UserRole,
                request.ClaimSchedule,
                request.ClaimAmount,
                request.Comments
            );
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Reject a flagged transaction.
    /// </summary>
    [HttpPost("reject/{transactionId}")]
    public async Task<ActionResult<TransactionRecord>> RejectTransaction(
        long transactionId,
        [FromBody] WorkflowRejectionRequest request)
    {
        try
        {
            var transaction = await _workflowService.RejectTransactionAsync(
                transactionId,
                request.UserId,
                request.UserName,
                request.UserRole,
                request.Reason
            );
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Mark transactions as claimed.
    /// </summary>
    [HttpPost("claim")]
    public async Task<ActionResult<int>> MarkAsClaimed([FromBody] WorkflowClaimRequest request)
    {
        try
        {
            var count = await _workflowService.MarkAsClaimedAsync(
                request.TransactionIds,
                request.UserId,
                request.UserName,
                request.UserRole,
                request.TaxPeriod
            );
            return Ok(new { ClaimedCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking transactions as claimed");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get workflow statistics for an engagement.
    /// </summary>
    [HttpGet("stats/{engagementId}")]
    public async Task<ActionResult<Dictionary<string, int>>> GetWorkflowStats(int engagementId)
    {
        var stats = await _workflowService.GetWorkflowStatsAsync(engagementId);
        return Ok(stats);
    }

    /// <summary>
    /// Get all transactions by status for an engagement.
    /// </summary>
    [HttpGet("status/{engagementId}/{status}")]
    public async Task<ActionResult<List<TransactionRecord>>> GetByStatus(int engagementId, string status)
    {
        var transactions = await _context.TransactionRecords
            .Where(t => t.EngagementId == engagementId && t.Status == status.ToUpper())
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
        
        return Ok(transactions);
    }
}
