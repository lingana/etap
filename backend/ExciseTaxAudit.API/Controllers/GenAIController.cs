using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ExciseTaxAudit.API.Services;

namespace ExciseTaxAudit.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GenAIController : ControllerBase
    {
        private readonly IAITaxInsightService _aiInsightService;
        private readonly ILogger<GenAIController> _logger;

        public GenAIController(
            IAITaxInsightService aiInsightService,
            ILogger<GenAIController> logger)
        {
            _aiInsightService = aiInsightService;
            _logger = logger;
        }

        /// <summary>
        /// Get AI-generated tax insights using RAG (Retrieval-Augmented Generation)
        /// </summary>
        [HttpPost("tax-insights")]
        public async Task<ActionResult<List<AITaxInsightDto>>> GetTaxInsights([FromBody] TaxInsightRequest request)
        {
            try
            {
                var insights = await _aiInsightService.GetTaxInsightsAsync(request.TaxType, request.Context);
                return Ok(insights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate AI insights for {request.TaxType}");
                return StatusCode(500, new { error = "Failed to generate AI insights", details = ex.Message });
            }
        }

        /// <summary>
        /// Get industry-specific tax recommendations using AI RAG
        /// </summary>
        [HttpPost("industry-recommendations")]
        public async Task<ActionResult<IndustryRecommendationDto>> GetIndustryRecommendations([FromBody] IndustryRecommendationRequest request)
        {
            try
            {
                var recommendations = await _aiInsightService.GetIndustryRecommendationsAsync(request.Industry, request.TaxType);
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate industry recommendations for {request.Industry} - {request.TaxType}");
                return StatusCode(500, new { error = "Failed to generate recommendations", details = ex.Message });
            }
        }
    }

    // Request DTOs
    public class TaxInsightRequest
    {
        public string TaxType { get; set; } = string.Empty;
        public string? Context { get; set; }
    }

    public class IndustryRecommendationRequest
    {
        public string Industry { get; set; } = string.Empty;
        public string TaxType { get; set; } = string.Empty;
    }

    // Response DTOs
    public class AITaxInsightDto
    {
        public string Category { get; set; } = string.Empty;
        public string Insight { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<string> Sources { get; set; } = new();
        public List<string>? RelevantRegulations { get; set; }
    }

    public class IndustryRecommendationDto
    {
        public string Industry { get; set; } = string.Empty;
        public string TaxType { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyRecommendations { get; set; } = new();
        public List<string> CommonPitfalls { get; set; } = new();
        public List<string> BestPractices { get; set; } = new();
    }
}
