using ExciseTaxAudit.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ExciseTaxAudit.API.Services.Plugins;

/// <summary>
/// Plugin for searching historical approval patterns using RAG approach
/// </summary>
public class HistoricalApprovalPlugin
{
    private readonly AuditContext _context;

    public HistoricalApprovalPlugin(AuditContext context)
    {
        _context = context;
    }

    [KernelFunction("search_similar_approved_cases")]
    [Description("Searches for similar previously approved transactions to support current decision")]
    [return: Description("Summary of similar approved cases")]
    public async Task<string> SearchSimilarApprovedCases(
        [Description("The fuel type")] string fuelType,
        [Description("The state")] string state,
        [Description("The variance percentage")] decimal variance)
    {
        var approvedTransactions = await _context.TransactionRecords
            .Where(t => t.Status == "APPROVED" 
                && t.FuelType == fuelType 
                && t.MerchantState == state)
            .OrderByDescending(t => t.ApprovedDate)
            .Take(10)
            .ToListAsync();

        if (!approvedTransactions.Any())
        {
            return $"No similar approved cases found for {fuelType} in {state}";
        }

        // Calculate average variance (we don't have ExpectedTaxAmount field, so use claim amount as proxy)
        var avgVariance = approvedTransactions
            .Where(t => t.ClaimAmount.HasValue && t.TotalTaxAmount > 0)
            .Select(t => Math.Abs(t.ClaimAmount!.Value / t.TotalTaxAmount))
            .DefaultIfEmpty(0)
            .Average();

        var avgClaimAmount = approvedTransactions
            .Where(t => t.ClaimAmount.HasValue)
            .Select(t => t.ClaimAmount!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return $"Found {approvedTransactions.Count} similar approved cases: " +
               $"Avg variance {avgVariance:P2}, Avg claim ${avgClaimAmount:F2}. " +
               $"Current variance {variance:P2} is {(variance <= avgVariance ? "within" : "above")} historical pattern.";
    }

    [KernelFunction("get_approval_statistics")]
    [Description("Gets overall approval statistics for pattern analysis")]
    [return: Description("Approval statistics summary")]
    public async Task<string> GetApprovalStatistics(
        [Description("The fuel type to filter by (optional)")] string? fuelType = null)
    {
        var query = _context.TransactionRecords.AsQueryable();

        if (!string.IsNullOrEmpty(fuelType))
        {
            query = query.Where(t => t.FuelType == fuelType);
        }

        var totalFlagged = await query.CountAsync(t => t.AnomalyScore > 0.5);
        var totalReviewed = await query.CountAsync(t => t.Status == "REVIEWED" || t.Status == "APPROVED" || t.Status == "REJECTED");
        var totalApproved = await query.CountAsync(t => t.Status == "APPROVED");
        var totalRejected = await query.CountAsync(t => t.Status == "REJECTED");

        var approvalRate = totalReviewed > 0 ? (decimal)totalApproved / totalReviewed : 0;

        return $"Statistics{(fuelType != null ? $" for {fuelType}" : "")}: " +
               $"{totalFlagged} flagged, {totalReviewed} reviewed, " +
               $"{totalApproved} approved ({approvalRate:P1} approval rate), {totalRejected} rejected";
    }

    [KernelFunction("check_reviewer_consensus")]
    [Description("Checks if multiple reviewers have handled similar cases")]
    [return: Description("Reviewer consensus information")]
    public async Task<string> CheckReviewerConsensus(
        [Description("The fuel type")] string fuelType,
        [Description("The state")] string state)
    {
        var reviewedCases = await _context.TransactionRecords
            .Where(t => t.FuelType == fuelType 
                && t.MerchantState == state 
                && !string.IsNullOrEmpty(t.ReviewedBy))
            .GroupBy(t => new { t.ReviewedBy, t.Status })
            .Select(g => new { g.Key.ReviewedBy, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        if (!reviewedCases.Any())
        {
            return "No reviewer history found for this case type";
        }

        var reviewerSummary = reviewedCases
            .GroupBy(r => r.ReviewedBy)
            .Select(g => $"{g.Key}: {g.Sum(x => x.Count)} cases")
            .ToList();

        return $"Reviewer consensus: {string.Join(", ", reviewerSummary)}";
    }
}
