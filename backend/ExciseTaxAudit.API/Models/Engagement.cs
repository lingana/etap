namespace ExciseTaxAudit.API.Models
{
    public enum EngagementStatusEnum
    {
        Planning = 1,
        InProgress = 2,
        UnderReview = 3,
        Completed = 4,
        Archived = 5
    }

    public enum EngagementTypeEnum
    {
        FullAudit = 1,
        LimitedScope = 2,
        Review = 3,
        Consultation = 4
    }

    public class Engagement
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string? ExternalEngagementId { get; set; }
        public string EngagementName { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public DateTime FiscalYearStart { get; set; }
        public DateTime FiscalYearEnd { get; set; }
        public EngagementTypeEnum EngagementType { get; set; }
        public EngagementStatusEnum Status { get; set; } = EngagementStatusEnum.Planning;
        public string Description { get; set; } = string.Empty;
        public int? LeadAuditorId { get; set; }
        public decimal BudgetedHours { get; set; }
        public decimal ActualHours { get; set; }
        public decimal EstimatedPotentialRecovery { get; set; }
        public decimal ActualRecovery { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Client? Client { get; set; }
        public User? LeadAuditor { get; set; }
        public ICollection<AuditCase> AuditCases { get; set; } = new List<AuditCase>();
    }
}
