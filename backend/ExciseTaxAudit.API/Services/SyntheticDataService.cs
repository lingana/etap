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

            // Create mix of regular, large, and MEGA transactions
            // 5% MEGA fleet operations (1500-2500 gallons) - TARGET THESE FOR REFUNDS!
            // 15% Large fleet refueling (500-1000 gallons) - ALSO TARGET FOR REFUNDS
            // 80% Normal transactions (20-170 gallons)
            var transactionType = random.Next(100);
            bool isMegaTransaction = transactionType < 5;  // 5% mega
            bool isLargeTransaction = transactionType >= 5 && transactionType < 20; // 15% large

            var quantity = isMegaTransaction
                ? Math.Round((decimal)(random.NextDouble() * 1000 + 1500), 1)  // 1500-2500 gallons (mega fleet)
                : isLargeTransaction 
                    ? Math.Round((decimal)(random.NextDouble() * 500 + 500), 1)  // 500-1000 gallons (large fleet)
                    : Math.Round((decimal)(random.NextDouble() * 150 + 20), 1);   // 20-170 gallons (normal)

            var pricePerUnit = Math.Round((decimal)(random.NextDouble() * 1.50 + 3.50), 4); // $3.50-$5.00
            var netCost = Math.Round(quantity * pricePerUnit, 2);
            var state = states[random.Next(states.Length)];

            // Calculate tax based on state (declare before anomaly logic)
            var taxRate = GetStateTaxRate(state);
            var totalTaxAmount = Math.Round(netCost * (taxRate / 100), 2);
            var grossCost = netCost;

            // STRATEGIC OVERPAYMENT TARGETING:
            // - Mega transactions: 90% chance of overpayment (BIG REFUNDS!)
            // - Large transactions: 70% chance of overpayment (GOOD REFUNDS)
            // - Normal transactions: 15% chance of overpayment (small refunds)

            var overpaymentChance = isMegaTransaction ? 90 
                                  : isLargeTransaction ? 70 
                                  : 15;

            bool shouldBeOverpayment = random.Next(100) < overpaymentChance;

            if (shouldBeOverpayment)
            {
                // OVERPAYMENT SCENARIOS - Larger transactions get larger overpayment %
                var overpaymentCase = isMegaTransaction 
                    ? random.Next(5, 10)  // Cases 5-9 (30-100% overpayment)
                    : isLargeTransaction
                        ? random.Next(6, 10) // Cases 6-9 (30-75% overpayment)
                        : random.Next(8, 10); // Cases 8-9 (30-40% overpayment)

                switch (overpaymentCase)
                {
                    case 5: // MAJOR 100% overpayment - MEGA transactions only
                        var baseTax1 = Math.Round(netCost * (taxRate / 100), 2);
                        totalTaxAmount = Math.Round(baseTax1 * 2.0m, 2);
                        break;
                    case 6: // Large 75% overpayment
                        var baseTax2 = Math.Round(netCost * (taxRate / 100), 2);
                        totalTaxAmount = Math.Round(baseTax2 * 1.75m, 2);
                        break;
                    case 7: // Significant 50% overpayment
                        var baseTax3 = Math.Round(netCost * (taxRate / 100), 2);
                        totalTaxAmount = Math.Round(baseTax3 * 1.50m, 2);
                        break;
                    case 8: // Wrong state rate 40% overpayment
                        var baseTax4 = Math.Round(netCost * (taxRate / 100), 2);
                        totalTaxAmount = Math.Round(baseTax4 * 1.40m, 2);
                        break;
                    case 9: // Moderate 30% overpayment
                        var baseTax5 = Math.Round(netCost * (taxRate / 100), 2);
                        totalTaxAmount = Math.Round(baseTax5 * 1.30m, 2);
                        break;
                }
            }
            else
            {
                // Regular anomalies (price spikes, quantity issues, zero tax)
                var hasAnomaly = random.Next(100) < 15; // 15% have other anomalies
                if (hasAnomaly)
                {
                    switch (random.Next(5))
                    {
                        case 0: // Severe price spike
                            pricePerUnit = Math.Round((decimal)(random.NextDouble() * 2.00 + 5.50), 4);
                            netCost = Math.Round(quantity * pricePerUnit, 2);
                            totalTaxAmount = Math.Round(netCost * (taxRate / 100), 2);
                            break;
                        case 1: // Moderate price spike
                            pricePerUnit = Math.Round((decimal)(random.NextDouble() * 1.00 + 5.00), 4);
                            netCost = Math.Round(quantity * pricePerUnit, 2);
                            totalTaxAmount = Math.Round(netCost * (taxRate / 100), 2);
                            break;
                        case 2: // Unusual high quantity
                            quantity = (decimal)(random.NextDouble() * 300 + 200);
                            netCost = Math.Round(quantity * pricePerUnit, 2);
                            totalTaxAmount = Math.Round(netCost * (taxRate / 100), 2);
                            break;
                        case 3: // Unusual low quantity
                            quantity = (decimal)(random.NextDouble() * 5 + 1);
                            netCost = Math.Round(quantity * pricePerUnit, 2);
                            totalTaxAmount = Math.Round(netCost * (taxRate / 100), 2);
                            break;
                        case 4: // Zero tax
                            totalTaxAmount = 0;
                            break;
                    }
                }
            }

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

        // For demo purposes: Create realistic status distribution with proper refund scenarios
        // Expected distribution:
        // - 12% APPROVED (with claim amounts - TAX OVERPAYMENTS = REFUNDS)
        // - 15% REVIEWED (flagged and reviewed, awaiting approval)
        // - 8% REJECTED (reviewed but deemed invalid)
        // - 10% PENDING (flagged but not yet reviewed)
        // - 55% null (normal transactions, not flagged)

        var anomalousRecords = records.Where(r => r.TotalTaxAmount == 0 || r.PricePerUnit > 5.0m || r.Quantity > 200 || r.Quantity < 10).ToList();
        var approvedCount = 0;
        var reviewedCount = 0;
        var rejectedCount = 0;
        var pendingCount = 0;

        // Prioritize LARGE overpayment records for approval (biggest refund opportunities first!)
        var overpaymentRecords = records
            .Where(r => {
                var expectedTax = r.NetCost * (GetStateTaxRate(r.MerchantState ?? "CA") / 100m);
                var overpayment = r.TotalTaxAmount - expectedTax;
                return overpayment > 0.50m; // Even small overpayments count
            })
            .OrderByDescending(r => {
                // Sort by refund amount (largest first)
                var expectedTax = r.NetCost * (GetStateTaxRate(r.MerchantState ?? "CA") / 100m);
                return r.TotalTaxAmount - expectedTax;
            })
            .ToList();

        _logger.LogInformation($"Found {overpaymentRecords.Count} overpayment records for potential approval");

        // Process overpayments first (these are refund opportunities)
        // APPROVE MORE RECORDS (15% of total, not just anomalies)
        var approvalTarget = Math.Max(recordCount * 12 / 100, overpaymentRecords.Count / 2); // At least 12% or half of overpayments

        foreach (var record in overpaymentRecords)
        {
            if (approvedCount < approvalTarget)
            {
                // Calculate proper refund amount (overpayment)
                var expectedTax = record.NetCost * (GetStateTaxRate(record.MerchantState ?? "CA") / 100m);
                var overpayment = record.TotalTaxAmount - expectedTax;

                if (overpayment > 0.50m) // Approve all meaningful refunds (> $0.50)
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
                    record.ClaimAmount = Math.Round(overpayment, 2); // This is the REFUND amount

                    // Add descriptive comments based on refund size
                    var refundCategory = overpayment switch
                    {
                        > 500 => "MAJOR REFUND",
                        > 200 => "LARGE REFUND",
                        > 100 => "SIGNIFICANT REFUND",
                        > 50 => "MODERATE REFUND",
                        _ => "REFUND"
                    };
                    record.ReviewedComments = $"{refundCategory} APPROVED - Tax overpayment of ${overpayment:F2}";

                    approvedCount++;
                }
            }
        }

        // Process remaining anomalous records for other statuses
        var remainingAnomalies = anomalousRecords.Except(overpaymentRecords).ToList();

        foreach (var record in remainingAnomalies.OrderByDescending(r => r.NetCost))
        {
            if (reviewedCount < anomalousRecords.Count * 0.15) // 15% reviewed but not approved
            {
                record.Status = "REVIEWED";
                record.ReviewedByUserId = "1";
                record.ReviewedByUserName = "Naveen L";
                record.ReviewedDate = record.TransactionDate.AddDays(random.Next(1, 5));
                record.ReviewedComments = "Flagged for manager approval";
                reviewedCount++;
            }
            else if (rejectedCount < anomalousRecords.Count * 0.10) // 10% rejected
            {
                record.Status = "REJECTED";
                record.ReviewedByUserId = "1";
                record.ReviewedByUserName = "Naveen L";
                record.ReviewedDate = record.TransactionDate.AddDays(random.Next(1, 5));
                record.ReviewedComments = new[] { 
                    "Insufficient documentation", 
                    "Outside audit scope", 
                    "Merchant error - not recoverable",
                    "Below materiality threshold" 
                }[random.Next(4)];
                rejectedCount++;
            }
            else if (pendingCount < anomalousRecords.Count * 0.10) // 10% pending review
            {
                record.Status = "PENDING";
                pendingCount++;
            }
            // Remaining 55% stay null (normal transactions)
        }

        _cachedRecords = records;

        var approvedRefunds = records.Where(r => r.Status == "APPROVED" && r.ClaimAmount.HasValue).ToList();
        var totalRefundAmount = approvedRefunds.Sum(r => r.ClaimAmount.Value);
        var largeRefunds = approvedRefunds.Where(r => r.ClaimAmount > 100).Count();
        var majorRefunds = approvedRefunds.Where(r => r.ClaimAmount > 500).Count();
        var megaRefunds = approvedRefunds.Where(r => r.ClaimAmount > 1000).Count();

        // Get top 5 refunds for demo visibility
        var top5Refunds = approvedRefunds
            .OrderByDescending(r => r.ClaimAmount)
            .Take(5)
            .Select(r => $"${r.ClaimAmount:F2}")
            .ToList();

        var megaTransactions = records.Where(r => r.Quantity > 1500).Count();
        var largeTransactions = records.Where(r => r.Quantity >= 500 && r.Quantity <= 1500).Count();

        _logger.LogInformation(
            $"✅ Successfully generated {records.Count} synthetic records with PII masking\n" +
            $"  📊 Transaction Mix: {megaTransactions} MEGA (1500+ gal), {largeTransactions} LARGE (500-1500 gal)\n" +
            $"  📊 Status Distribution: {approvedCount} APPROVED, {reviewedCount} REVIEWED, {rejectedCount} REJECTED, {pendingCount} PENDING\n" +
            $"  💰 Total Refunds: ${totalRefundAmount:F2} ({approvedCount} claims)\n" +
            $"  🎯 Refund Breakdown: {megaRefunds} over $1,000 | {majorRefunds} over $500 | {largeRefunds} over $100\n" +
            $"  🏆 Top 5 Refunds: {string.Join(", ", top5Refunds)}\n" +
            $"  ⚠️  Anomalies: {anomalousRecords.Count} records\n" +
            $"  📈 Overpayments Found: {overpaymentRecords.Count} potential refund opportunities"
        );
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

