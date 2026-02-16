namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Represents a normalized fuel transaction record for audit analysis.
/// Schema matches business-provided CSV exactly - DO NOT change column order or names.
/// Includes compliance fields for IRS audit trail and approval workflow.
/// </summary>
public class TransactionRecord
{
    public long RecordID { get; set; }

    // Engagement scoping (nullable for backward compatibility)
    public int? EngagementId { get; set; }
    public string? Branch { get; set; }
    public string? Department { get; set; }
    public string? Entity { get; set; }
    public string? TransactionNumber { get; set; }
    public string? BillingCode { get; set; }
    public string? CardNumberMask { get; set; }
    public string? FuelPlatform { get; set; }
    public string? CustomerID { get; set; }
    public string? FuelType { get; set; }
    public string? Discrepancies { get; set; }
    public string? EmployeeID { get; set; }
    public string? EmployeeType { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantCity { get; set; }
    public string? MerchantState { get; set; }
    public string? PADDRegion { get; set; }
    public string? AssetNumber { get; set; }
    public string? AssetDescription { get; set; }
    public long? Odometer { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductType { get; set; }
    public decimal Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal NetCost { get; set; }
    public decimal GrossCost { get; set; }
    public string? UOM { get; set; }
    public DateTime PostedDate { get; set; }
    public int Month { get; set; }
    public string? Currency { get; set; }
    public string? CardTypeFlag { get; set; }
    public string? CardholderName { get; set; }
    public decimal? TripDispatchQuantity { get; set; }
    public string? ReportingLevel { get; set; }
    public string? MCC { get; set; }
    public string? MerchantCode { get; set; }
    public string? MerchantAddress1 { get; set; }
    public string? MerchantAddress2 { get; set; }
    public string? MerchantPostalCode { get; set; }
    public int? ChainCode { get; set; }
    public string? PSItemID { get; set; }
    public string? CrossBorderTransaction { get; set; }
    public decimal CrossBorderTransactionAmount { get; set; }
    public decimal TotalAmountDue { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TransactionFee { get; set; }
    public long? FuelTransactionID { get; set; }
    public long? FuelTransactionDetailID { get; set; }
    public string? BranchDescription { get; set; }
    public string? OriginalBranch { get; set; }
    public string? OriginalBranchDescription { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewedComments { get; set; }
    public string? PaymentProcessedBy { get; set; }
    public DateTime? PaymentProcessedDate { get; set; }
    public string? TransactionTimezone { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? FileName { get; set; }
    public string? FullCardNumber { get; set; }
    public string? K_EquipmentDescription { get; set; }
    public decimal? K_SafeHarborPercentage { get; set; }
    public DateTime IngestedAt { get; set; }
    public string? SourceFileUrl { get; set; }
    
    // Audit/ML fields
    public string? Label_OverUnder { get; set; }
    public decimal? Label_AdjustmentAmount { get; set; }
    public float? AnomalyScore { get; set; }
    public string? AnomalyReason { get; set; }
    public float? Confidence { get; set; }
    public string? GenAIExplanation { get; set; }
    public bool IsReviewed { get; set; }
    public string? AuditorNotes { get; set; }
    
    // Compliance & Workflow fields
    public string Status { get; set; } = "FLAGGED"; // FLAGGED, REVIEWED, APPROVED, REJECTED, CLAIMED
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? ClaimSchedule { get; set; } // IRS Form 8849 Schedule (e.g., "2a", "2b", "6")
    public string? TaxPeriod { get; set; } // e.g., "Q1 2022", "Q2 2023"
    public decimal? ClaimAmount { get; set; } // Calculated refund amount
    public bool IsLocked { get; set; } = false; // Prevent modification after finalization
    public string? AttachmentUrls { get; set; } // JSON array of document URLs
}
