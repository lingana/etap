using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExciseTaxAudit.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditsController : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly IAuthService _authService;
        private readonly ILogger<AuditsController> _logger;

        public AuditsController(
            IAuditService auditService,
            IAuthService authService,
            ILogger<AuditsController> logger)
        {
            _auditService = auditService;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyAudits()
        {
            try
            {
                var user = _authService.GetCurrentUser(User);
                if (user == null)
                    return Unauthorized();

                var audits = await _auditService.GetAuditsByUserAsync(user.Id, user.RoleName);
                return Ok(audits);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting audits: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAudit(int id)
        {
            try
            {
                var audit = await _auditService.GetAuditByIdAsync(id);
                if (audit == null)
                    return NotFound();

                return Ok(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting audit {id}: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAudit([FromBody] CreateAuditRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = _authService.GetCurrentUser(User);
                if (user == null)
                    return Unauthorized();

                var audit = await _auditService.CreateAuditAsync(request, user.Id);
                return CreatedAtAction(nameof(GetAudit), new { id = audit.Id }, audit);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating audit: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateAuditStatus(int id, [FromBody] UpdateAuditStatusRequest request)
        {
            try
            {
                var audit = await _auditService.UpdateAuditStatusAsync(id, request.Status);
                if (audit == null)
                    return NotFound();

                return Ok(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating audit status: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignAudit(int id, [FromBody] AssignAuditRequest request)
        {
            try
            {
                var audit = await _auditService.AssignAuditAsync(id, request.AssignedToUserId);
                if (audit == null)
                    return NotFound();

                return Ok(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning audit: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPut("{id}/stats")]
        public async Task<IActionResult> UpdateAuditStats(int id, [FromBody] UpdateAuditStatsRequest request)
        {
            try
            {
                var audit = await _auditService.UpdateAuditStatsAsync(
                    id,
                    request.TotalRecords,
                    request.FlaggedRecords,
                    request.ReviewedRecords,
                    request.PotentialRecovery);

                if (audit == null)
                    return NotFound();

                return Ok(audit);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating audit stats: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAudit(int id)
        {
            try
            {
                var success = await _auditService.DeleteAuditAsync(id);
                if (!success)
                    return NotFound();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting audit: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpGet("{auditId}/comments")]
        public async Task<IActionResult> GetAuditComments(int auditId)
        {
            try
            {
                var comments = await _auditService.GetAuditCommentsAsync(auditId);
                return Ok(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting comments: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost("{auditId}/comments")]
        public async Task<IActionResult> AddComment(int auditId, [FromBody] AddCommentRequest request)
        {
            try
            {
                var user = _authService.GetCurrentUser(User);
                if (user == null)
                    return Unauthorized();

                var comment = await _auditService.AddCommentAsync(
                    auditId,
                    user.Id,
                    request.Comment,
                    request.IsInternal);

                return Ok(comment);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding comment: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }
    }

    public class AssignAuditRequest
    {
        public int? AssignedToUserId { get; set; }
    }

    public class UpdateAuditStatsRequest
    {
        public int TotalRecords { get; set; }
        public int FlaggedRecords { get; set; }
        public int ReviewedRecords { get; set; }
        public decimal PotentialRecovery { get; set; }
    }

    public class AddCommentRequest
    {
        public string Comment { get; set; }
        public bool IsInternal { get; set; }
    }
}
