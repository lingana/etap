using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for managing comprehensive audit trail logging.
/// Tracks all user actions for IRS compliance and internal controls.
/// </summary>
public class AuditTrailService
{
    private readonly AuditContext _context;
    private readonly ILogger<AuditTrailService> _logger;

    public AuditTrailService(AuditContext context, ILogger<AuditTrailService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Log an action to the audit trail.
    /// </summary>
    public async Task LogActionAsync(AuditLog log)
    {
        log.Timestamp = DateTime.UtcNow;
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get audit logs for a specific engagement.
    /// </summary>
    public async Task<List<AuditLog>> GetEngagementLogsAsync(int engagementId, int maxRecords = 100)
    {
        return await _context.AuditLogs
            .Where(l => l.EngagementId == engagementId)
            .OrderByDescending(l => l.Timestamp)
            .Take(maxRecords)
            .ToListAsync();
    }

    /// <summary>
    /// Get audit logs for a specific transaction.
    /// </summary>
    public async Task<List<AuditLog>> GetTransactionLogsAsync(long transactionId)
    {
        return await _context.AuditLogs
            .Where(l => l.TransactionRecordId == transactionId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    /// <summary>
    /// Get recent activity across all engagements.
    /// </summary>
    public async Task<List<AuditLog>> GetRecentActivityAsync(int maxRecords = 50)
    {
        return await _context.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(maxRecords)
            .ToListAsync();
    }

    /// <summary>
    /// Get activity summary by user.
    /// </summary>
    public async Task<Dictionary<string, int>> GetUserActivitySummaryAsync(int engagementId)
    {
        return await _context.AuditLogs
            .Where(l => l.EngagementId == engagementId)
            .GroupBy(l => l.UserName)
            .Select(g => new { UserName = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserName, x => x.Count);
    }
}
