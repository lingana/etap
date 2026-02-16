namespace ExciseTaxAudit.API.DTOs;

/// <summary>
/// Result from AI agent's autonomous transaction review
/// </summary>
public class AgenticReviewResult
{
    public int RecordID { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty; // APPROVE, REJECT, NEEDS_REVIEW
    public decimal ConfidenceScore { get; set; } // 0.0 to 1.0
    public List<ReasoningStep> ReasoningSteps { get; set; } = new();
    public string FinalAssessment { get; set; } = string.Empty;
    public decimal? RecommendedClaimAmount { get; set; }
    public int? RecommendedSchedule { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// Individual reasoning step from agent's thought process
/// </summary>
public class ReasoningStep
{
    public int StepNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Thought { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Request to trigger agentic review
/// </summary>
public class AgenticReviewRequest
{
    public int RecordID { get; set; }
    public bool IncludeHistoricalAnalysis { get; set; } = true;
    public bool AutoApplyIfHighConfidence { get; set; } = false;
    public decimal ConfidenceThreshold { get; set; } = 0.85m;
}
