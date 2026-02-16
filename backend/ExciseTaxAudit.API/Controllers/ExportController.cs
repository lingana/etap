using Microsoft.AspNetCore.Mvc;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text;

namespace ExciseTaxAudit.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly ExportService _exportService;

    public ExportController(ExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("form8849/{engagementId}/{taxPeriod}")]
    public async Task<IActionResult> ExportForm8849(int engagementId, string taxPeriod)
    {
        var userId = User.FindFirst("UserId")?.Value ?? "unknown";
        var userName = User.FindFirst("UserName")?.Value ?? "Unknown User";

        var csv = await _exportService.GenerateForm8849WorksheetAsync(
            engagementId, taxPeriod, userId, userName);

        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"Form8849_{taxPeriod}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("audit-report/{engagementId}")]
    public async Task<IActionResult> ExportAuditReport(int engagementId)
    {
        var userId = User.FindFirst("UserId")?.Value ?? "unknown";
        var userName = User.FindFirst("UserName")?.Value ?? "Unknown User";

        var report = await _exportService.GenerateAuditReportAsync(
            engagementId, userId, userName);

        var bytes = Encoding.UTF8.GetBytes(report);
        return File(bytes, "text/plain", $"AuditReport_{engagementId}_{DateTime.UtcNow:yyyyMMdd}.txt");
    }

    [HttpGet("transactions/{engagementId}/{status}")]
    public async Task<IActionResult> ExportTransactions(int engagementId, string status)
    {
        var userId = User.FindFirst("UserId")?.Value ?? "unknown";
        var userName = User.FindFirst("UserName")?.Value ?? "Unknown User";

        var csv = await _exportService.ExportTransactionDetailsAsync(
            engagementId, status, userId, userName);

        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"{status}Transactions_{engagementId}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
