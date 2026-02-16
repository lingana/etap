using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for file upload and ingestion of fuel transaction data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly ExcelParsingService _excelService;
    private readonly AnomalyDetectionService _anomalyService;
    private readonly GenAIExplanationService _genAIService;
    private readonly AuditContext _dbContext;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        ExcelParsingService excelService,
        AnomalyDetectionService anomalyService,
        GenAIExplanationService genAIService,
        AuditContext dbContext,
        ILogger<UploadController> logger)
    {
        _excelService = excelService;
        _anomalyService = anomalyService;
        _genAIService = genAIService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Upload and ingest an Excel or CSV file containing fuel transaction data.
    /// Clears all previous transaction data before processing the new upload.
    /// </summary>
    [HttpPost("excel")]
    public async Task<ActionResult<UploadJobStatus>> UploadExcelAsync([FromForm] IFormFile file, [FromForm] int? engagementId)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        var jobId = Guid.NewGuid().ToString();
        var jobStatus = new UploadJobStatus
        {
            JobId = jobId,
            FileName = file.FileName,
            Status = "Processing"
        };

        try
        {
            // Backward compatible behavior:
            // - When engagementId is supplied, replace transactions only for that engagement.
            // - When engagementId is missing, keep the legacy behavior and clear all transactions.
            if (engagementId.HasValue)
            {
                var existingRecords = _dbContext.TransactionRecords
                    .Where(t => t.EngagementId == engagementId.Value)
                    .ToList();
                _dbContext.TransactionRecords.RemoveRange(existingRecords);
            }
            else
            {
                var existingRecords = _dbContext.TransactionRecords.ToList();
                _dbContext.TransactionRecords.RemoveRange(existingRecords);
            }

            await _dbContext.SaveChangesAsync();

            _dbContext.UploadJobs.Add(jobStatus);
            await _dbContext.SaveChangesAsync();

            // Parse file (Excel or CSV)
            using (var stream = file.OpenReadStream())
            {
                List<TransactionRecord> records;
                
                // Handle both CSV and Excel files
                if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // For CSV, parse as text
                    using (var reader = new StreamReader(stream))
                    {
                        var csv = await reader.ReadToEndAsync();
                        records = ParseCsvToTransactionRecords(csv);
                    }
                }
                else
                {
                    // For Excel, use existing parser
                    records = await _excelService.ParseExcelAsync(stream);
                }
                
                jobStatus.TotalRecords = records.Count;

                // Use empty baseline since we just cleared the database
                var baselineRecords = new List<TransactionRecord>();

                // Process and score each record
                foreach (var record in records)
                {
                    record.EngagementId = engagementId;

                    var (score, reason) = _anomalyService.CalculateAnomalyScore(record, baselineRecords);
                    record.AnomalyScore = score;
                    record.AnomalyReason = reason;

                    // Calculate confidence based on anomaly score
                    if (score > 0.5f)
                    {
                        record.Confidence = _anomalyService.CalculateConfidence(score);
                        jobStatus.FlaggedRecords++;
                        record.Label_OverUnder = DeterminePredictedClaimType(record, score);
                    }

                    _dbContext.TransactionRecords.Add(record);
                    jobStatus.ProcessedRecords++;
                }

                await _dbContext.SaveChangesAsync();
                jobStatus.Status = "Completed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing upload {jobId}: {ex.Message}");
            jobStatus.Status = "Failed";
            jobStatus.ErrorMessage = ex.Message;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(jobStatus);
    }

    /// <summary>
    /// Get status of a specific upload job.
    /// </summary>
    [HttpGet("{jobId}/status")]
    public async Task<ActionResult<UploadJobStatus>> GetUploadStatusAsync(string jobId)
    {
        var job = await _dbContext.UploadJobs.FindAsync(jobId);
        if (job == null)
            return NotFound();

        return Ok(job);
    }

    /// <summary>
    /// Get preview of Excel or CSV file (first N rows) before upload.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<List<TransactionRecord>>> PreviewExcelAsync([FromForm] IFormFile file, [FromForm] int? engagementId, [FromQuery] int maxRows = 10)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        try
        {
            using (var stream = file.OpenReadStream())
            {
                List<TransactionRecord> records;
                
                // Handle both CSV and Excel files
                if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // For CSV, parse as text
                    using (var reader = new StreamReader(stream))
                    {
                        var csv = await reader.ReadToEndAsync();
                        records = ParseCsvToTransactionRecords(csv);
                    }
                }
                else
                {
                    // For Excel, use existing parser
                    records = await _excelService.ParseExcelAsync(stream);
                }
                
                return Ok(records.Take(maxRows).ToList());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error previewing file: {ex.Message}");
            return BadRequest($"Error reading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse CSV text to TransactionRecord list with proper quoted field handling.
    /// </summary>
    private List<TransactionRecord> ParseCsvToTransactionRecords(string csvContent)
    {
        var records = new List<TransactionRecord>();
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        if (lines.Length < 2)
            return records; // No data, just header

        // Parse header - handle quoted values
        var headers = ParseCsvLine(lines[0]);
        
        // Parse data rows
        var currentLine = "";
        for (int i = 1; i < lines.Length; i++)
        {
            currentLine += lines[i];
            
            // Check if line is complete (not in the middle of a quoted field)
            if (CountUnescapedQuotes(currentLine) % 2 == 0)
            {
                if (string.IsNullOrWhiteSpace(currentLine.Trim()))
                {
                    currentLine = "";
                    continue;
                }

                try
                {
                    var values = ParseCsvLine(currentLine);
                    var record = new TransactionRecord();
                    
                    // Map columns by header name
                    for (int j = 0; j < headers.Count && j < values.Count; j++)
                    {
                        var header = headers[j].Trim();
                        var value = values[j].Trim();
                        
                        switch (header)
                        {
                            case "RecordID": record.RecordID = long.TryParse(value, out var rid) ? rid : 0; break;
                            case "Branch": record.Branch = value; break;
                            case "Department": record.Department = value; break;
                            case "Entity": record.Entity = value; break;
                            case "Transaction Number": record.TransactionNumber = value; break;
                            case "Billing Code": record.BillingCode = value; break;
                            case "Card Number": record.CardNumberMask = value; break;
                            case "Fuel Platform": record.FuelPlatform = value; break;
                            case "Customer ID": record.CustomerID = value; break;
                            case "Fuel Type": record.FuelType = value; break;
                            case "Discrepancies": record.Discrepancies = value; break;
                            case "Employee ID": record.EmployeeID = value; break;
                            case "Employee Type": record.EmployeeType = value; break;
                            case "Transaction Date": record.TransactionDate = DateTime.TryParse(value, out var td) ? td : DateTime.MinValue; break;
                            case "Merchant Name": record.MerchantName = value; break;
                            case "Merchant City": record.MerchantCity = value; break;
                            case "Merchant State": record.MerchantState = value; break;
                            case "PADD/Region": record.PADDRegion = value; break;
                            case "Asset Number": record.AssetNumber = value; break;
                            case "Asset Description": record.AssetDescription = value; break;
                            case "Odometer": record.Odometer = int.TryParse(value, out var od) ? od : 0; break;
                            case "Product Description": record.ProductDescription = value; break;
                            case "Product Type Description": record.ProductType = value; break;
                            case "Quantity": record.Quantity = decimal.TryParse(value, out var qty) ? qty : 0; break;
                            case "Price Per Unit": record.PricePerUnit = decimal.TryParse(value, out var ppu) ? ppu : 0; break;
                            case "Net Cost": record.NetCost = decimal.TryParse(value, out var nc) ? nc : 0; break;
                            case "Gross Cost": record.GrossCost = decimal.TryParse(value, out var gc) ? gc : 0; break;
                            case "UOM": record.UOM = value; break;
                            case "Posted Date": record.PostedDate = DateTime.TryParse(value, out var pd) ? pd : DateTime.MinValue; break;
                            case "Month": record.Month = int.TryParse(value, out var m) ? m : 0; break;
                            case "Currency": record.Currency = value; break;
                            case "Card Type Flag": record.CardTypeFlag = value; break;
                            case "Cardholder Name": record.CardholderName = value; break;
                            case "Reporting Level": record.ReportingLevel = value; break;
                            case "MCC": record.MCC = value; break;
                            case "Merchant Code": record.MerchantCode = value; break;
                            case "Total Tax Amount": record.TotalTaxAmount = decimal.TryParse(value, out var tax) ? tax : 0; break;
                            case "Reviewed By": record.ReviewedBy = value; break;
                            case "Transaction Time Zone": record.TransactionTimezone = value; break;
                        }
                    }
                    
                    records.Add(record);
                    currentLine = "";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error parsing CSV row: {ex.Message}");
                    currentLine = "";
                }
            }
            else
            {
                currentLine += "\r\n";
            }
        }
        
        return records;
    }

    private List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.Trim().Trim('"'));
                current = "";
            }
            else
            {
                current += c;
            }
        }

        values.Add(current.Trim().Trim('"'));
        return values;
    }

    private int CountUnescapedQuotes(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                count++;
            }
        }
        return count;
    }

    private string DeterminePredictedClaimType(TransactionRecord record, float anomalyScore)
    {
        if (string.IsNullOrEmpty(record.AnomalyReason))
            return "NEEDS_REVIEW";

        var reason = record.AnomalyReason.ToLower();

        // Underpayment scenarios (recovery opportunity)
        if (reason.Contains("underpayment") ||
            reason.Contains("zero tax") ||
            reason.Contains("tax shortfall"))
            return "UNDER";

        // Overpayment scenarios (refund opportunity)
        if (reason.Contains("overpayment") ||
            reason.Contains("high tax rate") ||
            reason.Contains("potential refund"))
            return "OVER";

        // Price anomalies that need investigation
        if (reason.Contains("price") && (reason.Contains("above") || reason.Contains("higher")))
            return "OVER"; // Likely overpaid due to inflated prices

        if (reason.Contains("price") && (reason.Contains("below") || reason.Contains("lower")))
            return "NEEDS_REVIEW"; // Suspicious - needs manual investigation

        // Quantity and data quality issues
        if (reason.Contains("quantity") || reason.Contains("inconsistency") || reason.Contains("invalid"))
            return "NEEDS_REVIEW";

        // Default based on anomaly severity
        if (anomalyScore > 0.75f)
            return "NEEDS_REVIEW"; // High severity - needs investigation
        else if (anomalyScore > 0.6f)
            return "UNDER"; // Moderate severity - likely underpayment
        else
            return "NEEDS_REVIEW";
    }
}
