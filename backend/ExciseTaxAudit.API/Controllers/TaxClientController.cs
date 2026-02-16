using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExciseTaxAudit.API.Services;
using ExciseTaxAudit.API.DTOs;

namespace ExciseTaxAudit.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaxClientController : ControllerBase
    {
        private readonly ITaxClientService _taxClientService;

        public TaxClientController(ITaxClientService taxClientService)
        {
            _taxClientService = taxClientService;
        }

        /// <summary>
        /// Get all tax types
        /// </summary>
        [HttpGet("tax-types")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TaxTypeDto>>> GetAllTaxTypes()
        {
            var taxTypes = await _taxClientService.GetAllTaxTypesAsync();
            return Ok(taxTypes);
        }

        /// <summary>
        /// Get clients by tax type
        /// </summary>
        [HttpGet("clients/by-tax-type/{taxTypeId}")]
        public async Task<ActionResult<List<ClientDto>>> GetClientsByTaxType(int taxTypeId)
        {
            var clients = await _taxClientService.GetClientsByTaxTypeAsync(taxTypeId);
            return Ok(clients);
        }

        /// <summary>
        /// Get engagements by client
        /// </summary>
        [HttpGet("engagements/by-client/{clientId}")]
        public async Task<ActionResult<List<EngagementDto>>> GetEngagementsByClient(int clientId)
        {
            var engagements = await _taxClientService.GetEngagementsByClientAsync(clientId);
            return Ok(engagements);
        }

        /// <summary>
        /// Get engagement by ID
        /// </summary>
        [HttpGet("engagements/{engagementId}")]
        public async Task<ActionResult<EngagementDto>> GetEngagementById(int engagementId)
        {
            var engagement = await _taxClientService.GetEngagementByIdAsync(engagementId);
            if (engagement == null)
                return NotFound($"Engagement with ID {engagementId} not found");
            return Ok(engagement);
        }

        /// <summary>
        /// Create a new client
        /// </summary>
        [HttpPost("clients")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<ClientDto>> CreateClient([FromBody] CreateClientRequest request)
        {
            var client = await _taxClientService.CreateClientAsync(request);
            return CreatedAtAction(nameof(GetClientsByTaxType), new { taxTypeId = client.TaxTypeId }, client);
        }

        /// <summary>
        /// Create a new engagement
        /// </summary>
        [HttpPost("engagements")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<EngagementDto>> CreateEngagement([FromBody] CreateEngagementRequest request)
        {
            var engagement = await _taxClientService.CreateEngagementAsync(request);
            return CreatedAtAction(nameof(GetEngagementById), new { engagementId = engagement.Id }, engagement);
        }
    }
}
