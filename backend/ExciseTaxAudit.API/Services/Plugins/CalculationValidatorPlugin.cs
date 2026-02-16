using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ExciseTaxAudit.API.Services.Plugins;

/// <summary>
/// Plugin for validating tax calculations and identifying anomalies
/// </summary>
public class CalculationValidatorPlugin
{
    [KernelFunction("validate_transaction_data")]
    [Description("Validates that transaction data is complete and logical")]
    [return: Description("Validation result message")]
    public string ValidateTransactionData(
        [Description("Quantity in gallons")] decimal quantity,
        [Description("Price per unit")] decimal pricePerUnit,
        [Description("Net cost")] decimal netCost,
        [Description("Tax amount")] decimal taxAmount)
    {
        var issues = new List<string>();

        if (quantity <= 0)
            issues.Add("Invalid quantity (must be positive)");

        if (pricePerUnit <= 0)
            issues.Add("Invalid price per unit (must be positive)");

        if (netCost <= 0)
            issues.Add("Invalid net cost (must be positive)");

        if (taxAmount < 0)
            issues.Add("Invalid tax amount (cannot be negative)");

        // Validate net cost calculation
        var expectedNetCost = quantity * pricePerUnit;
        var costVariance = Math.Abs(netCost - expectedNetCost) / expectedNetCost;
        if (costVariance > 0.01m) // Allow 1% variance for rounding
        {
            issues.Add($"Net cost mismatch (expected ${expectedNetCost:F2}, got ${netCost:F2})");
        }

        return issues.Count == 0 
            ? "All transaction data is valid" 
            : "Validation issues: " + string.Join("; ", issues);
    }

    [KernelFunction("calculate_variance_percentage")]
    [Description("Calculates the percentage variance between actual and expected values")]
    [return: Description("The variance percentage (e.g., 0.15 for 15%)")]
    public decimal CalculateVariancePercentage(
        [Description("The actual value")] decimal actualValue,
        [Description("The expected value")] decimal expectedValue)
    {
        if (expectedValue == 0) return actualValue == 0 ? 0 : 1;
        return Math.Abs((actualValue - expectedValue) / expectedValue);
    }

    [KernelFunction("determine_claim_schedule")]
    [Description("Determines the appropriate IRS Form 8849 schedule based on fuel type and use")]
    [return: Description("The Form 8849 schedule number (1-6)")]
    public int DetermineClaimSchedule(
        [Description("The fuel type")] string fuelType,
        [Description("Is this for off-highway use?")] bool isOffHighway = false)
    {
        // Simplified schedule determination
        if (isOffHighway)
        {
            return fuelType.ToLower() switch
            {
                "diesel" => 2, // Schedule 2: Nontaxable Use of Diesel Fuel
                "gasoline" => 1, // Schedule 1: Nontaxable Use of Gasoline
                "kerosene" => 3, // Schedule 3: Nontaxable Use of Kerosene
                _ => 6 // Schedule 6: Other Nontaxable Use
            };
        }

        return 5; // Schedule 5: Registered Ultimate Vendor
    }

    [KernelFunction("calculate_potential_recovery")]
    [Description("Calculates the potential refund amount based on tax overpayment")]
    [return: Description("The potential recovery amount in dollars")]
    public decimal CalculatePotentialRecovery(
        [Description("The actual tax paid")] decimal actualTax,
        [Description("The correct tax amount")] decimal correctTax)
    {
        var recovery = actualTax - correctTax;
        return recovery > 0 ? recovery : 0;
    }

    [KernelFunction("assess_risk_level")]
    [Description("Assesses the audit risk level of approving a claim")]
    [return: Description("Risk level: Low, Medium, High, or Critical")]
    public string AssessRiskLevel(
        [Description("Variance percentage")] decimal variance,
        [Description("Claim amount in dollars")] decimal claimAmount)
    {
        // High dollar amounts require more scrutiny
        if (claimAmount > 10000)
        {
            if (variance > 0.20m) return "Critical";
            if (variance > 0.10m) return "High";
            return "Medium";
        }

        if (variance > 0.30m) return "High";
        if (variance > 0.15m) return "Medium";
        return "Low";
    }
}
