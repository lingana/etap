namespace ExciseTaxAudit.API.Models
{
    public enum AuditStatus
    {
        Draft,
        DataLoaded,
        InReview,
        PendingApproval,
        Approved,
        Closed
    }

    public enum AuditType
    {
        TaxExemption,
        OverPayment,
        UnderPayment,
        Compliance,
        Comprehensive
    }

    public class AuditCase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public string State { get; set; }
        
        public int EngagementId { get; set; }
        
        public DateTime AuditPeriodStart { get; set; }
        public DateTime AuditPeriodEnd { get; set; }
        
        public AuditType AuditType { get; set; }
        public AuditStatus Status { get; set; } = AuditStatus.Draft;
        
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; }
        
        public int? AssignedToUserId { get; set; }
        public User AssignedToUser { get; set; }
        
        public int? ReviewedByUserId { get; set; }
        public User ReviewedByUser { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DataLoadedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        
        // Statistics
        public int TotalRecords { get; set; }
        public int FlaggedRecords { get; set; }
        public int ReviewedRecords { get; set; }
        public decimal PotentialRecovery { get; set; }
        
        public int ReviewProgress => TotalRecords > 0 ? (ReviewedRecords * 100) / TotalRecords : 0;
        public string StatusText => Status.ToString();

        // Navigation
        public Engagement Engagement { get; set; }
    }
}
