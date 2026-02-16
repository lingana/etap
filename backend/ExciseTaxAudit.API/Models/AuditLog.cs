namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Audit log entry for tracking all user actions on transactions and engagements.
/// Provides complete audit trail for IRS compliance and internal controls.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? EngagementId { get; set; }
    public long? TransactionRecordId { get; set; }
    public string Action { get; set; } = ""; // UPLOAD, REVIEW, APPROVE, REJECT, MODIFY, EXPORT, LOCK, UNLOCK
    public string EntityType { get; set; } = ""; // TRANSACTION, ENGAGEMENT, USER
    public string? EntityId { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? OldValue { get; set; } // JSON of previous state
    public string? NewValue { get; set; } // JSON of new state
    public string? Comments { get; set; }
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Document attachment for supporting evidence on transactions.
/// Required for IRS substantiation of refund claims.
/// </summary>
public class TransactionAttachment
{
    public long Id { get; set; }
    public long TransactionRecordId { get; set; }
    public string FileName { get; set; } = "";
    public string FileUrl { get; set; } = "";
    public string FileType { get; set; } = ""; // PDF, XLSX, PNG, etc.
    public long FileSizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = "";
    public string UploadedByUserName { get; set; } = "";
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = ""; // INVOICE, RECEIPT, CONTRACT, CORRESPONDENCE, OTHER
    public string? Description { get; set; }
}

/// <summary>
/// Export history for tracking all report generations and downloads.
/// Required for maintaining audit trail of data access.
/// </summary>
public class ExportHistory
{
    public long Id { get; set; }
    public int EngagementId { get; set; }
    public string ExportType { get; set; } = ""; // FORM_8849, AUDIT_REPORT, TRANSACTION_DETAIL, SUMMARY
    public string FileUrl { get; set; } = "";
    public string ExportedByUserId { get; set; } = "";
    public string ExportedByUserName { get; set; } = "";
    public DateTime ExportedDate { get; set; } = DateTime.UtcNow;
    public int RecordCount { get; set; }
    public string? TaxPeriod { get; set; }
    public string? Parameters { get; set; } // JSON of export parameters
}
