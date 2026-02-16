using Microsoft.Extensions.Configuration;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service to manage IRS connector mode (real APIs vs fallback)
/// </summary>
public class IRSConnectorToggleService
{
    private bool _useRealApis;

    public IRSConnectorToggleService(IConfiguration configuration)
    {
        // Initialize from config, default to false
        _useRealApis = configuration.GetValue<bool>("IRSConnector:UseRealApis", false);
    }

    public bool GetUseRealApis() => _useRealApis;

    public void SetUseRealApis(bool value)
    {
        _useRealApis = value;
    }
}