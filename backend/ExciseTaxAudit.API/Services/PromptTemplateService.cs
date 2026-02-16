/**
 * Prompt Template Service
 * Generates optimized prompts for Azure OpenAI
 * Ensures consistency and cost efficiency
 */

using System;
using System.Text;
using ExciseTaxAudit.API.Services;

namespace ExciseTaxAudit.API.Services
{
    public interface IPromptTemplateService
    {
        string GetAnomalyExplanationPrompt(AnomalyExplanationRequest request);
        string GetSummaryPrompt(SummaryGenerationRequest request);
    }

    public class PromptTemplateService : IPromptTemplateService
    {
        /// <summary>
        /// Generate optimized prompt for anomaly explanation
        /// Uses structured data format for consistency
        /// </summary>
        public string GetAnomalyExplanationPrompt(AnomalyExplanationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var sb = new StringBuilder();

            sb.AppendLine("Analyze this fuel transaction anomaly and provide a brief explanation:");
            sb.AppendLine();
            sb.AppendLine($"Transaction ID: {request.TransactionId}");
            sb.AppendLine($"Date: {request.TransactionDate:yyyy-MM-dd}");
            sb.AppendLine($"Merchant: {request.MerchantName} ({request.MerchantState})");
            sb.AppendLine($"Fuel Type: {request.FuelType}");
            sb.AppendLine($"Quantity: {request.Quantity} gallons");
            sb.AppendLine($"Price: ${request.TransactionPrice:F2} per gallon");
            sb.AppendLine($"State Average: ${request.StateAveragePrice:F2} per gallon");
            sb.AppendLine($"Deviation: {((request.TransactionPrice - request.StateAveragePrice) / request.StateAveragePrice * 100):F1}%");
            sb.AppendLine($"Anomaly Score: {request.AnomalyScore:F2}/1.0");
            sb.AppendLine($"Supplier: {request.SupplierName}");
            sb.AppendLine();
            sb.AppendLine("Why might this transaction be flagged as anomalous?");

            return sb.ToString();
        }

        /// <summary>
        /// Generate optimized prompt for audit summary
        /// Provides context for batch analysis
        /// </summary>
        public string GetSummaryPrompt(SummaryGenerationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var sb = new StringBuilder();

            sb.AppendLine($"Generate an audit summary for period {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}:");
            sb.AppendLine();
            sb.AppendLine("AUDIT STATISTICS:");
            sb.AppendLine($"- Total Transactions Processed: {request.TotalTransactionsProcessed:N0}");
            sb.AppendLine($"- Anomalies Detected: {request.AnomaliesDetected} ({(double)request.AnomaliesDetected / request.TotalTransactionsProcessed * 100:F2}%)");
            sb.AppendLine($"- Duplicates Found: {request.DuplicatesFound}");
            sb.AppendLine($"- Average Price Deviation: {request.AveragePriceDeviation:F2}%");
            sb.AppendLine();
            
            if (request.HighRiskMerchants != null && request.HighRiskMerchants.Length > 0)
            {
                sb.AppendLine("HIGH-RISK MERCHANTS:");
                foreach (var merchant in request.HighRiskMerchants)
                {
                    sb.AppendLine($"- {merchant}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"SUMMARY TYPE: {request.SummaryType}");
            sb.AppendLine();
            sb.AppendLine("Provide key findings, risk assessment, and recommended actions.");
            sb.AppendLine("Keep response focused and actionable for compliance officers.");

            return sb.ToString();
        }
    }
}
