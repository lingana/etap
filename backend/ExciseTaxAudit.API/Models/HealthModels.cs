namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Health status response model
/// </summary>
public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime LastCheckedAt { get; set; }
    public string ErrorMessage { get; set; }
}