using ExciseTaxAudit.API.Models;
using System.Text;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Generates realistic demo data for fuel tax audits with varied anomaly patterns.
/// IMPORTANT: CSV header and column order must match business-provided schema exactly.
/// Reference: sample-transactions.csv provided by business
/// </summary>
public class DemoDataGenerator
{
    private readonly Random _random = new Random(12345); // Fixed seed for reproducibility

    private readonly string[] _merchants = new[]
    {
        "CHEVRON", "SHELL", "BP", "EXXON MOBIL", "PILOT FLYING J",
        "TA PETRO", "LOVES TRAVEL STOP", "SPEEDWAY", "MARATHON",
        "SUNOCO STATION", "ONE9 EZ TRIP 1277", "CIRCLE K"
    };

    private readonly string[] _states = new[] { "CA", "TX", "NY", "IL", "FL", "PA", "OH" };

    private readonly Dictionary<string, decimal> _stateTaxRates = new()
    {
        { "CA", 0.539m }, // $0.539 per gallon
        { "TX", 0.20m },
        { "NY", 0.444m },
        { "IL", 0.454m },
        { "FL", 0.361m },
        { "PA", 0.577m },
        { "OH", 0.385m }
    };

    private readonly string[] _fuelTypes = new[]
    {
        "UNLEADED GASOLINE",
        "PREMIUM UNLEADED",
        "DIESEL",
        "DIESEL #2 ULTRA-LOW SULFUR CARB WIT"
    };

    /// <summary>
    /// Generate CSV content with realistic fuel transaction data
    /// </summary>
    public string GenerateDemoCSV(int recordCount = 500)
    {
        var sb = new StringBuilder();
        
        // CSV Header - EXACT match with business CSV schema
        sb.AppendLine("RecordID,Branch,Department,Entity,Transaction Number,Billing Code,Card Number,Fuel Platform,Customer ID,Fuel Type,Discrepancies,Employee ID,Employee Type,Transaction Date,Merchant Name,Merchant City,Merchant State,PADD/Region,Asset Number,Asset Description,Odometer,Product Description,Product Type Description,Quantity,Price Per Unit,Net Cost,Gross Cost,UOM,Posted Date,Month,Currency,Card Type Flag,Cardholder Name,Trip & Dispatch Quantity,Reporting Level,MCC,Merchant Code,Merchant Address 1,Merchant Address 2,Merchant Postal Code,Chain Code,PS Item ID,Cross Border Transaction,Total Amount Due,Total Discount Amount,Total Tax Amount,Transaction Fee,Fuel Transaction ID,Fuel Transaction Detail ID,Branch Description,Original Branch,Original Branch Description,Reviewed By,Reviewed Date,Reviewed Comments,Payment Processed By,Payment Processed Date,Transaction Time Zone,Last Modified By,Last Modified Date,FileName,Full Card Number,K_Equipment Description,K_Safe Harbor Percentage");

        var startDate = new DateTime(2022, 1, 1);
        
        for (int i = 0; i < recordCount; i++)
        {
            var transaction = GenerateTransaction(i + 1000000, startDate.AddDays(_random.Next(0, 180)));
            sb.AppendLine(FormatTransactionAsCSV(transaction));
        }

        return sb.ToString();
    }

    private TransactionData GenerateTransaction(int recordId, DateTime transactionDate)
    {
        var merchant = _merchants[_random.Next(_merchants.Length)];
        var state = _states[_random.Next(_states.Length)];
        var fuelType = _fuelTypes[_random.Next(_fuelTypes.Length)];
        
        // Normal price ranges (per gallon)
        var basePricePerGallon = fuelType.Contains("DIESEL") 
            ? (decimal)(3.5 + _random.NextDouble() * 1.5) // $3.50-$5.00
            : (decimal)(2.8 + _random.NextDouble() * 1.2); // $2.80-$4.00

        // Quantity: Most normal (10-40 gallons), some outliers
        decimal quantity;
        var anomalyType = _random.NextDouble();
        var anomalyCategory = "";
        
        // Generate diverse scenarios for different AI agent recommendations:
        // - APPROVE: Clean overpayment within safe harbor (±10%)
        // - NEEDS_MANUAL_REVIEW: Significant issues, borderline cases
        // - REJECT: Calculation errors, extreme variances
        
        if (anomalyType < 0.55) // 55% normal transactions (for baseline/contrast)
        {
            quantity = (decimal)(10 + _random.NextDouble() * 30); // 10-40 gallons
            anomalyCategory = "NORMAL";
        }
        else if (anomalyType < 0.68) // 13% moderate overpayment (APPROVE candidates)
        {
            // 3-9% overpayment - within safe harbor, clear refund approval
            quantity = (decimal)(15 + _random.NextDouble() * 30);
            anomalyCategory = "MODERATE_OVERPAY";
        }
        else if (anomalyType < 0.76) // 8% high overpayment (APPROVE but flagged)
        {
            // 10-18% overpayment - exceeds safe harbor, but clear overpayment pattern
            quantity = (decimal)(20 + _random.NextDouble() * 35);
            anomalyCategory = "HIGH_OVERPAY";
        }
        else if (anomalyType < 0.82) // 6% borderline underpayment (MANUAL_REVIEW)
        {
            // 5-9% underpayment - within safe harbor but suspicious
            quantity = (decimal)(12 + _random.NextDouble() * 28);
            anomalyCategory = "BORDERLINE_UNDERPAY";
        }
        else if (anomalyType < 0.88) // 6% significant underpayment (REJECT candidates)
        {
            // 15-40% underpayment - clear calculation error
            quantity = (decimal)(18 + _random.NextDouble() * 32);
            anomalyCategory = "MAJOR_UNDERPAY";
        }
        else if (anomalyType < 0.92) // 4% zero/near-zero tax (REJECT - obvious error)
        {
            quantity = (decimal)(15 + _random.NextDouble() * 35);
            anomalyCategory = "ZERO_TAX";
        }
        else if (anomalyType < 0.96) // 4% extreme overpayment (MANUAL_REVIEW - unusual)
        {
            // 50-100% overpayment - requires investigation
            quantity = (decimal)(15 + _random.NextDouble() * 30);
            anomalyCategory = "EXTREME_OVERPAY";
        }
        else // 4% suspicious patterns (MANUAL_REVIEW)
        {
            // Quantity spike or price anomaly - fraud indicator
            if (_random.NextDouble() < 0.5)
            {
                quantity = (decimal)(60 + _random.NextDouble() * 100); // Unusual quantity
                anomalyCategory = "QUANTITY_SPIKE";
            }
            else
            {
                basePricePerGallon *= (decimal)(0.4 + _random.NextDouble() * 0.3); // Suspiciously low price
                quantity = (decimal)(12 + _random.NextDouble() * 28);
                anomalyCategory = "PRICE_ANOMALY";
            }
        }

        var pricePerUnit = Math.Round(basePricePerGallon, 6);
        var netCost = Math.Round(quantity * pricePerUnit, 2);
        var taxRate = _stateTaxRates[state];
        var correctTax = quantity * taxRate;
        
        // Calculate tax based on anomaly category to drive AI recommendations
        decimal totalTaxAmount;
        switch (anomalyCategory)
        {
            case "MODERATE_OVERPAY":
                // 103-109% of correct (within safe harbor, APPROVE for refund)
                totalTaxAmount = Math.Round(correctTax * (decimal)(1.03 + _random.NextDouble() * 0.06), 2);
                break;
            case "HIGH_OVERPAY":
                // 110-118% of correct (exceeds safe harbor, still APPROVE)
                totalTaxAmount = Math.Round(correctTax * (decimal)(1.10 + _random.NextDouble() * 0.08), 2);
                break;
            case "BORDERLINE_UNDERPAY":
                // 91-95% of correct (within safe harbor, but suspicious - MANUAL_REVIEW)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.91 + _random.NextDouble() * 0.04), 2);
                break;
            case "MAJOR_UNDERPAY":
                // 60-85% of correct (major error - likely REJECT)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.60 + _random.NextDouble() * 0.25), 2);
                break;
            case "ZERO_TAX":
                // 0-15% of correct (clear error - REJECT)
                totalTaxAmount = Math.Round(correctTax * (decimal)(_random.NextDouble() * 0.15), 2);
                break;
            case "EXTREME_OVERPAY":
                // 150-200% of correct (unusual - MANUAL_REVIEW)
                totalTaxAmount = Math.Round(correctTax * (decimal)(1.50 + _random.NextDouble() * 0.50), 2);
                break;
            case "QUANTITY_SPIKE":
            case "PRICE_ANOMALY":
                // Normal tax but suspicious pattern - MANUAL_REVIEW
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.98 + _random.NextDouble() * 0.04), 2);
                break;
            default:
                // Normal: 98-102% of correct (baseline)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.98 + _random.NextDouble() * 0.04), 2);
                break;
        }

        var grossCost = netCost + totalTaxAmount;

        return new TransactionData
        {
            RecordID = recordId,
            TransactionNumber = $"{recordId:D9}",
            TransactionDate = transactionDate,
            MerchantName = merchant,
            MerchantState = state,
            FuelType = fuelType,
            Quantity = Math.Round(quantity, 4),
            PricePerUnit = pricePerUnit,
            NetCost = netCost,
            GrossCost = grossCost,
            TotalTaxAmount = totalTaxAmount,
            AssetNumber = $"VH{_random.Next(1000, 9999)}",
            Odometer = _random.Next(50000, 200000)
        };
    }

    private string FormatTransactionAsCSV(TransactionData t)
    {
        // Format CSV row to match EXACT business schema
        return $"{t.RecordID},MAIN,FLEET,ACME CORP,{t.TransactionNumber},BC{_random.Next(100, 999)},****{_random.Next(1000, 9999)},WEX,CUST{_random.Next(100, 999)},{t.FuelType},,EMP{_random.Next(100, 999)},Driver,{t.TransactionDate:yyyy-MM-dd},{t.MerchantName},Various,{t.MerchantState},PADD{_random.Next(1, 5)},{t.AssetNumber},Fleet Vehicle,{t.Odometer},Fuel Purchase,Fuel,{t.Quantity},{t.PricePerUnit},{t.NetCost},{t.GrossCost},GAL,{t.TransactionDate:yyyy-MM-dd},{t.TransactionDate.Month},USD,Fleet,John Doe,{t.Quantity},Fleet,5541,M{_random.Next(1000, 9999)},123 Main St,,{_random.Next(10000, 99999)},{_random.Next(1000, 9999)},0,NO,{t.GrossCost},0,{t.TotalTaxAmount},0.50,{_random.Next(100000, 999999)},{_random.Next(100000, 999999)},Main Branch,MAIN,Main Branch,,,,,,EST,,{DateTime.Now:yyyy-MM-dd},demo-data.csv,,Standard Vehicle,0";
    }

    private class TransactionData
    {
        public long RecordID { get; set; }
        public string TransactionNumber { get; set; } = "";
        public DateTime TransactionDate { get; set; }
        public string MerchantName { get; set; } = "";
        public string MerchantState { get; set; } = "";
        public string FuelType { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal NetCost { get; set; }
        public decimal GrossCost { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public string AssetNumber { get; set; } = "";
        public long Odometer { get; set; }
    }
}
