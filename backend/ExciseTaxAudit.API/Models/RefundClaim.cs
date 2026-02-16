namespace ExciseTaxAudit.API.Models;

public enum ClaimStatus
{
    Draft,           // Being prepared
    ReadyToFile,     // Ready for submission
    Submitted,       // Filed with IRS
    UnderReview,     // IRS reviewing
    Approved,        // Approved for payment
    Paid,            // Payment received
    PartiallyPaid,   // Partial payment received
    Rejected,        // Claim denied
    Appealed         // Under appeal
}

public enum RefundType
{
    Overpayment,     // Paid too much tax
    Exemption,       // Qualified for exemption
    Credit,          // Tax credit applies
    RateError,       // Wrong rate applied
    Other            // Other refund type
}

public class RefundClaim
{
    public int Id { get; set; }
    
    // Relationships
    public int EngagementId { get; set; }
    public Engagement? Engagement { get; set; }
    
    public int? AuditCaseId { get; set; }
    public AuditCase? AuditCase { get; set; }
    
    // Claim Details
    public string ClaimNumber { get; set; } = string.Empty; // Auto-generated: RC-YYYY-XXXXX
    public RefundType RefundType { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    
    // Tax Information
    public string TaxType { get; set; } = string.Empty; // Fuel, Alcohol, Tobacco, etc.
    public string TaxFormType { get; set; } = string.Empty; // Form 8849, 720, etc.
    
    // Financial Details
    public decimal ClaimedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "USD";
    
    // Period Information
    public DateTime TaxPeriodStart { get; set; }
    public DateTime TaxPeriodEnd { get; set; }
    public int TaxYear { get; set; }
    public string TaxQuarter { get; set; } = string.Empty; // Q1, Q2, Q3, Q4
    
    // Transaction Details
    public int TransactionCount { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    
    // Documentation
    public string Justification { get; set; } = string.Empty;
    public string IRSCitations { get; set; } = string.Empty; // JSON array of citations
    public string SupportingDocuments { get; set; } = string.Empty; // JSON array of file paths
    
    // IRS Form 8849 Fields
    public string? EIN { get; set; } // Employer Identification Number
    public string? NameOfClaimant { get; set; }
    public string? ClaimantAddress { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    
    // Workflow Tracking
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; } = DateTime.UtcNow;
    
    // User Tracking
    public string CreatedBy { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public string? ReviewedBy { get; set; }
    
    // IRS Response
    public string? IRSResponseNotes { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ExpectedPaymentDate { get; set; }
    
    // Internal Notes
    public string InternalNotes { get; set; } = string.Empty;
    public int Priority { get; set; } = 3; // 1-5, where 1 is highest priority
    
    // AI Analysis
    public decimal ConfidenceScore { get; set; } // 0-1, AI confidence in claim validity
    public string? AIRecommendation { get; set; }
    public string? AIReasoning { get; set; }
    
    // File Attachments
    public string? Form8849Path { get; set; } // Generated PDF path
    public string? SupportingEvidencePath { get; set; }
}

public class RefundClaimSummary
{
    public int TotalClaims { get; set; }
    public int DraftClaims { get; set; }
    public int SubmittedClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int RejectedClaims { get; set; }
    
    public decimal TotalClaimedAmount { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalPendingAmount { get; set; }
    
    public Dictionary<string, decimal> ClaimsByTaxType { get; set; } = new();
    public Dictionary<RefundType, int> ClaimsByRefundType { get; set; } = new();
    public Dictionary<ClaimStatus, int> ClaimsByStatus { get; set; } = new();
}
