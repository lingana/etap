namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Tracks the status of file upload and ingestion jobs.
/// </summary>
public class UploadJobStatus
{
    public string JobId { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int FlaggedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SourceBlobUrl { get; set; }
}
