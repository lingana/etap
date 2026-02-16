/**
 * Extended Models for Gen AI Integration
 * Additional DTOs and domain models
 */

using System;
using System.Collections.Generic;

namespace ExciseTaxAudit.API.Models
{
    /// <summary>
    /// Anomaly with detailed explanation and metadata
    /// </summary>
    public class DetailedAnomalyResult
    {
        public string TransactionId { get; set; }
        public bool IsAnomaly { get; set; }
        public double AnomalyScore { get; set; }
        public double Confidence { get; set; }
        
        // Gen AI Generated Content
        public string ShortExplanation { get; set; } // 1-2 sentences
        public string DetailedExplanation { get; set; } // Full paragraph
        public List<string> RiskFactors { get; set; } // Bullet points
        public string RecommendedAction { get; set; } // What to do
        
        // Metadata
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; } // AI model version
        public bool FromCache { get; set; }
        public double CacheCostSaved { get; set; } // Dollars saved
    }

    /// <summary>
    /// Audit period summary with Gen AI insights
    /// </summary>
    public class AuditPeriodSummary
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SummaryType { get; set; } // quick, standard, detailed
        
        // Statistics
        public int TotalTransactions { get; set; }
        public int AnomaliesDetected { get; set; }
        public double AnomalyPercentage { get; set; }
        public decimal TotalQuestionableAmount { get; set; }
        
        // Gen AI Generated Content
        public string ExecutiveSummary { get; set; }
        public List<string> KeyFindings { get; set; }
        public List<string> RiskAreas { get; set; }
        public List<string> RecommendedActions { get; set; }
        
        // Top anomalies
        public List<DetailedAnomalyResult> TopAnomalies { get; set; }
    }

    /// <summary>
    /// Merchant risk profile with AI assessment
    /// </summary>
    public class MerchantRiskProfile
    {
        public string MerchantId { get; set; }
        public string MerchantName { get; set; }
        public string State { get; set; }
        
        // Risk Metrics
        public int TotalTransactions { get; set; }
        public int AnomalousTransactions { get; set; }
        public double AnomalyRate { get; set; }
        public double AverageAnomalyScore { get; set; }
        
        // AI Assessment
        public string RiskLevel { get; set; } // Low, Medium, High, Critical
        public string RiskAssessment { get; set; } // Gen AI generated
        public List<string> HistoricalPatterns { get; set; }
        public List<string> RecommendedActions { get; set; }
        
        // Trend
        public double TrendDirection { get; set; } // -1 to 1 (improving to worsening)
    }

    /// <summary>
    /// Compliance report with Gen AI insights
    /// </summary>
    public class ComplianceReport
    {
        public string ReportId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime ReportPeriodStart { get; set; }
        public DateTime ReportPeriodEnd { get; set; }
        
        // Executive Summary (Gen AI)
        public string ExecutiveSummary { get; set; }
        
        // Findings
        public List<ComplianceFinding> Findings { get; set; }
        
        // Risk Assessment (Gen AI)
        public string OverallRiskAssessment { get; set; }
        public List<string> CriticalIssues { get; set; }
        
        // Recommendations (Gen AI)
        public List<string> ImmediateActions { get; set; }
        public List<string> ShortTermActions { get; set; }
        public List<string> LongTermActions { get; set; }
    }

    /// <summary>
    /// Individual compliance finding
    /// </summary>
    public class ComplianceFinding
    {
        public string FindingId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; } // Low, Medium, High, Critical
        public int AffectedTransactions { get; set; }
        public decimal FinancialImpact { get; set; }
        
        // Gen AI Content
        public string Analysis { get; set; }
        public string RecommendedAction { get; set; }
    }

    /// <summary>
    /// API cost tracking and billing
    /// </summary>
    public class ApiUsageCost
    {
        public string ApiName { get; set; }
        public DateTime Date { get; set; }
        public int CallCount { get; set; }
        public int TotalTokens { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public decimal CostUsd { get; set; }
        public int CacheHits { get; set; }
        public decimal CostSavedByCache { get; set; }
    }

    /// <summary>
    /// Monthly cost summary
    /// </summary>
    public class MonthlyCostSummary
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal AzureOpenAICost { get; set; }
        public decimal ComputeCost { get; set; }
        public decimal StorageCost { get; set; }
        public decimal TotalCost { get; set; }
        
        public decimal CostSavedByCache { get; set; }
        public double CacheHitRate { get; set; }
        public decimal CostPerExplanation { get; set; }
        
        public List<ApiUsageCost> DailyBreakdown { get; set; }
    }

    /// <summary>
    /// System health and performance metrics
    /// </summary>
    public class SystemHealthMetrics
    {
        public DateTime MeasuredAt { get; set; }
        
        // Availability
        public bool IsOpenAIHealthy { get; set; }
        public int OpenAIResponseTimeMs { get; set; }
        
        public bool IsMLEndpointHealthy { get; set; }
        public int MLResponseTimeMs { get; set; }
        
        public bool IsDatabaseHealthy { get; set; }
        
        // Performance
        public int RequestsPerMinute { get; set; }
        public double CacheHitRate { get; set; }
        public double ErrorRate { get; set; }
        
        // Capacity
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public int ActiveConnections { get; set; }
    }

    /// <summary>
    /// Transaction with enhanced anomaly detection results
    /// </summary>
    public class EnhancedTransactionResult
    {
        public string TransactionId { get; set; }
        public FuelTransaction OriginalTransaction { get; set; }
        
        // Anomaly Detection Results
        public bool IsAnomaly { get; set; }
        public double AnomalyScore { get; set; }
        public double Confidence { get; set; }
        
        // Gen AI Explanation
        public string Explanation { get; set; }
        public bool ExplanationFromCache { get; set; }
        
        // Additional Analysis
        public List<string> SimilarAnomalies { get; set; }
        public MerchantRiskProfile MerchantRiskContext { get; set; }
        
        // Processing Details
        public DateTime ProcessedAt { get; set; }
        public long ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Batch job for processing multiple transactions
    /// </summary>
    public class BatchJob
    {
        public string JobId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        public int TotalTransactions { get; set; }
        public int ProcessedCount { get; set; }
        public int FailureCount { get; set; }
        
        public string Status { get; set; } // Pending, Running, Completed, Failed
        public string ErrorMessage { get; set; }
        
        public List<EnhancedTransactionResult> Results { get; set; }
    }
}
