using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for managing Power BI integration, token generation, and report management.
/// Handles authentication with Azure AD and communication with Power BI REST APIs.
/// </summary>
public interface IPowerBIService
{
    /// <summary>
    /// Generate a service principal access token for Power BI operations.
    /// </summary>
    Task<string?> GenerateServicePrincipalTokenAsync();

    /// <summary>
    /// Get Power BI report embed configuration.
    /// </summary>
    Task<PowerBIEmbedConfig?> GetReportEmbedConfigAsync();

    /// <summary>
    /// Get list of available reports in the Power BI workspace.
    /// </summary>
    Task<List<PowerBIReportInfo>> GetReportsAsync(string token);

    /// <summary>
    /// Get list of available dashboards in the Power BI workspace.
    /// </summary>
    Task<List<PowerBIDashboardInfo>> GetDashboardsAsync(string token);

    /// <summary>
    /// Check if Power BI is configured and available.
    /// </summary>
    bool IsPowerBIConfigured();
}

/// <summary>
/// Power BI report information
/// </summary>
public class PowerBIReportInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? WebUrl { get; set; }
    public string? EmbedUrl { get; set; }
}

/// <summary>
/// Power BI dashboard information
/// </summary>
public class PowerBIDashboardInfo
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? WebUrl { get; set; }
    public string? EmbedUrl { get; set; }
}

/// <summary>
/// Power BI embed configuration
/// </summary>
public class PowerBIEmbedConfig
{
    public string? AccessToken { get; set; }
    public string? EmbedUrl { get; set; }
    public string? ReportId { get; set; }
    public string? GroupId { get; set; }
    public long ExpiresIn { get; set; } = 3600; // 60 minutes default
}

/// <summary>
/// Implementation of Power BI service for report embedding and management.
/// </summary>
public class PowerBIService : IPowerBIService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PowerBIService> _logger;
    private readonly HttpClient _httpClient;

    public PowerBIService(
        IConfiguration configuration,
        ILogger<PowerBIService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Generate a service principal access token for Power BI API calls.
    /// </summary>
    public async Task<string?> GenerateServicePrincipalTokenAsync()
    {
        try
        {
            var tenantId = _configuration["PowerBI:TenantId"] ?? "common";
            var clientId = _configuration["PowerBI:ClientId"];
            var clientSecret = _configuration["PowerBI:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogWarning("Power BI credentials not configured");
                return null;
            }

            var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var body = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
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
                    using (var doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
                        {
                            var token = tokenElement.GetString();
                            _logger.LogInformation("Successfully generated Power BI service principal token");
                            return token;
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception generating Power BI service principal token");
            return null;
        }
    }

    /// <summary>
    /// Get Power BI report embed configuration.
    /// </summary>
    public async Task<PowerBIEmbedConfig?> GetReportEmbedConfigAsync()
    {
        try
        {
            if (!IsPowerBIConfigured())
            {
                _logger.LogWarning("Power BI not configured");
                return null;
            }

            var token = await GenerateServicePrincipalTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to generate Power BI token for embed config");
                return null;
            }

            var reportId = _configuration["PowerBI:ReportId"];
            var groupId = _configuration["PowerBI:GroupId"];
            var embedUrl = _configuration["PowerBI:EmbedUrl"];

            return new PowerBIEmbedConfig
            {
                AccessToken = token,
                ReportId = reportId,
                GroupId = groupId,
                EmbedUrl = embedUrl ?? $"https://app.powerbi.com/groups/{groupId}/reports/{reportId}",
                ExpiresIn = 3600
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Power BI embed configuration");
            return null;
        }
    }

    /// <summary>
    /// Get list of available reports in the Power BI workspace.
    /// </summary>
    public async Task<List<PowerBIReportInfo>> GetReportsAsync(string token)
    {
        var reports = new List<PowerBIReportInfo>();

        try
        {
            var groupId = _configuration["PowerBI:GroupId"];
            var apiUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/reports";

            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using (var response = await _httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to retrieve reports from Power BI API");
                        return reports;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(content))
                    {
                        if (doc.RootElement.TryGetProperty("value", out var valueElement))
                        {
                            foreach (var item in valueElement.EnumerateArray())
                            {
                                var report = new PowerBIReportInfo();

                                if (item.TryGetProperty("id", out var idElement))
                                    report.Id = idElement.GetString();
                                if (item.TryGetProperty("name", out var nameElement))
                                    report.Name = nameElement.GetString();
                                if (item.TryGetProperty("webUrl", out var webUrlElement))
                                    report.WebUrl = webUrlElement.GetString();
                                if (item.TryGetProperty("embedUrl", out var embedUrlElement))
                                    report.EmbedUrl = embedUrlElement.GetString();

                                reports.Add(report);
                            }
                        }
                    }

                    _logger.LogInformation("Successfully retrieved {Count} Power BI reports", reports.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Power BI reports");
        }

        return reports;
    }

    /// <summary>
    /// Get list of available dashboards in the Power BI workspace.
    /// </summary>
    public async Task<List<PowerBIDashboardInfo>> GetDashboardsAsync(string token)
    {
        var dashboards = new List<PowerBIDashboardInfo>();

        try
        {
            var groupId = _configuration["PowerBI:GroupId"];
            var apiUrl = $"https://api.powerbi.com/v1.0/myorg/groups/{groupId}/dashboards";

            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using (var response = await _httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to retrieve dashboards from Power BI API");
                        return dashboards;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(content))
                    {
                        if (doc.RootElement.TryGetProperty("value", out var valueElement))
                        {
                            foreach (var item in valueElement.EnumerateArray())
                            {
                                var dashboard = new PowerBIDashboardInfo();

                                if (item.TryGetProperty("id", out var idElement))
                                    dashboard.Id = idElement.GetString();
                                if (item.TryGetProperty("displayName", out var displayNameElement))
                                    dashboard.DisplayName = displayNameElement.GetString();
                                if (item.TryGetProperty("webUrl", out var webUrlElement))
                                    dashboard.WebUrl = webUrlElement.GetString();
                                if (item.TryGetProperty("embedUrl", out var embedUrlElement))
                                    dashboard.EmbedUrl = embedUrlElement.GetString();

                                dashboards.Add(dashboard);
                            }
                        }
                    }

                    _logger.LogInformation("Successfully retrieved {Count} Power BI dashboards", dashboards.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Power BI dashboards");
        }

        return dashboards;
    }

    /// <summary>
    /// Check if Power BI is properly configured.
    /// </summary>
    public bool IsPowerBIConfigured()
    {
        var clientId = _configuration["PowerBI:ClientId"];
        var clientSecret = _configuration["PowerBI:ClientSecret"];
        var reportId = _configuration["PowerBI:ReportId"];

        var configured = !string.IsNullOrEmpty(clientId) &&
                        !string.IsNullOrEmpty(clientSecret) &&
                        !string.IsNullOrEmpty(reportId);

        return configured;
    }
}
