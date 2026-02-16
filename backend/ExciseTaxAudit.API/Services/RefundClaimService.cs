using ExciseTaxAudit.API.Controllers;
using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ExciseTaxAudit.API.Services;

public class RefundClaimService
{
    private readonly AuditContext _context;
    private readonly ILogger<RefundClaimService> _logger;
    private readonly Form8849GeneratorService _pdfGenerator;

    public RefundClaimService(
        AuditContext context, 
        ILogger<RefundClaimService> logger,
        Form8849GeneratorService pdfGenerator)
    {
        _context = context;
        _logger = logger;
        _pdfGenerator = pdfGenerator;
    }

    /// <summary>
    /// Generate a refund claim from flagged transactions in an engagement
    /// </summary>
    public async Task<RefundClaim> GenerateClaimFromTransactionsAsync(
        int engagementId,
        List<int> transactionIds,
        string createdBy,
        int? auditCaseId = null)
    {
        var engagement = await _context.Engagements
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == engagementId);

        if (engagement == null)
            throw new ArgumentException($"Engagement {engagementId} not found");

        var transactions = await _context.TransactionRecords
            .Where(t => transactionIds.Contains((int)t.RecordID))
            .ToListAsync();

        if (!transactions.Any())
            throw new ArgumentException("No transactions found");

        // Calculate refund amount (sum of ClaimAmount or Label_AdjustmentAmount)
        var claimedAmount = transactions.Sum(t => 
            t.ClaimAmount ?? Math.Abs(t.Label_AdjustmentAmount ?? 0));

        // Determine tax period
        var periodStart = transactions.Min(t => t.TransactionDate);
        var periodEnd = transactions.Max(t => t.TransactionDate);
        var taxYear = periodStart.Year;
        var quarter = GetQuarter(periodStart);

        // Determine refund type based on transaction analysis
        var refundType = DetermineRefundType(transactions);

        // Generate claim number
        var claimNumber = await GenerateClaimNumberAsync(taxYear);

        // Extract IRS citations from AI analysis
        var citations = ExtractCitations(transactions);

        var claim = new RefundClaim
        {
            EngagementId = engagementId,
            AuditCaseId = auditCaseId,
            ClaimNumber = claimNumber,
            RefundType = refundType,
            Status = ClaimStatus.Draft,
            TaxType = DetermineTaxTypeFromTransactions(transactions),
            TaxFormType = "Form 8849",
            ClaimedAmount = claimedAmount,
            ApprovedAmount = 0,
            PaidAmount = 0,
            TaxPeriodStart = periodStart,
            TaxPeriodEnd = periodEnd,
            TaxYear = taxYear,
            TaxQuarter = quarter,
            TransactionCount = transactions.Count,
            TransactionIds = transactionIds,
            Justification = GenerateJustification(transactions, refundType),
            IRSCitations = JsonSerializer.Serialize(citations),
            SupportingDocuments = "[]",
            EIN = engagement.Client?.EIN,
            NameOfClaimant = engagement.Client?.Name,
            ClaimantAddress = engagement.Client?.Address,
            ContactName = engagement.Client?.ContactPerson,
            ContactPhone = engagement.Client?.ContactPhone,
            ContactEmail = engagement.Client?.ContactEmail,
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            ConfidenceScore = CalculateConfidence(transactions),
            AIRecommendation = GenerateAIRecommendation(transactions, claimedAmount),
            AIReasoning = GenerateAIReasoning(transactions),
            Priority = CalculatePriority(claimedAmount, transactions.Count)
        };

        _context.RefundClaims.Add(claim);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Generated refund claim {claimNumber} for ${claimedAmount:N2}");

        return claim;
    }

    /// <summary>
    /// Create a new refund claim directly (manual entry, no transactions)
    /// </summary>
    public async Task<RefundClaim> CreateClaimAsync(CreateClaimRequest request, string createdBy)
    {
        var engagement = await _context.Engagements
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == request.EngagementId);

        if (engagement == null)
            throw new ArgumentException($"Engagement {request.EngagementId} not found");

        var taxYear = request.TaxYear > 0 ? request.TaxYear : DateTime.UtcNow.Year;
        var claimNumber = await GenerateClaimNumberAsync(taxYear);
        var quarter = !string.IsNullOrEmpty(request.TaxQuarter) ? request.TaxQuarter : GetQuarter(DateTime.UtcNow);

        var claim = new RefundClaim
        {
            EngagementId = request.EngagementId,
            ClaimNumber = claimNumber,
            RefundType = request.RefundType ?? RefundType.Overpayment,
            Status = ClaimStatus.Draft,
            TaxType = request.TaxType ?? "Fuel Excise Tax",
            TaxFormType = "Form 8849",
            ClaimedAmount = request.ClaimedAmount ?? 0,
            ApprovedAmount = 0,
            PaidAmount = 0,
            TaxPeriodStart = request.TaxPeriodStart ?? new DateTime(taxYear, 1, 1),
            TaxPeriodEnd = request.TaxPeriodEnd ?? new DateTime(taxYear, 12, 31),
            TaxYear = taxYear,
            TaxQuarter = quarter,
            TransactionCount = 0,
            TransactionIds = new List<int>(),
            Justification = request.Justification ?? string.Empty,
            IRSCitations = "[]",
            SupportingDocuments = "[]",
            EIN = request.EIN ?? engagement.Client?.EIN,
            NameOfClaimant = request.NameOfClaimant ?? engagement.Client?.Name,
            ClaimantAddress = request.ClaimantAddress ?? engagement.Client?.Address,
            ContactName = request.ContactName ?? engagement.Client?.ContactPerson,
            ContactPhone = request.ContactPhone ?? engagement.Client?.ContactPhone,
            ContactEmail = request.ContactEmail ?? engagement.Client?.ContactEmail,
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            ConfidenceScore = 0,
            Priority = request.Priority ?? 3,
            InternalNotes = request.InternalNotes ?? string.Empty
        };

        _context.RefundClaims.Add(claim);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Created manual refund claim {claimNumber}");

        return claim;
    }

    /// <summary>
    /// Update claim status with workflow validation
    /// </summary>
    public async Task<RefundClaim> UpdateClaimStatusAsync(
        int claimId,
        ClaimStatus newStatus,
        string updatedBy,
        string? notes = null)
    {
        var claim = await _context.RefundClaims.FindAsync(claimId);
        if (claim == null)
            throw new ArgumentException($"Claim {claimId} not found");

        // Validate status transition
        if (!IsValidStatusTransition(claim.Status, newStatus))
            throw new InvalidOperationException(
                $"Cannot transition from {claim.Status} to {newStatus}");

        claim.Status = newStatus;
        claim.LastUpdatedDate = DateTime.UtcNow;

        // Update workflow timestamps
        switch (newStatus)
        {
            case ClaimStatus.Submitted:
                claim.SubmittedDate = DateTime.UtcNow;
                claim.SubmittedBy = updatedBy;
                break;
            case ClaimStatus.UnderReview:
                claim.ReviewedDate = DateTime.UtcNow;
                claim.ReviewedBy = updatedBy;
                break;
            case ClaimStatus.Approved:
                claim.ApprovedDate = DateTime.UtcNow;
                break;
            case ClaimStatus.Paid:
            case ClaimStatus.PartiallyPaid:
                claim.PaidDate = DateTime.UtcNow;
                break;
            case ClaimStatus.Rejected:
                claim.RejectionReason = notes;
                break;
        }

        if (!string.IsNullOrEmpty(notes))
        {
            claim.InternalNotes += $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {updatedBy}: {notes}";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Claim {claim.ClaimNumber} status updated to {newStatus}");

        return claim;
    }

    /// <summary>
    /// Get all refund claims, optionally filtered by engagement
    /// </summary>
    public async Task<List<RefundClaim>> GetClaimsAsync(int? engagementId = null)
    {
        var query = _context.RefundClaims
            .Include(c => c.Engagement)
                .ThenInclude(e => e!.Client)
            .Include(c => c.AuditCase)
            .AsQueryable();
        
        if (engagementId.HasValue)
            query = query.Where(c => c.EngagementId == engagementId.Value);

        return await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
    }

    /// <summary>
    /// Get a specific refund claim by ID
    /// </summary>
    public async Task<RefundClaim?> GetClaimByIdAsync(int id)
    {
        return await _context.RefundClaims
            .Include(c => c.Engagement)
                .ThenInclude(e => e!.Client)
            .Include(c => c.AuditCase)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Update claim details
    /// </summary>
    public async Task<RefundClaim> UpdateClaimAsync(int id, dynamic request)
    {
        var claim = await _context.RefundClaims.FindAsync(id);
        if (claim == null)
            throw new ArgumentException($"Claim {id} not found");

        // Update fields if provided
        if (request.EIN != null) claim.EIN = request.EIN;
        if (request.NameOfClaimant != null) claim.NameOfClaimant = request.NameOfClaimant;
        if (request.ClaimantAddress != null) claim.ClaimantAddress = request.ClaimantAddress;
        if (request.ContactName != null) claim.ContactName = request.ContactName;
        if (request.ContactPhone != null) claim.ContactPhone = request.ContactPhone;
        if (request.ContactEmail != null) claim.ContactEmail = request.ContactEmail;
        if (request.ApprovedAmount != null) claim.ApprovedAmount = request.ApprovedAmount;
        if (request.PaidAmount != null) claim.PaidAmount = request.PaidAmount;
        if (request.IRSResponseNotes != null) claim.IRSResponseNotes = request.IRSResponseNotes;
        if (request.InternalNotes != null) claim.InternalNotes = request.InternalNotes;
        if (request.Priority != null) claim.Priority = request.Priority;

        claim.LastUpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return claim;
    }

    /// <summary>
    /// Delete a draft claim (only drafts can be deleted)
    /// </summary>
    public async Task DeleteClaimAsync(int id)
    {
        var claim = await _context.RefundClaims.FindAsync(id);
        if (claim == null)
            throw new ArgumentException($"Claim {id} not found");

        if (claim.Status != ClaimStatus.Draft)
            throw new InvalidOperationException("Only draft claims can be deleted");

        _context.RefundClaims.Remove(claim);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Deleted draft claim {claim.ClaimNumber}");
    }

    /// <summary>
    /// Generate IRS Form 8849 PDF
    /// </summary>
    public async Task<string> GenerateForm8849Async(int claimId)
    {
        var claim = await GetClaimByIdAsync(claimId);
        if (claim == null)
            throw new ArgumentException($"Claim {claimId} not found");

        var filePath = await _pdfGenerator.GenerateForm8849PdfAsync(claimId);

        claim.Form8849Path = filePath;
        claim.LastUpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Generated Form 8849 PDF for claim {claim.ClaimNumber}");

        return filePath;
    }

    /// <summary>
    /// Get Form 8849 PDF file bytes for download
    /// </summary>
    public async Task<(byte[] fileBytes, string fileName)> GetForm8849FileAsync(int claimId)
    {
        var claim = await GetClaimByIdAsync(claimId);
        if (claim == null)
            throw new ArgumentException($"Claim {claimId} not found");

        if (string.IsNullOrEmpty(claim.Form8849Path))
            throw new FileNotFoundException("Form 8849 not generated for this claim");

        if (!File.Exists(claim.Form8849Path))
            throw new FileNotFoundException("Form 8849 file not found on disk");

        var fileBytes = await File.ReadAllBytesAsync(claim.Form8849Path);
        var fileName = Path.GetFileName(claim.Form8849Path);

        return (fileBytes, fileName);
    }

    /// <summary>
    /// Get summary statistics for refund claims
    /// </summary>
    public async Task<RefundClaimSummary> GetClaimSummaryAsync(int? engagementId = null)
    {
        var query = _context.RefundClaims.AsQueryable();
        
        if (engagementId.HasValue)
            query = query.Where(c => c.EngagementId == engagementId.Value);

        var claims = await query.ToListAsync();

        var summary = new RefundClaimSummary
        {
            TotalClaims = claims.Count,
            DraftClaims = claims.Count(c => c.Status == ClaimStatus.Draft),
            SubmittedClaims = claims.Count(c => c.Status == ClaimStatus.Submitted || 
                                                  c.Status == ClaimStatus.UnderReview),
            ApprovedClaims = claims.Count(c => c.Status == ClaimStatus.Approved || 
                                                c.Status == ClaimStatus.Paid ||
                                                c.Status == ClaimStatus.PartiallyPaid),
            RejectedClaims = claims.Count(c => c.Status == ClaimStatus.Rejected),
            TotalClaimedAmount = claims.Sum(c => c.ClaimedAmount),
            TotalApprovedAmount = claims.Sum(c => c.ApprovedAmount),
            TotalPaidAmount = claims.Sum(c => c.PaidAmount),
            TotalPendingAmount = claims
                .Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.UnderReview)
                .Sum(c => c.ClaimedAmount),
            ClaimsByTaxType = claims.GroupBy(c => c.TaxType)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.ClaimedAmount)),
            ClaimsByRefundType = claims.GroupBy(c => c.RefundType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ClaimsByStatus = claims.GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return summary;
    }

    // Helper Methods

    private async Task<string> GenerateClaimNumberAsync(int taxYear)
    {
        var yearSuffix = taxYear.ToString().Substring(2);
        var count = await _context.RefundClaims
            .Where(c => c.TaxYear == taxYear)
            .CountAsync();
        
        return $"RC-{yearSuffix}-{(count + 1):D5}";
    }

    private string GetQuarter(DateTime date)
    {
        return date.Month switch
        {
            <= 3 => "Q1",
            <= 6 => "Q2",
            <= 9 => "Q3",
            _ => "Q4"
        };
    }

    private RefundType DetermineRefundType(List<TransactionRecord> transactions)
    {
        // Analyze transaction anomaly reasons to determine refund type
        var hasRateError = transactions.Any(t => 
            t.AnomalyReason?.Contains("Rate", StringComparison.OrdinalIgnoreCase) == true || 
            t.AnomalyReason?.Contains("Tax", StringComparison.OrdinalIgnoreCase) == true);
        
        var hasExemption = transactions.Any(t => 
            t.AnomalyReason?.Contains("Exemption", StringComparison.OrdinalIgnoreCase) == true);

        if (hasRateError) return RefundType.RateError;
        if (hasExemption) return RefundType.Exemption;
        
        return RefundType.Overpayment;
    }

    private string DetermineTaxTypeFromTransactions(List<TransactionRecord> transactions)
    {
        // Determine tax type from fuel type or product type
        var fuelTypes = transactions.Select(t => t.FuelType).Distinct().ToList();
        if (fuelTypes.Count == 1 && !string.IsNullOrEmpty(fuelTypes[0]))
        {
            return fuelTypes[0].Contains("Diesel", StringComparison.OrdinalIgnoreCase) ? "Fuel Tax" : "Fuel Tax";
        }
        return "Fuel Tax"; // Default
    }

    private string GenerateJustification(List<TransactionRecord> transactions, RefundType refundType)
    {
        var totalAmount = transactions.Sum(t => t.ClaimAmount ?? Math.Abs(t.Label_AdjustmentAmount ?? 0));
        var count = transactions.Count;

        return refundType switch
        {
            RefundType.RateError => 
                $"Overpayment due to incorrect tax rate applied to {count} transactions. " +
                $"Total refund amount: ${totalAmount:N2}. Transactions were charged at an incorrect " +
                $"rate that does not match the IRS-mandated rate for the tax period.",
            
            RefundType.Exemption => 
                $"Qualified exemption applied to {count} transactions. " +
                $"Total refund amount: ${totalAmount:N2}. Transactions qualify for tax exemption " +
                $"under applicable IRS regulations.",
            
            RefundType.Overpayment => 
                $"Tax overpayment identified across {count} transactions. " +
                $"Total refund amount: ${totalAmount:N2}. Analysis shows tax paid exceeds " +
                $"the amount required by IRS regulations.",
            
            _ => $"Refund claim for {count} transactions totaling ${totalAmount:N2}."
        };
    }

    private List<string> ExtractCitations(List<TransactionRecord> transactions)
    {
        var citations = new HashSet<string>();
        
        // Default citations based on common excise taxes
        citations.Add("IRS Publication 510, Excise Taxes (2024)");
        citations.Add("26 CFR 48.4081 - Tax on taxable fuel");
        
        // Extract citations from AI explanations if available
        foreach (var transaction in transactions)
        {
            if (!string.IsNullOrEmpty(transaction.GenAIExplanation))
            {
                // Simple citation extraction (could be enhanced with regex)
                if (transaction.GenAIExplanation.Contains("Publication"))
                    citations.Add("IRS Publication 510");
                if (transaction.GenAIExplanation.Contains("Form 720"))
                    citations.Add("Form 720, Quarterly Federal Excise Tax Return");
            }
        }
        
        return citations.ToList();
    }

    private decimal CalculateConfidence(List<TransactionRecord> transactions)
    {
        // Average AI confidence score from transactions
        var confidenceScores = transactions
            .Where(t => t.Confidence.HasValue)
            .Select(t => t.Confidence!.Value)
            .ToList();
        
        return confidenceScores.Any() 
            ? (decimal)confidenceScores.Average() 
            : 0.85m; // Default confidence
    }

    private string GenerateAIRecommendation(List<TransactionRecord> transactions, decimal amount)
    {
        var avgConfidence = CalculateConfidence(transactions);
        
        if (avgConfidence >= 0.90m && amount >= 1000)
            return "High confidence claim. Recommend immediate submission.";
        else if (avgConfidence >= 0.75m && amount >= 500)
            return "Good confidence claim. Review supporting documentation and submit.";
        else if (avgConfidence >= 0.60m)
            return "Moderate confidence. Consider additional review before submission.";
        else
            return "Low confidence. Recommend thorough manual review before proceeding.";
    }

    private string GenerateAIReasoning(List<TransactionRecord> transactions)
    {
        var reasons = new List<string>();
        
        var rateErrors = transactions.Count(t => t.AnomalyReason?.Contains("Rate", StringComparison.OrdinalIgnoreCase) == true);
        if (rateErrors > 0)
            reasons.Add($"{rateErrors} transactions with rate-related anomalies");
        
        var variances = transactions.Count(t => t.Label_AdjustmentAmount.HasValue && t.Label_AdjustmentAmount != 0);
        if (variances > 0)
            reasons.Add($"{variances} transactions with tax adjustments");
        
        var avgConfidence = CalculateConfidence(transactions);
        reasons.Add($"Average AI confidence: {avgConfidence:P1}");
        
        return string.Join(". ", reasons) + ".";
    }

    private int CalculatePriority(decimal amount, int transactionCount)
    {
        // Priority 1-5 (1 = highest)
        if (amount >= 10000) return 1;
        if (amount >= 5000) return 2;
        if (amount >= 1000) return 3;
        if (transactionCount >= 100) return 2;
        return 4;
    }

    private bool IsValidStatusTransition(ClaimStatus current, ClaimStatus next)
    {
        return (current, next) switch
        {
            (ClaimStatus.Draft, ClaimStatus.ReadyToFile) => true,
            (ClaimStatus.ReadyToFile, ClaimStatus.Submitted) => true,
            (ClaimStatus.ReadyToFile, ClaimStatus.Draft) => true,
            (ClaimStatus.Submitted, ClaimStatus.UnderReview) => true,
            (ClaimStatus.UnderReview, ClaimStatus.Approved) => true,
            (ClaimStatus.UnderReview, ClaimStatus.Rejected) => true,
            (ClaimStatus.Approved, ClaimStatus.Paid) => true,
            (ClaimStatus.Approved, ClaimStatus.PartiallyPaid) => true,
            (ClaimStatus.PartiallyPaid, ClaimStatus.Paid) => true,
            (ClaimStatus.Rejected, ClaimStatus.Appealed) => true,
            (ClaimStatus.Appealed, ClaimStatus.Approved) => true,
            (ClaimStatus.Appealed, ClaimStatus.Rejected) => true,
            _ => false
        };
    }
}
