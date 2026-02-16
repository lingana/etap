namespace ExciseTaxAudit.API.DTOs;

/// <summary>
/// Request for workflow transaction review.
/// </summary>
public class WorkflowReviewRequest
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string? Comments { get; set; }
}

/// <summary>
/// Request for workflow transaction approval.
/// </summary>
public class WorkflowApprovalRequest
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string? ClaimSchedule { get; set; }
    public decimal? ClaimAmount { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Request for workflow transaction rejection.
/// </summary>
public class WorkflowRejectionRequest
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string Reason { get; set; } = "";
}

/// <summary>
/// Request for marking transactions as claimed.
/// </summary>
public class WorkflowClaimRequest
{
    public List<long> TransactionIds { get; set; } = new();
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserRole { get; set; } = "";
    public string TaxPeriod { get; set; } = "";
    public string? Comments { get; set; }
}
