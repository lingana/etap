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

    // Per-gallon tax rates (Federal + State combined) — MUST match TaxRatePlugin rates
    // so the AI agent's variance calculation produces correct APPROVE/REJECT recommendations.
    // Source: TaxRatePlugin.cs
    private readonly Dictionary<string, Dictionary<string, decimal>> _perGallonTaxRates = new()
    {
        // Diesel: Federal $0.244 + state rate
        ["DIESEL"] = new()
        {
            { "CA", 0.244m + 0.133m }, // $0.377/gal
            { "TX", 0.244m + 0.20m },  // $0.444/gal
            { "NY", 0.244m + 0.169m }, // $0.413/gal
            { "IL", 0.244m + 0.219m }, // $0.463/gal
            { "FL", 0.244m + 0.191m }, // $0.435/gal
            { "PA", 0.244m + 0.255m }, // $0.499/gal
            { "OH", 0.244m + 0.28m }   // $0.524/gal
        },
        // Gasoline: Federal $0.184 + state rate
        ["GASOLINE"] = new()
        {
            { "CA", 0.184m + 0.539m }, // $0.723/gal
            { "TX", 0.184m + 0.20m },  // $0.384/gal
            { "NY", 0.184m + 0.459m }, // $0.643/gal
            { "IL", 0.184m + 0.392m }, // $0.576/gal
            { "FL", 0.184m + 0.196m }, // $0.380/gal
            { "PA", 0.184m + 0.576m }, // $0.760/gal
            { "OH", 0.184m + 0.385m }  // $0.569/gal
        }
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
        // - APPROVE: Clear overpayment exceeding safe harbor (>10%) — refund opportunity
        // - NEEDS_MANUAL_REVIEW: Significant issues, borderline cases
        // - REJECT: Calculation errors, extreme variances
        
        if (anomalyType < 0.40) // 40% normal transactions (for baseline/contrast)
        {
            quantity = (decimal)(10 + _random.NextDouble() * 30); // 10-40 gallons
            anomalyCategory = "NORMAL";
        }
        else if (anomalyType < 0.58) // 18% moderate overpayment (APPROVE candidates)
        {
            // 20-29% overpayment with large quantities → recovery > $20 → deterministic APPROVE (Path 2)
            // Worst case: 300 gal × $0.377/gal (CA Diesel) × 20% = $22.62 recovery ✓
            quantity = (decimal)(300 + _random.NextDouble() * 300); // 300-600 gallons
            anomalyCategory = "MODERATE_OVERPAY";
        }
        else if (anomalyType < 0.70) // 12% high overpayment (APPROVE - strong refund case)
        {
            // 20-29% overpayment with very large quantities → bigger recovery → deterministic APPROVE
            // Worst case: 500 gal × $0.377/gal (CA Diesel) × 20% = $37.70 recovery ✓
            quantity = (decimal)(500 + _random.NextDouble() * 500); // 500-1000 gallons
            anomalyCategory = "HIGH_OVERPAY";
        }
        else if (anomalyType < 0.76) // 6% borderline underpayment (MANUAL_REVIEW)
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

        // Calculate correct tax using per-gallon rates (Federal + State)
        // This matches TaxRatePlugin.CalculateExpectedTax() which the AI agent uses
        var fuelCategory = fuelType.Contains("DIESEL") ? "DIESEL" : "GASOLINE";
        var perGallonRate = _perGallonTaxRates[fuelCategory][state];
        var correctTax = quantity * perGallonRate;
        
        // Calculate tax based on anomaly category to drive AI recommendations
        // AI agent approves when: potentialRecovery > $0 (actualTax > expectedTax) AND variance > 10%
        decimal totalTaxAmount;
        switch (anomalyCategory)
        {
            case "MODERATE_OVERPAY":
                // 120-129% of correct tax → 20-29% variance → risk=MEDIUM → APPROVE (Path 2)
                // At 300 gal × $0.377/gal min rate → correctTax ≥ $113, recovery ≥ $22.60
                totalTaxAmount = Math.Round(correctTax * (decimal)(1.20 + _random.NextDouble() * 0.09), 2);
                break;
            case "HIGH_OVERPAY":
                // 120-129% of correct tax → 20-29% variance → risk=MEDIUM, large qty → bigger recovery → APPROVE (Path 2)
                // At 500 gal × $0.377/gal × 20% → recovery ≥ $37.70
                totalTaxAmount = Math.Round(correctTax * (decimal)(1.20 + _random.NextDouble() * 0.09), 2);
                break;
            case "BORDERLINE_UNDERPAY":
                // 85-92% of correct (8-15% underpayment → MANUAL_REVIEW)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.85 + _random.NextDouble() * 0.07), 2);
                break;
            case "MAJOR_UNDERPAY":
                // 40-65% of correct (35-60% underpayment → REJECT, no recovery)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.40 + _random.NextDouble() * 0.25), 2);
                break;
            case "ZERO_TAX":
                // 0-10% of correct (clear error → REJECT)
                totalTaxAmount = Math.Round(correctTax * (decimal)(_random.NextDouble() * 0.10), 2);
                break;
            case "EXTREME_OVERPAY":
                // 200-300% of correct (unusual → MANUAL_REVIEW)
                totalTaxAmount = Math.Round(correctTax * (decimal)(2.0 + _random.NextDouble() * 1.0), 2);
                break;
            case "QUANTITY_SPIKE":
            case "PRICE_ANOMALY":
                // Normal tax but suspicious pattern → MANUAL_REVIEW
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.97 + _random.NextDouble() * 0.06), 2);
                break;
            default:
                // Normal: 96-104% of correct (within safe harbor, no flag)
                totalTaxAmount = Math.Round(correctTax * (decimal)(0.96 + _random.NextDouble() * 0.08), 2);
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
