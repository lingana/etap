using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for audit trail and activity logging.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditTrailController : ControllerBase
{
    private readonly AuditTrailService _auditTrail;
    private readonly ILogger<AuditTrailController> _logger;

    public AuditTrailController(AuditTrailService auditTrail, ILogger<AuditTrailController> logger)
    {
        _auditTrail = auditTrail;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs for an engagement.
    /// </summary>
    [HttpGet("engagement/{engagementId}")]
    public async Task<ActionResult<List<AuditLog>>> GetEngagementLogs(int engagementId, [FromQuery] int maxRecords = 100)
    {
        var logs = await _auditTrail.GetEngagementLogsAsync(engagementId, maxRecords);
        return Ok(logs);
    }

    /// <summary>
    /// Get audit logs for a transaction.
    /// </summary>
    [HttpGet("transaction/{transactionId}")]
    public async Task<ActionResult<List<AuditLog>>> GetTransactionLogs(long transactionId)
    {
        var logs = await _auditTrail.GetTransactionLogsAsync(transactionId);
        return Ok(logs);
    }

    /// <summary>
    /// Get recent activity, optionally filtered by engagement.
    /// Accepts both 'maxRecords' and 'limit' as param names for flexibility.
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<AuditLog>>> GetRecentActivity(
        [FromQuery] int? engagementId = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? maxRecords = null)
    {
        var count = limit ?? maxRecords ?? 50;
        var logs = await _auditTrail.GetRecentActivityAsync(count, engagementId);
        return Ok(logs);
    }

    /// <summary>
    /// Get user activity summary for an engagement.
    /// </summary>
    [HttpGet("users/{engagementId}")]
    public async Task<ActionResult<Dictionary<string, int>>> GetUserActivity(int engagementId)
    {
        var summary = await _auditTrail.GetUserActivitySummaryAsync(engagementId);
        return Ok(summary);
    }
}
