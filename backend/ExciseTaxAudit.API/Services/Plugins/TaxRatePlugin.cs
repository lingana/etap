using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ExciseTaxAudit.API.Services.Plugins;

/// <summary>
/// Plugin for looking up federal and state excise tax rates
/// </summary>
public class TaxRatePlugin
{
    private readonly Dictionary<string, Dictionary<string, decimal>> _taxRates;

    public TaxRatePlugin()
    {
        // Initialize with current IRS and state excise tax rates (per gallon)
        _taxRates = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["Diesel"] = new Dictionary<string, decimal>
            {
                ["Federal"] = 0.244m,
                ["CA"] = 0.133m,
                ["TX"] = 0.20m,
                ["FL"] = 0.191m,
                ["NY"] = 0.169m,
                ["PA"] = 0.255m,
                ["IL"] = 0.219m,
                ["OH"] = 0.28m,
                ["GA"] = 0.133m,
                ["NC"] = 0.003m,
                ["MI"] = 0.198m
            },
            ["Gasoline"] = new Dictionary<string, decimal>
            {
                ["Federal"] = 0.184m,
                ["CA"] = 0.539m,
                ["TX"] = 0.20m,
                ["FL"] = 0.196m,
                ["NY"] = 0.459m,
                ["PA"] = 0.576m,
                ["IL"] = 0.392m,
                ["OH"] = 0.385m,
                ["GA"] = 0.316m,
                ["NC"] = 0.383m,
                ["MI"] = 0.277m
            },
            ["Kerosene"] = new Dictionary<string, decimal>
            {
                ["Federal"] = 0.244m,
                ["CA"] = 0.133m,
                ["TX"] = 0.20m,
                ["FL"] = 0.191m,
                ["NY"] = 0.169m,
                ["PA"] = 0.255m,
                ["IL"] = 0.219m,
                ["OH"] = 0.28m,
                ["GA"] = 0.133m,
                ["NC"] = 0.003m,
                ["MI"] = 0.198m
            }
        };
    }

    [KernelFunction("get_tax_rate")]
    [Description("Gets the excise tax rate per gallon for a specific fuel type and state")]
    [return: Description("The tax rate per gallon in dollars")]
    public decimal GetTaxRate(
        [Description("The type of fuel (Diesel, Gasoline, Kerosene)")] string fuelType,
        [Description("The two-letter state code (e.g., CA, TX, FL)")] string state)
    {
        if (!_taxRates.ContainsKey(fuelType))
        {
            return 0;
        }

        if (!_taxRates[fuelType].ContainsKey(state))
        {
            // Return federal rate if state not found
            return _taxRates[fuelType].GetValueOrDefault("Federal", 0);
        }

        return _taxRates[fuelType][state];
    }

    [KernelFunction("calculate_expected_tax")]
    [Description("Calculates the expected tax amount based on quantity and rates")]
    [return: Description("The expected total tax amount in dollars")]
    public decimal CalculateExpectedTax(
        [Description("The type of fuel")] string fuelType,
        [Description("The state code")] string state,
        [Description("The quantity in gallons")] decimal quantity)
    {
        var taxRate = GetTaxRate(fuelType, state);
        return quantity * taxRate;
    }

    [KernelFunction("get_safe_harbor_threshold")]
    [Description("Gets the IRS safe harbor variance threshold percentage")]
    [return: Description("The safe harbor percentage (e.g., 0.10 for 10%)")]
    public decimal GetSafeHarborThreshold()
    {
        // IRS safe harbor: ±10% variance is typically acceptable
        return 0.10m;
    }

    [KernelFunction("is_within_safe_harbor")]
    [Description("Determines if a tax variance falls within IRS safe harbor limits")]
    [return: Description("True if within safe harbor, false if exceeds limits")]
    public bool IsWithinSafeHarbor(
        [Description("The actual tax amount paid")] decimal actualTax,
        [Description("The expected tax amount")] decimal expectedTax)
    {
        if (expectedTax == 0) return actualTax == 0;
        
        var variance = Math.Abs((actualTax - expectedTax) / expectedTax);
        return variance <= GetSafeHarborThreshold();
    }
}
