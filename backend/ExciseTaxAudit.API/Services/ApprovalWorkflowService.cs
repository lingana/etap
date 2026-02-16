using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for managing transaction approval workflow and status transitions.
/// Ensures compliance with segregation of duties and audit trail requirements.
/// </summary>
public class ApprovalWorkflowService
{
    private readonly AuditContext _context;
    private readonly AuditTrailService _auditTrail;
    private readonly ILogger<ApprovalWorkflowService> _logger;

    public ApprovalWorkflowService(
        AuditContext context,
        AuditTrailService auditTrail,
        ILogger<ApprovalWorkflowService> logger)
    {
        _context = context;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    /// <summary>
    /// Review a transaction and update its status.
    /// </summary>
    public async Task<TransactionRecord> ReviewTransactionAsync(
        long transactionId, 
        string userId, 
        string userName, 
        string userRole,
        string? comments)
    {
        var transaction = await _context.TransactionRecords.FindAsync(transactionId);
        if (transaction == null)
            throw new KeyNotFoundException($"Transaction {transactionId} not found");

        if (transaction.IsLocked)
            throw new InvalidOperationException("Cannot review locked transaction");

        var oldStatus = transaction.Status;
        transaction.Status = "REVIEWED";
        transaction.IsReviewed = true;
        transaction.ReviewedByUserId = userId;
        transaction.ReviewedByUserName = userName;
        transaction.ReviewedDate = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(comments))
        {
            transaction.AuditorNotes = comments;
        }

        await _context.SaveChangesAsync();

        // Log the action
        await _auditTrail.LogActionAsync(new AuditLog
        {
            Action = "REVIEW",
            EntityType = "TRANSACTION",
            EntityId = transactionId.ToString(),
            TransactionRecordId = transactionId,
            EngagementId = transaction.EngagementId,
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            OldValue = oldStatus,
            NewValue = "REVIEWED",
            Comments = comments
        });

        _logger.LogInformation($"Transaction {transactionId} reviewed by {userName}");
        return transaction;
    }

    /// <summary>
    /// Approve a transaction for claim submission.
    /// </summary>
    public async Task<TransactionRecord> ApproveTransactionAsync(
        long transactionId,
        string userId,
        string userName,
        string userRole,
        string? claimSchedule,
        decimal? claimAmount,
        string? comments)
    {
        var transaction = await _context.TransactionRecords.FindAsync(transactionId);
        if (transaction == null)
            throw new KeyNotFoundException($"Transaction {transactionId} not found");

        if (transaction.IsLocked)
            throw new InvalidOperationException("Cannot approve locked transaction");

        if (transaction.Status != "REVIEWED")
            throw new InvalidOperationException("Transaction must be reviewed before approval");

        // Segregation of duties: reviewer and approver should be different
        if (transaction.ReviewedByUserId == userId)
            _logger.LogWarning($"Same user reviewing and approving transaction {transactionId}");

        var oldStatus = transaction.Status;
        transaction.Status = "APPROVED";
        transaction.ApprovedByUserId = userId;
        transaction.ApprovedByUserName = userName;
        transaction.ApprovedDate = DateTime.UtcNow;
        transaction.ClaimSchedule = claimSchedule;
        transaction.ClaimAmount = claimAmount ?? transaction.TotalTaxAmount;

        await _context.SaveChangesAsync();

        // Log the action
        await _auditTrail.LogActionAsync(new AuditLog
        {
            Action = "APPROVE",
            EntityType = "TRANSACTION",
            EntityId = transactionId.ToString(),
            TransactionRecordId = transactionId,
            EngagementId = transaction.EngagementId,
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            OldValue = oldStatus,
            NewValue = $"APPROVED - Schedule {claimSchedule} - ${claimAmount}",
            Comments = comments
        });

        _logger.LogInformation($"Transaction {transactionId} approved by {userName} for ${claimAmount}");
        return transaction;
    }

    /// <summary>
    /// Reject a flagged transaction.
    /// </summary>
    public async Task<TransactionRecord> RejectTransactionAsync(
        long transactionId,
        string userId,
        string userName,
        string userRole,
        string reason)
    {
        var transaction = await _context.TransactionRecords.FindAsync(transactionId);
        if (transaction == null)
            throw new KeyNotFoundException($"Transaction {transactionId} not found");

        if (transaction.IsLocked)
            throw new InvalidOperationException("Cannot reject locked transaction");

        var oldStatus = transaction.Status;
        transaction.Status = "REJECTED";
        transaction.RejectionReason = reason;
        transaction.ReviewedByUserId = userId;
        transaction.ReviewedByUserName = userName;
        transaction.ReviewedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the action
        await _auditTrail.LogActionAsync(new AuditLog
        {
            Action = "REJECT",
            EntityType = "TRANSACTION",
            EntityId = transactionId.ToString(),
            TransactionRecordId = transactionId,
            EngagementId = transaction.EngagementId,
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            OldValue = oldStatus,
            NewValue = "REJECTED",
            Comments = reason
        });

        _logger.LogInformation($"Transaction {transactionId} rejected by {userName}: {reason}");
        return transaction;
    }

    /// <summary>
    /// Mark transactions as claimed (submitted to IRS).
    /// </summary>
    public async Task<int> MarkAsClaimedAsync(
        List<long> transactionIds,
        string userId,
        string userName,
        string userRole,
        string taxPeriod)
    {
        var transactions = await _context.TransactionRecords
            .Where(t => transactionIds.Contains(t.RecordID))
            .ToListAsync();

        int count = 0;
        foreach (var transaction in transactions)
        {
            if (transaction.Status != "APPROVED")
                continue;

            if (transaction.IsLocked)
                continue;

            transaction.Status = "CLAIMED";
            transaction.TaxPeriod = taxPeriod;
            count++;

            await _auditTrail.LogActionAsync(new AuditLog
            {
                Action = "CLAIM_SUBMITTED",
                EntityType = "TRANSACTION",
                EntityId = transaction.RecordID.ToString(),
                TransactionRecordId = transaction.RecordID,
                EngagementId = transaction.EngagementId,
                UserId = userId,
                UserName = userName,
                UserRole = userRole,
                NewValue = $"CLAIMED - {taxPeriod}",
                Comments = $"Submitted to IRS for tax period {taxPeriod}"
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"{count} transactions marked as claimed by {userName}");
        return count;
    }

    /// <summary>
    /// Get workflow statistics for an engagement.
    /// </summary>
    public async Task<Dictionary<string, int>> GetWorkflowStatsAsync(int engagementId)
    {
        var stats = await _context.TransactionRecords
            .Where(t => t.EngagementId == engagementId && t.AnomalyScore > 0.5f)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        return stats;
    }
}
