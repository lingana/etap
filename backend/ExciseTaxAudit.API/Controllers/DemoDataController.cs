using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// Generate demo/synthetic data for testing and demonstrations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DemoDataController : ControllerBase
{
    private readonly DemoDataGenerator _generator;

    public DemoDataController()
    {
        _generator = new DemoDataGenerator();
    }

    /// <summary>
    /// Generate demo CSV file with realistic fuel transaction data
    /// </summary>
    /// <param name="recordCount">Number of transactions to generate (default: 500)</param>
    /// <returns>CSV file download</returns>
    [HttpGet("generate-csv")]
    public IActionResult GenerateDemoCSV([FromQuery] int recordCount = 500)
    {
        if (recordCount < 10 || recordCount > 10000)
            return BadRequest("Record count must be between 10 and 10,000");

        var csvContent = _generator.GenerateDemoCSV(recordCount);
        var bytes = Encoding.UTF8.GetBytes(csvContent);

        return File(bytes, "text/csv", $"demo-transactions-{recordCount}.csv");
    }

    /// <summary>
    /// Get information about the demo data generator
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            Description = "Demo data generator for fuel tax audit testing",
            Features = new[]
            {
                "75% normal transactions with realistic prices and tax",
                "10% price anomalies (30-70% higher than normal)",
                "5% tax anomalies (zero or incorrect tax amounts)",
                "5% quantity anomalies (60-160 gallons vs normal 10-40)",
                "5% price dip anomalies (20-40% lower than normal)",
                "Varied merchants, states, fuel types, and date ranges",
                "Realistic tax rates per state (CA, TX, NY, IL, FL, PA, OH)"
            },
            Usage = "GET /api/demodata/generate-csv?recordCount=500",
            DefaultRecordCount = 500,
            MinRecordCount = 10,
            MaxRecordCount = 10000
        });
    }
}
