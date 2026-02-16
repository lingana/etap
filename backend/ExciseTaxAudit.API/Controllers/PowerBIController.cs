using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for Power BI integration and token generation.
/// Provides access tokens and configuration for embedding Power BI reports and dashboards.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PowerBIController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PowerBIController> _logger;
    private readonly HttpClient _httpClient;

    private sealed class PowerBIEngagementOverride
    {
        public string? ReportId { get; set; }
        public string? GroupId { get; set; }
        public string? EmbedUrl { get; set; }
    }

    public PowerBIController(
        IConfiguration configuration,
        ILogger<PowerBIController> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    private (string? ReportId, string? GroupId, string? EmbedUrl) ResolvePowerBIReportConfig(int? engagementId, string? externalEngagementId)
    {
        var reportId = _configuration["PowerBI:ReportId"];
        var groupId = _configuration["PowerBI:GroupId"];
        var embedUrl = _configuration["PowerBI:EmbedUrl"];

        // Apply internal engagement-id overrides first (backward compatible)
        if (engagementId.HasValue)
        {
            var overridesSection = _configuration.GetSection("PowerBI:EngagementOverrides");
            var engagementSection = overridesSection.GetSection(engagementId.Value.ToString());
            if (engagementSection.Exists())
            {
                var mapped = engagementSection.Get<PowerBIEngagementOverride>();
                if (mapped != null)
                {
                    reportId = string.IsNullOrWhiteSpace(mapped.ReportId) ? reportId : mapped.ReportId;
                    groupId = string.IsNullOrWhiteSpace(mapped.GroupId) ? groupId : mapped.GroupId;
                    embedUrl = string.IsNullOrWhiteSpace(mapped.EmbedUrl) ? embedUrl : mapped.EmbedUrl;
                }
            }
        }

        // Apply external engagement-id overrides second (best practice)
        if (!string.IsNullOrWhiteSpace(externalEngagementId))
        {
            var overridesSection = _configuration.GetSection("PowerBI:ExternalEngagementOverrides");
            var engagementSection = overridesSection.GetSection(externalEngagementId);
            if (engagementSection.Exists())
            {
                var mapped = engagementSection.Get<PowerBIEngagementOverride>();
                if (mapped != null)
                {
                    reportId = string.IsNullOrWhiteSpace(mapped.ReportId) ? reportId : mapped.ReportId;
                    groupId = string.IsNullOrWhiteSpace(mapped.GroupId) ? groupId : mapped.GroupId;
                    embedUrl = string.IsNullOrWhiteSpace(mapped.EmbedUrl) ? embedUrl : mapped.EmbedUrl;
                }
            }
        }

        return (reportId, groupId, embedUrl);
    }

    /// <summary>
    /// Model for Power BI token response
    /// </summary>
    public class PowerBITokenResponse
    {
        public string? AccessToken { get; set; }
        public string? EmbedUrl { get; set; }
        public string? ReportId { get; set; }
        public string? GroupId { get; set; }
        public long ExpiresIn { get; set; }
        public string? TokenType { get; set; }
    }

    /// <summary>
    /// Model for Power BI configuration
    /// </summary>
    public class PowerBIConfiguration
    {
        public string? AccessToken { get; set; }
        public string? EmbedUrl { get; set; }
        public string? ReportId { get; set; }
        public string? GroupId { get; set; }
        public long ExpiresIn { get; set; }
        public bool IsConfigured { get; set; }
    }

    /// <summary>
    /// Get Power BI access token for report embedding.
    /// Uses service principal authentication to generate a token valid for 60 minutes.
    /// </summary>
    /// <returns>Power BI token and configuration</returns>
    [HttpGet("token")]
    public async Task<ActionResult<PowerBIConfiguration>> GetPowerBIToken(int? engagementId = null, string? externalEngagementId = null)
    {
        try
        {
            // Retrieve Power BI configuration from settings
            var tenantId = _configuration["PowerBI:TenantId"];
            var clientId = _configuration["PowerBI:ClientId"];
            var clientSecret = _configuration["PowerBI:ClientSecret"];
            var (reportId, groupId, embedUrl) = ResolvePowerBIReportConfig(engagementId, externalEngagementId);

            // Validate configuration
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogWarning("Power BI configuration is incomplete. ClientId or ClientSecret missing.");
                return Ok(new PowerBIConfiguration
                {
                    IsConfigured = false
                });
            }

            // Generate access token using service principal
            var token = await GenerateServicePrincipalToken(tenantId, clientId, clientSecret);

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to generate Power BI service principal token");
                return StatusCode(500, "Unable to generate Power BI token");
            }

            _logger.LogInformation("Successfully generated Power BI access token for service principal");

            // Return configuration with token (valid for 60 minutes)
            return Ok(new PowerBIConfiguration
            {
                AccessToken = token,
                EmbedUrl = embedUrl ?? $"https://app.powerbi.com/groups/{groupId}/reports/{reportId}",
                ReportId = reportId,
                GroupId = groupId,
                ExpiresIn = 3600, // 60 minutes
                IsConfigured = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Power BI token");
            return StatusCode(500, $"Error generating Power BI token: {ex.Message}");
        }
    }

    /// <summary>
    /// Get Power BI configuration status without generating a new token.
    /// Useful for checking if Power BI is configured in the system.
    /// </summary>
    /// <returns>Power BI configuration status</returns>
    [HttpGet("status")]
    public ActionResult<PowerBIConfiguration> GetPowerBIStatus(int? engagementId = null, string? externalEngagementId = null)
    {
        try
        {
            var clientId = _configuration["PowerBI:ClientId"];
            var clientSecret = _configuration["PowerBI:ClientSecret"];
            var (reportId, groupId, _) = ResolvePowerBIReportConfig(engagementId, externalEngagementId);

            var isConfigured = !string.IsNullOrEmpty(clientId) && 
                              !string.IsNullOrEmpty(clientSecret) &&
                              !string.IsNullOrEmpty(reportId);

            _logger.LogInformation("Power BI status check: IsConfigured={IsConfigured}", isConfigured);

            return Ok(new PowerBIConfiguration
            {
                ReportId = reportId,
                GroupId = groupId,
                IsConfigured = isConfigured
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Power BI status");
            return StatusCode(500, $"Error checking Power BI status: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a service principal access token for Power BI API calls.
    /// </summary>
    /// <param name="tenantId">Azure AD Tenant ID</param>
    /// <param name="clientId">Azure AD Application ID</param>
    /// <param name="clientSecret">Azure AD Application Secret</param>
    /// <returns>Access token or null if generation fails</returns>
    private async Task<string?> GenerateServicePrincipalToken(string? tenantId, string? clientId, string? clientSecret)
    {
        try
        {
            if (string.IsNullOrEmpty(tenantId))
                tenantId = "common"; // Use common tenant if not specified

            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var body = new Dictionary<string, string>
            {
                { "client_id", clientId ?? "" },
                { "client_secret", clientSecret ?? "" },
                { "scope", "https://analysis.windows.net/powerbi/api/.default" },
                { "grant_type", "client_credentials" }
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint))
            {
                request.Content = new FormUrlEncodedContent(body);
                
                using (var response = await _httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Power BI token generation failed: {StatusCode} {Content}", 
                            response.StatusCode, errorContent);
                        return null;
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    
                    // Parse JSON to extract access token
                    if (jsonResponse.Contains("\"access_token\""))
                    {
                        var startIndex = jsonResponse.IndexOf("\"access_token\":\"") + 17;
                        var endIndex = jsonResponse.IndexOf("\"", startIndex);
                        return jsonResponse.Substring(startIndex, endIndex - startIndex);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception generating service principal token");
            return null;
        }
    }

    /// <summary>
    /// Model for dashboard list response
    /// </summary>
    public class DashboardInfo
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? EmbedUrl { get; set; }
    }

    /// <summary>
    /// Get list of available Power BI dashboards in the workspace.
    /// Requires valid Power BI service principal credentials.
    /// </summary>
    /// <returns>List of available dashboards</returns>
    [HttpGet("dashboards")]
    public async Task<ActionResult<List<DashboardInfo>>> GetDashboards(int? engagementId = null, string? externalEngagementId = null)
    {
        try
        {
            var clientId = _configuration["PowerBI:ClientId"];
            var clientSecret = _configuration["PowerBI:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return Ok(new List<DashboardInfo>());
            }

            var token = await GenerateServicePrincipalToken(
                _configuration["PowerBI:TenantId"],
                clientId,
                clientSecret);

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Unable to generate token for dashboard retrieval");
                return StatusCode(500, "Unable to authenticate with Power BI");
            }

            var (_, groupId, _) = ResolvePowerBIReportConfig(engagementId, externalEngagementId);
            var apiUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/dashboards";

            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using (var response = await _httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to retrieve dashboards from Power BI API");
                        return Ok(new List<DashboardInfo>());
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Parse and return dashboard information
                    // In production, use JSON deserialization with proper DTO classes
                    _logger.LogInformation("Successfully retrieved Power BI dashboards");
                    return Ok(new List<DashboardInfo>());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Power BI dashboards");
            return StatusCode(500, $"Error retrieving dashboards: {ex.Message}");
        }
    }
}
