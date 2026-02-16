using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.DTOs;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Autonomous AI agent service for transaction review using Semantic Kernel
/// </summary>
public class AgenticReviewService
{
    private readonly AuditContext _context;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatService;
    private readonly TaxRatePlugin _taxRatePlugin;
    private readonly CalculationValidatorPlugin _calculationPlugin;
    private readonly HistoricalApprovalPlugin _historyPlugin;
    private readonly IRSTaxRateRAGService _ragService;

    public AgenticReviewService(
        AuditContext context,
        Kernel kernel,
        TaxRatePlugin taxRatePlugin,
        CalculationValidatorPlugin calculationPlugin,
        HistoricalApprovalPlugin historyPlugin,
        IRSTaxRateRAGService ragService)
    {
        _context = context;
        _kernel = kernel;
        
        // Try to get chat service, but don't crash if Azure OpenAI unavailable
        try
        {
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ WARNING: Chat completion service unavailable: {ex.Message}");
            _chatService = null; // Will be checked in ReviewTransactionAsync
        }
        
        _taxRatePlugin = taxRatePlugin;
        _calculationPlugin = calculationPlugin;
        _historyPlugin = historyPlugin;
        _ragService = ragService;
    }

    /// <summary>
    /// Performs autonomous agentic review of a flagged transaction
    /// </summary>
    public async Task<AgenticReviewResult> ReviewTransactionAsync(int recordId, AgenticReviewRequest? request = null)
    {
        // Check if Azure OpenAI is available
        if (_chatService == null)
        {
            throw new InvalidOperationException(
                "Azure OpenAI chat service is not configured. Please check appsettings.json for valid AzureOpenAI configuration (Endpoint, ApiKey, DeploymentId).");
        }
        
        request ??= new AgenticReviewRequest { RecordID = recordId };
        var stopwatch = Stopwatch.StartNew();
        var reasoningSteps = new List<ReasoningStep>();
        var stepNumber = 1;

        // Fetch transaction
        var transaction = await _context.TransactionRecords
            .FirstOrDefaultAsync(t => t.RecordID == recordId);

        if (transaction == null)
        {
            throw new Exception($"Transaction {recordId} not found");
        }

        // Step 1: Data Validation
        var validationResult = _calculationPlugin.ValidateTransactionData(
            transaction.Quantity,
            transaction.PricePerUnit,
            transaction.NetCost,
            transaction.TotalTaxAmount
        );

        reasoningSteps.Add(new ReasoningStep
        {
            StepNumber = stepNumber++,
            Action = "Validate Transaction Data",
            Thought = "First, I need to verify that all transaction data is complete and logically consistent",
            Result = validationResult,
            Timestamp = DateTime.UtcNow
        });

        // Step 2: Tax Rate Lookup with RAG
        IRSRegulatoryContext? regulatoryContext = null;
        decimal expectedTax;
        try
        {
            regulatoryContext = await _ragService.GetRegulatoryContextAsync(
                transaction.FuelType,
                transaction.MerchantState,
                transaction.Quantity
            );

            var rateInfo = regulatoryContext.RateInfo.FederalRate > 0 
                ? $"${regulatoryContext.RateInfo.FederalRate}/gal federal"
                : "Rate unavailable";
                
            if (regulatoryContext.RateInfo.StateRate > 0)
            {
                rateInfo += $" + ${regulatoryContext.RateInfo.StateRate}/gal state";
            }

            expectedTax = regulatoryContext.RateInfo.FederalRate * transaction.Quantity +
                             regulatoryContext.RateInfo.StateRate * transaction.Quantity;

            var citations = string.Join(", ", regulatoryContext.RateInfo.Citations);

            reasoningSteps.Add(new ReasoningStep
            {
                StepNumber = stepNumber++,
                Action = "Calculate Expected Tax (IRS RAG)",
                Thought = $"Looking up regulatory-backed tax rates for {transaction.FuelType} in {transaction.MerchantState}",
                Result = $"{rateInfo}, Expected tax: ${expectedTax:F2} for {transaction.Quantity} gallons\n" +
                         $"📚 Citations: {citations}\n" +
                         $"💡 Guidance: {regulatoryContext.SummaryGuidance}",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // Fallback to static plugin if RAG fails
            var expectedTaxRate = _taxRatePlugin.GetTaxRate(transaction.FuelType ?? "Diesel", transaction.MerchantState ?? "CA");
            expectedTax = _taxRatePlugin.CalculateExpectedTax(
                transaction.FuelType ?? "Diesel",
                transaction.MerchantState ?? "CA",
                transaction.Quantity
            );

            reasoningSteps.Add(new ReasoningStep
            {
                StepNumber = stepNumber++,
                Action = "Calculate Expected Tax (Fallback)",
                Thought = $"RAG service unavailable ({ex.Message}), using static rates for {transaction.FuelType} in {transaction.MerchantState}",
                Result = $"Tax rate: ${expectedTaxRate}/gal, Expected tax: ${expectedTax:F2} for {transaction.Quantity} gallons",
                Timestamp = DateTime.UtcNow
            });
        }

        // Step 3: Variance Analysis
        if (regulatoryContext == null)
        {
            // Fallback to static calculation if RAG wasn't successful
            expectedTax = _taxRatePlugin.CalculateExpectedTax(
                transaction.FuelType ?? "Diesel",
                transaction.MerchantState ?? "CA",
                transaction.Quantity
            );
        }

        var variance = _calculationPlugin.CalculateVariancePercentage(
            transaction.TotalTaxAmount,
            expectedTax
        );

        var isWithinSafeHarbor = _taxRatePlugin.IsWithinSafeHarbor(
            transaction.TotalTaxAmount,
            expectedTax
        );

        var varianceResult = $"Actual: ${transaction.TotalTaxAmount:F2}, Expected: ${expectedTax:F2}, " +
                     $"Variance: {variance:P2}, Within safe harbor: {isWithinSafeHarbor}";
        
        if (regulatoryContext?.RelevantRegulations != null && regulatoryContext.RelevantRegulations.Any())
        {
            varianceResult += $"\n📖 Safe harbor authority: {regulatoryContext.RelevantRegulations.FirstOrDefault(r => r.Content.Contains("10%") || r.Content.Contains("safe harbor"))?.Citation ?? "Rev. Proc. 2011-42"}";
        }

        reasoningSteps.Add(new ReasoningStep
        {
            StepNumber = stepNumber++,
            Action = "Analyze Tax Variance",
            Thought = "Comparing actual tax paid vs expected tax to determine if variance is significant",
            Result = varianceResult,
            Timestamp = DateTime.UtcNow
        });

        // Step 4: Historical Pattern Analysis
        string historicalAnalysis = "Skipped historical analysis";
        if (request.IncludeHistoricalAnalysis)
        {
            historicalAnalysis = await _historyPlugin.SearchSimilarApprovedCases(
                transaction.FuelType,
                transaction.MerchantState,
                variance
            );

            reasoningSteps.Add(new ReasoningStep
            {
                StepNumber = stepNumber++,
                Action = "Search Historical Approvals",
                Thought = "Checking if similar cases were previously approved to establish precedent",
                Result = historicalAnalysis,
                Timestamp = DateTime.UtcNow
            });
        }

        // Step 5: Risk Assessment
        var potentialRecovery = _calculationPlugin.CalculatePotentialRecovery(
            transaction.TotalTaxAmount,
            expectedTax
        );

        var riskLevel = _calculationPlugin.AssessRiskLevel(variance, potentialRecovery);

        var potentialRecoveryNote = potentialRecovery == 0 && transaction.TotalTaxAmount < expectedTax
            ? " (No potential recovery: underpayment detected)"
            : "";
        reasoningSteps.Add(new ReasoningStep
        {
            StepNumber = stepNumber++,
            Action = "Assess Risk Level",
            Thought = "Evaluating the audit risk of approving this claim based on variance and dollar amount",
            Result = $"Risk level: {riskLevel}, Potential recovery: ${potentialRecovery:F2}{potentialRecoveryNote}",
            Timestamp = DateTime.UtcNow
        });

        // Step 6: Determine Claim Schedule
        string claimSchedule;
        if (regulatoryContext != null && !string.IsNullOrEmpty(regulatoryContext.Form8849Schedule))
        {
            claimSchedule = regulatoryContext.Form8849Schedule;
        }
        else
        {
            var scheduleInt = _calculationPlugin.DetermineClaimSchedule(transaction.FuelType ?? "Diesel", isOffHighway: true);
            claimSchedule = scheduleInt.ToString();
        }

        var scheduleResult = $"Recommended Schedule {claimSchedule} for {transaction.FuelType} off-highway use";
        if (regulatoryContext != null && !string.IsNullOrEmpty(regulatoryContext.Form8849Schedule))
        {
            scheduleResult += $"\n📋 IRS Form 8849 guidance applied";
        }

        reasoningSteps.Add(new ReasoningStep
        {
            StepNumber = stepNumber++,
            Action = "Determine IRS Schedule",
            Thought = "Identifying the appropriate Form 8849 schedule for this fuel type and use case",
            Result = scheduleResult,
            Timestamp = DateTime.UtcNow
        });

        // Step 7: AI-Powered Final Assessment
        var finalAssessment = await GenerateFinalAssessmentAsync(
            transaction,
            expectedTax,
            variance,
            isWithinSafeHarbor,
            historicalAnalysis,
            riskLevel,
            potentialRecovery,
            regulatoryContext
        );

        reasoningSteps.Add(new ReasoningStep
        {
            StepNumber = stepNumber++,
            Action = "Generate Final Recommendation",
            Thought = "Synthesizing all analysis to make a final approve/reject recommendation",
            Result = finalAssessment.Recommendation,
            Timestamp = DateTime.UtcNow
        });

        stopwatch.Stop();

        // Update transaction status and review fields
        transaction.IsReviewed = true;
        transaction.ReviewedDate = DateTime.UtcNow;
        transaction.ReviewedByUserId = "auto";
        transaction.ReviewedByUserName = "AI Agent";
        // Set status based on recommendation
        if (finalAssessment.Recommendation.Contains("APPROVE", StringComparison.OrdinalIgnoreCase))
            transaction.Status = "APPROVED";
        else if (finalAssessment.Recommendation.Contains("REJECT", StringComparison.OrdinalIgnoreCase))
            transaction.Status = "REJECTED";
        else
            transaction.Status = "REVIEWED";
        // Optionally set claim amount and schedule
        if (potentialRecovery > 0)
            transaction.ClaimAmount = potentialRecovery;
        if (int.TryParse(claimSchedule, out int scheduleVal))
            transaction.ClaimSchedule = claimSchedule;
        await _context.SaveChangesAsync();

        return new AgenticReviewResult
        {
            RecordID = recordId,
            TransactionNumber = transaction.TransactionNumber,
            Recommendation = finalAssessment.Recommendation,
            ConfidenceScore = finalAssessment.Confidence,
            ReasoningSteps = reasoningSteps,
            FinalAssessment = finalAssessment.Assessment,
            RecommendedClaimAmount = potentialRecovery > 0 ? potentialRecovery : null,
            RecommendedSchedule = int.TryParse(claimSchedule, out int schedule) ? schedule : null,
            RiskLevel = riskLevel,
            ReviewedAt = DateTime.UtcNow,
            ProcessingTime = stopwatch.Elapsed
        };
    }

    private async Task<(string Recommendation, decimal Confidence, string Assessment)> GenerateFinalAssessmentAsync(
        Models.TransactionRecord transaction,
        decimal expectedTax,
        decimal variance,
        bool isWithinSafeHarbor,
        string historicalAnalysis,
        string riskLevel,
        decimal potentialRecovery,
        IRSRegulatoryContext? regulatoryContext = null)
    {
        var regulatoryInfo = "";
        if (regulatoryContext != null)
        {
            regulatoryInfo = $@"
IRS Regulatory Context:
- Tax Rates: ${regulatoryContext.RateInfo.FederalRate}/gal federal";
            
            if (regulatoryContext.RateInfo.StateRate > 0)
            {
                regulatoryInfo += $" + ${regulatoryContext.RateInfo.StateRate}/gal state";
            }
            
            regulatoryInfo += $@"
- Citations: {string.Join(", ", regulatoryContext.RateInfo.Citations)}
- Applicable Exemptions: {string.Join(", ", regulatoryContext.ApplicableExemptions)}
- Regulatory Guidance: {regulatoryContext.SummaryGuidance}
- Recommended Form 8849 Schedule: {regulatoryContext.Form8849Schedule}";
        }

        var prompt = $@"You are an expert IRS excise tax auditor reviewing a fuel transaction for potential refund claim approval.

Transaction Details:
- Merchant: {transaction.MerchantName}, {transaction.MerchantState}
- Fuel Type: {transaction.FuelType}
- Quantity: {transaction.Quantity:F2} gallons
- Tax Paid: ${transaction.TotalTaxAmount:F2}
- Expected Tax: ${expectedTax:F2}
- Variance: {variance:P2}
{regulatoryInfo}

Analysis:
- Safe Harbor Status: {(isWithinSafeHarbor ? "Within ±10% limit" : "Exceeds safe harbor")}
- Risk Level: {riskLevel}
- Potential Recovery: ${potentialRecovery:F2}
- Historical Pattern: {historicalAnalysis}

Based on IRS regulations and best practices, provide:
1. Recommendation: APPROVE, REJECT, or NEEDS_MANUAL_REVIEW
2. Confidence: 0.0 to 1.0 (how certain you are)
3. Brief assessment (2-3 sentences explaining your decision)

Format your response as:
RECOMMENDATION: [choice]
CONFIDENCE: [number]
ASSESSMENT: [explanation]";

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.3, // Low temperature for consistent, conservative decisions
            MaxTokens = 300
        };

        var response = await _chatService.GetChatMessageContentAsync(
            prompt,
            executionSettings,
            _kernel
        );

        var result = response.Content ?? "";

        // Parse the response
        var recommendation = ExtractField(result, "RECOMMENDATION") ?? "NEEDS_MANUAL_REVIEW";
        var confidenceStr = ExtractField(result, "CONFIDENCE") ?? "0.5";
        var assessment = ExtractField(result, "ASSESSMENT") ?? "Unable to generate assessment";

        decimal.TryParse(confidenceStr, out var confidence);

        return (recommendation, confidence, assessment);
    }

    private string? ExtractField(string text, string fieldName)
    {
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(fieldName.Length + 1).Trim();
            }
        }
        return null;
    }
}
