using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for managing IRS connector settings and fetching real-time IRS data.
/// </summary>
[ApiController]
[Route("api/irs-connector")]
public class IRSConnectorController : ControllerBase
{
    private readonly IRSConnectorToggleService _toggleService;
    private readonly IIRSDataService _irsDataService;
    private readonly ILogger<IRSConnectorController> _logger;

    public IRSConnectorController(
        IRSConnectorToggleService toggleService,
        IIRSDataService irsDataService,
        ILogger<IRSConnectorController> logger)
    {
        _toggleService = toggleService;
        _irsDataService = irsDataService;
        _logger = logger;
    }

    /// <summary>
    /// Get current IRS connector mode (true = real APIs, false = fallback)
    /// </summary>
    [HttpGet("mode")]
    public ActionResult<bool> GetMode()
    {
        var mode = _toggleService.GetUseRealApis();
        _logger.LogInformation($"IRS connector mode requested: {mode}");
        return Ok(mode);
    }

    /// <summary>
    /// Set IRS connector mode (true = real APIs, false = fallback)
    /// </summary>
    [HttpPost("mode")]
    public IActionResult SetMode([FromBody] bool useRealApis)
    {
        _toggleService.SetUseRealApis(useRealApis);
        _logger.LogInformation($"IRS connector mode set to: {useRealApis}");
        return Ok(new { message = $"IRS connector mode set to {(useRealApis ? "real APIs" : "fallback")}" });
    }

    /// <summary>
    /// Get real-time tax rates from IRS for a specific tax type
    /// </summary>
    [HttpGet("tax-rates/{taxTypeName}")]
    public async Task<ActionResult<IRSTaxRateDataDto>> GetTaxRates(string taxTypeName)
    {
        try
        {
            var rateData = await _irsDataService.GetTaxRateDataAsync(taxTypeName);
            return Ok(rateData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch tax rates for {taxTypeName}");
            return StatusCode(500, new { error = "Failed to fetch tax rate data", details = ex.Message });
        }
    }

    /// <summary>
    /// Get regulatory updates from IRS for a specific tax type
    /// </summary>
    [HttpGet("regulatory-updates/{taxTypeName}")]
    public async Task<ActionResult<List<RegulatoryUpdateDto>>> GetRegulatoryUpdates(string taxTypeName)
    {
        try
        {
            var updates = await _irsDataService.GetRegulatoryUpdatesAsync(taxTypeName);
            return Ok(updates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch regulatory updates for {taxTypeName}");
            return Ok(new List<RegulatoryUpdateDto>()); // Return empty list on error
        }
    }

    /// <summary>
    /// Get IRS publication references for a tax type
    /// </summary>
    [HttpGet("publications/{taxTypeName}")]
    public async Task<ActionResult<List<IRSPublicationDto>>> GetPublications(string taxTypeName)
    {
        try
        {
            var publications = await _irsDataService.GetRelevantPublicationsAsync(taxTypeName);
            return Ok(publications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch IRS publications for {taxTypeName}");
            return Ok(new List<IRSPublicationDto>());
        }
    }
}

// DTOs
public class IRSTaxRateDataDto
{
    public string TaxType { get; set; } = string.Empty;
    public decimal CurrentRate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Source { get; set; } = "Database"; // "IRS" or "Database"
    public DateTime LastUpdated { get; set; }
    public List<TaxRateHistoryDto>? RateHistory { get; set; }
}

public class TaxRateHistoryDto
{
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class RegulatoryUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Impact { get; set; } = "low"; // "high", "medium", "low"
    public string? Link { get; set; }
}

public class IRSPublicationDto
{
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Url { get; set; }
}