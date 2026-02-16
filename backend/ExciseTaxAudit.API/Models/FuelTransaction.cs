namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Simplified fuel transaction model for Azure ML inference
/// Used for real-time anomaly detection scoring
/// </summary>
public class FuelTransaction
{
    public string TransactionId { get; set; }
    public string MerchantName { get; set; }
    public string MerchantState { get; set; }
    public string FuelType { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double StateAveragePrice { get; set; }
    public int MerchantCount { get; set; }
    public int DayOfWeek { get; set; }
    public int MonthOfYear { get; set; }
    public int SupplierDaysActive { get; set; }
    public DateTime TransactionDate { get; set; }
}
