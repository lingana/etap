using ExciseTaxAudit.API.Models;
using Microsoft.Extensions.Logging;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for generating synthetic fuel transaction data matching actual Fuelman data format.
/// IMPORTANT: Schema/columns must match business-provided CSV exactly. Do not add, remove, or rename columns.
/// Reference: sample-transactions.csv provided by business
/// </summary>
public class SyntheticDataService
{
    private readonly ILogger<SyntheticDataService> _logger;
    private static List<TransactionRecord>? _cachedRecords;

    public SyntheticDataService(ILogger<SyntheticDataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate synthetic fuel transaction records based on realistic industry data with PII masking.
    /// </summary>
    public List<TransactionRecord> GenerateSyntheticData(int recordCount = 500)
    {
        if (_cachedRecords != null && _cachedRecords.Count > 0)
        {
            _logger.LogInformation($"Returning cached synthetic data: {_cachedRecords.Count} records");
            return _cachedRecords;
        }

        _logger.LogInformation($"Generating {recordCount} synthetic fuel transaction records...");
        var records = new List<TransactionRecord>();
        var random = new Random(42); // Seed for reproducibility

        // Realistic data from multiple fuel card providers and fleet operators
        var branches = new[] { "FNL", "LAX", "SEA", "DFW", "HOU", "ATL", "CHI", "MIA", "BOS", "DEN", "PHX", "SFO", "JFK", "ORD" };
        var departments = new[] { "77FNL", "88LAX", "99SEA", "11DFW", "22HOU", "33ATL", "44CHI", "55MIA", "66BOS", "77DEN" };
        var entities = new[] { "S2065", "S2066", "S2067", "S2068", "S2069", "S2070", "S2071", "S2072", "S2073", "S2074" };
        var cardBrands = new[] { "FUELMAN MASTERCARD", "FUELMAN VISA", "SHELL FLEET CARD", "CHEVRON CARD", "COMDATA CARD", "SPEEDWAY CARD" };
        var fuelPlatforms = new[] { "CHDLZ", "FLMN1", "FLMN2", "PDCRD", "SHELL", "CHEVRON", "BP", "EXXON" };
        var fuelTypes = new[] { "DIESEL", "UNLEADED", "PREMIUM", "E85", "PREMIUM DIESEL", "LOW SULFUR DIESEL" };
        var merchantNames = new[] { "ONE9 EZ TRIP", "PILOT FLYING J", "LOVES TRAVEL STOPS", "SHELL FUEL", "CHEVRON STATION", "BP EXPRESS", "SPEEDWAY FUEL", "CIRCLE K", "SUNOCO" };
        var cities = new[] { "AVENAL", "BAKERSFIELD", "STOCKTON", "SACRAMENTO", "LOS ANGELES", "SAN DIEGO", "FRESNO", "KERN", "OAKLAND", "TRACY", "MODESTO", "VISALIA" };
        var states = new[] { "CA", "TX", "NV", "AZ", "OR", "WA", "CO", "NM", "UT" };
        var assetDescriptions = new[] { "TRACTOR(SLEEP)", "TRACTOR(DAY)", "BOX TRUCK", "FLATBED", "TANKER", "REFRIGERATED", "STRAIGHT TRUCK", "PICKUP TRUCK" };
        var equipmentTypes = new[] { "Prime Mover", "Trailer", "Auxiliary Equipment", "PTO Equipment", "Other Auxiliary Equipment" };

        for (int i = 1; i <= recordCount; i++)
        {
            var transactionDate = DateTime.UtcNow.AddDays(-random.Next(0, 730)); // Last 2 years
            var quantity = Math.Round((decimal)(random.NextDouble() * 150 + 20), 1); // 20-170 gallons
            var pricePerUnit = Math.Round((decimal)(random.NextDouble() * 1.50 + 3.50), 4); // $3.50-$5.00
            var netCost = Math.Round(quantity * pricePerUnit, 2);
            var state = states[random.Next(states.Length)];

            // Add realistic anomalies (15% of records)
            bool isAnomaly = random.Next(100) < 15;
            if (isAnomaly)
            {
                switch (random.Next(3))
                {
                    case 0: // Price spike anomaly
                        pricePerUnit = Math.Round((decimal)(random.NextDouble() * 2.00 + 5.50), 4);
                        netCost = Math.Round(quantity * pricePerUnit, 2);
                        break;
                    case 1: // Unusual quantity
                        quantity = (decimal)(random.NextDouble() * 300 + 200);
                        netCost = Math.Round(quantity * pricePerUnit, 2);
                        break;
                    case 2: // Zero tax - discrepancy
                        // Tax will be zero to simulate missing tax or error
                        break;
                }
            }

            // Calculate tax based on state
            var taxRate = GetStateTaxRate(state);
            var totalTaxAmount = random.Next(100) < 20 ? 0 : Math.Round(netCost * (taxRate / 100), 2);
            var grossCost = netCost;

            // Mask PII: Employee name masked, Card number masked
            var cardLast4 = random.Next(1000, 9999).ToString();
            var employeeInitials = GenerateInitials(random);
            var cardholderMasked = employeeInitials + "*" + cardLast4;

            var record = new TransactionRecord
            {
                RecordID = 600000 + i,
                Branch = branches[random.Next(branches.Length)],
                Department = departments[random.Next(departments.Length)],
                Entity = entities[random.Next(entities.Length)],
                TransactionNumber = (random.Next(100000, 999999)).ToString("D6"),
                BillingCode = new[] { "F", "T", "O", "P", "A", "B" }[random.Next(6)],
                CardNumberMask = "****" + cardLast4, // PII MASKED: Only last 4 digits
                FuelPlatform = fuelPlatforms[random.Next(fuelPlatforms.Length)],
                CustomerID = "CUST" + random.Next(10000, 99999), // Masked customer ID
                FuelType = fuelTypes[random.Next(fuelTypes.Length)],
                Discrepancies = random.Next(100) < 5 ? "QUANTITY_VARIANCE" : "",
                EmployeeID = random.Next(100000, 999999).ToString("D6"), // ID only, no name
                EmployeeType = new[] { "REGULAR", "CONTRACT", "SEASONAL", "DRIVER", "OPERATOR" }[random.Next(5)],
                TransactionDate = transactionDate,
                MerchantName = merchantNames[random.Next(merchantNames.Length)] + " " + random.Next(1000, 9999),
                MerchantCity = cities[random.Next(cities.Length)],
                MerchantState = state,
                PADDRegion = GetPADDRegion(state),
                AssetNumber = random.Next(10000, 99999).ToString(), // Asset ID only
                AssetDescription = assetDescriptions[random.Next(assetDescriptions.Length)],
                Odometer = random.Next(100000, 900000),
                ProductDescription = "DIESEL #2 ULTRA-LOW SULFUR CARB WIT",
                ProductType = "DIESEL",
                Quantity = quantity,
                PricePerUnit = pricePerUnit,
                NetCost = netCost,
                GrossCost = grossCost,
                UOM = "GAL",
                PostedDate = transactionDate.AddDays(random.Next(0, 2)),
                Month = transactionDate.Month,
                Currency = "USD",
                CardTypeFlag = new[] { "E", "C", "P", "D" }[random.Next(4)],
                CardholderName = cardholderMasked, // PII MASKED: Initials + Card Last 4
                TripDispatchQuantity = 0,
                ReportingLevel = new[] { "F", "S", "D", "R" }[random.Next(4)],
                MCC = "CL191",
                MerchantCode = random.Next(10000, 99999) + " " + new[] { "S. LASSEN AVE.", "N. MAIN ST.", "E. HIGHWAY", "W. ROUTE", "CENTRAL BLVD" }[random.Next(5)],
                MerchantAddress1 = "", // Address not stored for privacy
                MerchantAddress2 = random.Next(90000, 99999) + "-0000",
                MerchantPostalCode = "PM" + random.Next(100, 999).ToString("D3"),
                ChainCode = random.Next(0, 20),
                PSItemID = "0",
                CrossBorderTransaction = "NO",
                CrossBorderTransactionAmount = netCost,
                TotalAmountDue = 0,
                TotalDiscountAmount = 0,
                TotalTaxAmount = totalTaxAmount,
                TransactionFee = Math.Round((decimal)(random.NextDouble() * 3), 2),
                FuelTransactionID = 3000000 + i,
                FuelTransactionDetailID = 3800000 + i,
                BranchDescription = branches[random.Next(branches.Length)] + " LOGISTICS",
                OriginalBranch = branches[random.Next(branches.Length)],
                OriginalBranchDescription = "REGIONAL OPERATIONS",
                ReviewedBy = random.Next(2) == 0 ? GenerateInitials(random) : null, // Initials only
                ReviewedDate = random.Next(2) == 0 ? transactionDate.AddDays(random.Next(1, 3)) : null,
                ReviewedComments = random.Next(2) == 0 ? GetRandomReviewComment(random) : null,
                PaymentProcessedBy = random.Next(2) == 0 ? GenerateInitials(random) : null, // Initials only
                PaymentProcessedDate = random.Next(2) == 0 ? transactionDate.AddDays(random.Next(10, 20)) : null,
                TransactionTimezone = new[] { "CST", "PST", "MST", "EST" }[random.Next(4)],
                LastModifiedBy = GenerateInitials(random),
                LastModifiedDate = transactionDate.AddDays(random.Next(0, 2)),
                FileName = transactionDate.ToString("yyyyMM"),
                FullCardNumber = "", // PII MASKED: Not stored
                K_EquipmentDescription = equipmentTypes[random.Next(equipmentTypes.Length)],
                K_SafeHarborPercentage = 0.1m,
                IngestedAt = DateTime.UtcNow,
                IsReviewed = false,
                AnomalyScore = 0, // Will be calculated by detection service
                AnomalyReason = ""
            };

            records.Add(record);
        }

        // For demo purposes: Mark 20% of anomalies as APPROVED with claim amounts
        var approvedCount = 0;
        var reviewedCount = 0;
        foreach (var record in records.Where(r => r.AnomalyScore > 0.5f).OrderByDescending(r => r.AnomalyScore))
        {
            if (approvedCount < recordCount * 0.05) // 5% approved
            {
                record.Status = "APPROVED";
                record.ReviewedByUserId = "1";
                record.ReviewedByUserName = "Naveen L";
                record.ReviewedDate = record.TransactionDate.AddDays(random.Next(1, 5));
                record.ApprovedByUserId = "2";
                record.ApprovedByUserName = "Sarah Johnson";
                record.ApprovedDate = record.TransactionDate.AddDays(random.Next(6, 10));
                record.ClaimSchedule = new[] { "Schedule 1", "Schedule 2", "Schedule 3", "Schedule 6" }[random.Next(4)];
                record.TaxPeriod = $"Q{(record.TransactionDate.Month - 1) / 3 + 1} {record.TransactionDate.Year}";
                
                // Calculate claim amount as difference between expected and actual tax
                var expectedTax = record.Quantity * GetStateTaxRate(record.MerchantState ?? "CA") / 100m;
                record.ClaimAmount = Math.Max(0, record.TotalTaxAmount - expectedTax);
                
                approvedCount++;
            }
            else if (reviewedCount < recordCount * 0.08) // Additional 8% reviewed but not yet approved
            {
                record.Status = "REVIEWED";
                record.ReviewedByUserId = "1";
                record.ReviewedByUserName = "Naveen L";
                record.ReviewedDate = record.TransactionDate.AddDays(random.Next(1, 5));
                reviewedCount++;
            }
        }

        _cachedRecords = records;
        _logger.LogInformation($"Successfully generated {records.Count} synthetic records ({approvedCount} approved, {reviewedCount} reviewed) with realistic data and PII masking");
        return records;
    }

    /// <summary>
    /// Get tax rate by state.
    /// </summary>
    private decimal GetStateTaxRate(string state)
    {
        return state switch
        {
            "CA" => 16.25m,
            "TX" => 20.0m,
            "NV" => 26.0m,
            "AZ" => 18.0m,
            "OR" => 30.0m,
            "WA" => 49.5m,
            "CO" => 22.0m,
            "NM" => 17.1m,
            "UT" => 24.5m,
            _ => 18.4m // Federal minimum
        };
    }

    /// <summary>
    /// Get PADD region by state.
    /// </summary>
    private string GetPADDRegion(string state)
    {
        return state switch
        {
            "CA" or "NV" or "OR" or "WA" => "PADD-V",
            "CO" or "UT" or "AZ" or "NM" => "PADD-IV",
            "TX" => "PADD-III",
            _ => "PADD-I"
        };
    }

    /// <summary>
    /// Generate realistic initials for PII masking (e.g., "JD", "AB").
    /// </summary>
    private string GenerateInitials(Random random)
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return $"{letters[random.Next(letters.Length)]}{letters[random.Next(letters.Length)]}";
    }

    /// <summary>
    /// Generate realistic review comments.
    /// </summary>
    private string GetRandomReviewComment(Random random)
    {
        var comments = new[]
        {
            "EXPORTED FOR PAYMENT",
            "APPROVED - STANDARD RATE",
            "FLAGGED FOR FURTHER REVIEW",
            "QUANTITY VERIFIED",
            "PRICE WITHIN RANGE",
            "PROCESSED SUCCESSFULLY",
            "PENDING VERIFICATION",
            "APPROVED WITH ADJUSTMENT"
        };
        return comments[random.Next(comments.Length)];
    }

    /// <summary>
    /// Clear cached synthetic data.
    /// </summary>
    public void ClearCache()
    {
        _cachedRecords = null;
        _logger.LogInformation("Synthetic data cache cleared");
    }
}

