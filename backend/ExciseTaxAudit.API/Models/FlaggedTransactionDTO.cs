namespace ExciseTaxAudit.API.Models;

/// <summary>
/// DTO for returning flagged transactions with anomaly details and explanations.
/// </summary>
public class FlaggedTransactionDTO
{
    public long RecordID { get; set; }
    public string? TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantState { get; set; }
    public string? FuelType { get; set; }
    public decimal Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public decimal NetCost { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public float AnomalyScore { get; set; }
    public string? AnomalyReason { get; set; }
    public decimal? ExpectedTaxAmount { get; set; }
    public decimal? TaxDifference { get; set; }
    public string? PredictedClaimType { get; set; } // "OVER" or "UNDER"
    public float? Confidence { get; set; }
    public string? ExplanationText { get; set; }
    public bool IsReviewed { get; set; }
    public string? AuditorNotes { get; set; }
    
    // Workflow status fields
    public string? Status { get; set; } // FLAGGED, REVIEWED, APPROVED, REJECTED, CLAIMED
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public decimal? ClaimAmount { get; set; }
}
