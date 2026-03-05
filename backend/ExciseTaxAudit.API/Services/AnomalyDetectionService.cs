using ExciseTaxAudit.API.Models;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for anomaly detection in fuel transactions using statistical methods.
/// Detects potential over/under-charging and suspicious pricing patterns.
/// </summary>
public class AnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> _logger;

    public AnomalyDetectionService(ILogger<AnomalyDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate anomaly score for a transaction based on statistical deviations.
    /// Returns a score between 0 (normal) and 1 (highly anomalous).
    /// </summary>
    public (float Score, string Reason) CalculateAnomalyScore(TransactionRecord transaction, List<TransactionRecord> baseline)
    {
        var reasons = new List<string>();
        var scores = new List<float>();

        // 1. Price per unit anomaly (Z-score)
        var priceAnomaly = DetectPriceAnomaly(transaction, baseline);
        if (priceAnomaly.Score > 0.5f)
        {
            scores.Add(priceAnomaly.Score);
            reasons.Add(priceAnomaly.Reason);
        }

        // 2. Tax amount anomaly
        var taxAnomaly = DetectTaxAnomaly(transaction);
        if (taxAnomaly.Score > 0.5f)
        {
            scores.Add(taxAnomaly.Score);
            reasons.Add(taxAnomaly.Reason);
        }

        // 3. Cost per unit anomaly
        var costAnomaly = DetectCostAnomalies(transaction);
        if (costAnomaly.Score > 0.5f)
        {
            scores.Add(costAnomaly.Score);
            reasons.Add(costAnomaly.Reason);
        }

        // 4. Quantity anomaly (unusual volumes)
        var quantityAnomaly = DetectQuantityAnomaly(transaction, baseline);
        if (quantityAnomaly.Score > 0.5f)
        {
            scores.Add(quantityAnomaly.Score);
            reasons.Add(quantityAnomaly.Reason);
        }

        float finalScore = scores.Count > 0 ? scores.Average() : 0f;
        string finalReason = string.Join(" | ", reasons);

        return (finalScore, finalReason);
    }

    private (float Score, string Reason) DetectPriceAnomaly(TransactionRecord transaction, List<TransactionRecord> baseline)
    {
        // Find similar transactions (same merchant, fuel type, state)
        var similar = baseline
            .Where(t => t.MerchantName == transaction.MerchantName &&
                       t.FuelType == transaction.FuelType &&
                       t.MerchantState == transaction.MerchantState)
            .ToList();

        // If we have baseline data, use Z-score comparison
        if (similar.Count >= 3)
        {
            var prices = similar.Select(t => t.PricePerUnit).ToList();
            var mean = prices.Average();
            var stdDev = CalculateStdDev(prices);

            if (stdDev > 0)
            {
                var zScore = Math.Abs((double)(transaction.PricePerUnit - (decimal)mean) / (double)stdDev);

                if (zScore > 3)
                {
                    var direction = transaction.PricePerUnit > (decimal)mean ? "HIGHER" : "LOWER";
                    return (Math.Min((float)zScore / 5f, 1f), $"Price {direction} than baseline (Z={zScore:F2})");
                }
            }
        }
        
        // Without baseline, check against expected price ranges
        var expectedPriceRange = GetExpectedPriceRange(transaction.FuelType);
        if (transaction.PricePerUnit > expectedPriceRange.High)
        {
            var excessPercent = (double)(transaction.PricePerUnit - expectedPriceRange.High) / (double)expectedPriceRange.High;
            var score = (float)Math.Min(0.5 + excessPercent, 0.95);
            return (score, $"Price {excessPercent:P0} above expected range");
        }
        else if (transaction.PricePerUnit < expectedPriceRange.Low)
        {
            var deficitPercent = (double)(expectedPriceRange.Low - transaction.PricePerUnit) / (double)expectedPriceRange.Low;
            var score = (float)Math.Min(0.5 + deficitPercent * 0.5, 0.85);
            return (score, $"Price {deficitPercent:P0} below expected range");
        }

        return (0f, "");
    }
    
    private (decimal Low, decimal High) GetExpectedPriceRange(string? fuelType)
    {
        // Expected price ranges per gallon based on 2024-2026 fuel prices
        return (fuelType?.ToUpper()) switch
        {
            var ft when ft?.Contains("DIESEL") == true => (3.00m, 5.50m),
            var ft when ft?.Contains("PREMIUM") == true => (3.50m, 6.00m),
            var ft when ft?.Contains("UNLEADED") == true || ft?.Contains("GASOLINE") == true => (2.50m, 5.00m),
            _ => (2.50m, 6.00m) // Default range
        };
    }

    private (float Score, string Reason) DetectTaxAnomaly(TransactionRecord transaction)
    {
        // IRS Safe Harbor: Rev Proc 2011-42 allows ±10% variance in fuel tax calculations
        // Flag transactions that exceed safe harbor or have obvious errors
        
        // Check if tax amount seems unusual given the transaction volume and state
        // Zero tax on significant purchase is a clear violation
        if (transaction.NetCost > 100 && transaction.TotalTaxAmount == 0)
        {
            // Vary the score based on the transaction size for diversity
            var scoreFactor = Math.Min((double)transaction.NetCost / 500.0, 1.0);
            var score = (float)(0.75 + scoreFactor * 0.15); // Range: 0.75-0.90
            return (score, "Zero tax on high-value transaction (exceeds IRS safe harbor)");
        }
        
        if (transaction.NetCost > 50 && transaction.TotalTaxAmount == 0)
        {
            return (0.70f, "Zero tax detected (exceeds IRS safe harbor)");
        }

        // Calculate expected tax based on state rates
        var expectedTax = CalculateExpectedTax(transaction);
        if (expectedTax <= 0) return (0f, "");
        
        var actualTax = transaction.TotalTaxAmount;
        var variance = (double)(actualTax - expectedTax) / (double)expectedTax;
        var absVariance = Math.Abs(variance);

        // IRS Safe Harbor thresholds:
        // - Within ±10%: Acceptable (no flag)
        // - 10-20% deviation: Borderline (flag for review)
        // - Beyond 20%: Clear violation (high priority flag)
        
        if (absVariance <= 0.10) // Within IRS safe harbor (±10%)
        {
            return (0f, ""); // No anomaly - within IRS safe harbor
        }
        else if (absVariance <= 0.20) // 10-20%: Exceeds safe harbor but not extreme
        {
            var score = (float)(0.50 + (absVariance - 0.10) * 2.0); // 0.50-0.70
            if (variance < 0) // Underpayment
            {
                return (score, $"Tax underpayment ({absVariance:P0} - exceeds IRS ±10% safe harbor)");
            }
            else // Overpayment - refund opportunity
            {
                return (score, $"Tax overpayment ({absVariance:P0} - exceeds IRS ±10% safe harbor, potential refund)");
            }
        }
        else // Beyond 20%: Major deviation from IRS standards
        {
            var score = (float)Math.Min(0.70 + absVariance * 0.5, 0.95); // 0.70-0.95
            if (variance < 0) // Underpayment
            {
                return (score, $"Major tax underpayment ({absVariance:P0} - significantly exceeds IRS safe harbor)");
            }
            else // Overpayment
            {
                return (score, $"Major tax overpayment ({absVariance:P0} - significantly exceeds IRS safe harbor, refund opportunity)");
            }
        }
    }

    private (float Score, string Reason) DetectCostAnomalies(TransactionRecord transaction)
    {
        // Check for discrepancies between net cost and gross cost
        if (transaction.GrossCost < transaction.NetCost)
        {
            return (0.7f, "Gross cost less than net cost (data inconsistency)");
        }

        // Check for zero or negative costs
        if (transaction.NetCost <= 0 || transaction.GrossCost <= 0)
        {
            return (0.9f, "Invalid cost values (zero or negative)");
        }

        // Check cost per unit calculation
        var calculatedCost = transaction.Quantity * transaction.PricePerUnit;
        if (transaction.Quantity > 0 && Math.Abs(calculatedCost - transaction.NetCost) > 10)
        {
            return (0.6f, $"Cost calculation mismatch (expected {calculatedCost}, got {transaction.NetCost})");
        }

        return (0f, "");
    }

    private (float Score, string Reason) DetectQuantityAnomaly(TransactionRecord transaction, List<TransactionRecord> baseline)
    {
        // Find similar transactions (same asset, fuel type)
        var similar = baseline
            .Where(t => t.AssetNumber == transaction.AssetNumber &&
                       t.FuelType == transaction.FuelType)
            .ToList();

        if (similar.Count < 3)
            return (0f, "");

        var quantities = similar.Select(t => t.Quantity).ToList();
        var mean = quantities.Average();
        var stdDev = CalculateStdDev(quantities);

        if (stdDev == 0)
            return (0f, "");

        var zScore = Math.Abs((double)(transaction.Quantity - mean) / (double)stdDev);

        if (zScore > 3)
        {
            return (0.5f, $"Unusual quantity deviation (Z={zScore:F2})");
        }

        return (0f, "");
    }

    private decimal CalculateStdDev(List<decimal> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
        return (decimal)Math.Sqrt((double)sumOfSquares / (values.Count - 1));
    }

    /// <summary>
    /// Calculate confidence score based on anomaly score.
    /// Higher anomaly scores indicate higher confidence in the anomaly detection.
    /// </summary>
    public float CalculateConfidence(float anomalyScore)
    {
        // Map anomaly score (0-1) to confidence (0-1)
        // Higher anomaly scores = higher confidence
        if (anomalyScore >= 0.8f) return 0.90f;
        if (anomalyScore >= 0.7f) return 0.85f;
        if (anomalyScore >= 0.6f) return 0.75f;
        if (anomalyScore >= 0.5f) return 0.65f;
        return 0.50f; // Low confidence for borderline cases
    }

    /// <summary>
    /// Calculate expected tax based on per-gallon federal + state excise tax rates.
    /// Uses the same rate structure as TaxRatePlugin and IRSDataConnectorService
    /// to ensure consistent anomaly detection and AI agent review results.
    /// </summary>
    public decimal CalculateExpectedTax(TransactionRecord transaction)
    {
        // Per-gallon excise tax rates (Federal + State combined)
        // Must stay in sync with TaxRatePlugin.cs and IRSDataConnectorService._fallbackRates
        var dieselRates = new Dictionary<string, decimal>
        {
            { "CA", 0.244m + 0.133m }, // $0.377/gal
            { "TX", 0.244m + 0.20m },  // $0.444/gal
            { "NY", 0.244m + 0.169m }, // $0.413/gal
            { "IL", 0.244m + 0.219m }, // $0.463/gal
            { "FL", 0.244m + 0.191m }, // $0.435/gal
            { "PA", 0.244m + 0.255m }, // $0.499/gal
            { "OH", 0.244m + 0.28m }   // $0.524/gal
        };

        var gasolineRates = new Dictionary<string, decimal>
        {
            { "CA", 0.184m + 0.539m }, // $0.723/gal
            { "TX", 0.184m + 0.20m },  // $0.384/gal
            { "NY", 0.184m + 0.459m }, // $0.643/gal
            { "IL", 0.184m + 0.392m }, // $0.576/gal
            { "FL", 0.184m + 0.196m }, // $0.380/gal
            { "PA", 0.184m + 0.576m }, // $0.760/gal
            { "OH", 0.184m + 0.385m }  // $0.569/gal
        };

        // Determine fuel category
        var isDiesel = transaction.FuelType?.Contains("DIESEL", StringComparison.OrdinalIgnoreCase) == true;
        var rates = isDiesel ? dieselRates : gasolineRates;
        var defaultRate = isDiesel ? 0.244m : 0.184m; // Federal rate as fallback

        var state = transaction.MerchantState ?? "";
        var perGallonRate = rates.ContainsKey(state) ? rates[state] : defaultRate;

        // Apply safe harbor discount if applicable
        if (transaction.K_SafeHarborPercentage.HasValue && transaction.K_SafeHarborPercentage > 0)
        {
            perGallonRate = perGallonRate * (1 - transaction.K_SafeHarborPercentage.Value);
        }

        return transaction.Quantity * perGallonRate;
    }
}
