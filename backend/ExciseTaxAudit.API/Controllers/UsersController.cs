using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Controllers;

/// <summary>
/// API endpoints for user and team management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AuditContext _context;
    private readonly IAuthService _authService;
    private readonly AuditTrailService _auditTrail;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AuditContext context,
        IAuthService authService,
        AuditTrailService auditTrail,
        ILogger<UsersController> logger)
    {
        _context = context;
        _authService = authService;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    /// <summary>
    /// Get all team members.
    /// GET api/users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TeamMemberDto>>> GetAll()
    {
        try
        {
            var users = await _context.Users.ToListAsync();
            var dtos = new List<TeamMemberDto>();

            foreach (var user in users)
            {
                var assignedCases = await _context.AuditCases
                    .CountAsync(c => c.AssignedToUserId == user.Id);
                var reviewedTxns = await _context.AuditLogs
                    .CountAsync(l => l.UserName == user.Username && l.Action == "REVIEW");

                dtos.Add(MapToTeamMember(user, assignedCases, reviewedTxns));
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }

    /// <summary>
    /// Get a specific user by ID.
    /// GET api/users/5
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TeamMemberDto>> GetById(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { error = $"User {id} not found" });

            var assignedCases = await _context.AuditCases
                .CountAsync(c => c.AssignedToUserId == user.Id);
            var reviewedTxns = await _context.AuditLogs
                .CountAsync(l => l.UserName == user.Username && l.Action == "REVIEW");

            return Ok(MapToTeamMember(user, assignedCases, reviewedTxns));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new { error = "Failed to retrieve user" });
        }
    }

    /// <summary>
    /// Invite (create) a new team member.
    /// POST api/users/invite
    /// </summary>
    [HttpPost("invite")]
    public async Task<ActionResult<TeamMemberDto>> InviteUser([FromBody] InviteUserRequest request)
    {
        try
        {
            // Validate email uniqueness
            var exists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (exists)
                return BadRequest(new { error = "A user with this email already exists" });

            // Parse role from string number
            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                // Try parsing as integer
                if (int.TryParse(request.Role, out var roleInt) && Enum.IsDefined(typeof(UserRole), roleInt))
                    role = (UserRole)roleInt;
                else
                    role = UserRole.Viewer;
            }

            // Generate a username from email
            var username = request.Email.Split('@')[0].ToLower();
            var baseUsername = username;
            var counter = 1;
            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                username = $"{baseUsername}{counter++}";
            }

            var user = new User
            {
                Username = username,
                Email = request.Email,
                FirstName = request.FirstName ?? "",
                LastName = request.LastName ?? "",
                Role = role,
                IsActive = true,
                PasswordHash = _authService.HashPassword("Welcome123!"), // Default password
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log the action
            await _auditTrail.LogActionAsync(new AuditLog
            {
                Action = "USER_INVITE",
                EntityType = "USER",
                EntityId = user.Id.ToString(),
                UserName = User.Identity?.Name ?? "System",
                Comments = $"Invited {user.Email} as {role}"
            });

            return Ok(MapToTeamMember(user, 0, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user");
            return StatusCode(500, new { error = "Failed to invite user" });
        }
    }

    /// <summary>
    /// Update a user's role.
    /// PUT api/users/5/role
    /// </summary>
    [HttpPut("{id}/role")]
    public async Task<ActionResult<TeamMemberDto>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { error = $"User {id} not found" });

            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                if (int.TryParse(request.Role, out var roleInt) && Enum.IsDefined(typeof(UserRole), roleInt))
                    role = (UserRole)roleInt;
                else
                    return BadRequest(new { error = "Invalid role" });
            }

            var oldRole = user.Role;
            user.Role = role;
            await _context.SaveChangesAsync();

            await _auditTrail.LogActionAsync(new AuditLog
            {
                Action = "USER_ROLE_CHANGE",
                EntityType = "USER",
                EntityId = user.Id.ToString(),
                UserName = User.Identity?.Name ?? "System",
                OldValue = oldRole.ToString(),
                NewValue = role.ToString(),
                Comments = $"Changed role from {oldRole} to {role}"
            });

            var assignedCases = await _context.AuditCases.CountAsync(c => c.AssignedToUserId == user.Id);
            var reviewedTxns = await _context.AuditLogs.CountAsync(l => l.UserName == user.Username && l.Action == "REVIEW");
            return Ok(MapToTeamMember(user, assignedCases, reviewedTxns));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role");
            return StatusCode(500, new { error = "Failed to update role" });
        }
    }

    /// <summary>
    /// Deactivate a user.
    /// PUT api/users/5/deactivate
    /// </summary>
    [HttpPut("{id}/deactivate")]
    public async Task<ActionResult> Deactivate(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { error = $"User {id} not found" });

            user.IsActive = false;
            await _context.SaveChangesAsync();

            await _auditTrail.LogActionAsync(new AuditLog
            {
                Action = "USER_DEACTIVATE",
                EntityType = "USER",
                EntityId = user.Id.ToString(),
                UserName = User.Identity?.Name ?? "System",
                Comments = $"Deactivated {user.Email}"
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user");
            return StatusCode(500, new { error = "Failed to deactivate user" });
        }
    }

    /// <summary>
    /// Activate a user.
    /// PUT api/users/5/activate
    /// </summary>
    [HttpPut("{id}/activate")]
    public async Task<ActionResult> Activate(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { error = $"User {id} not found" });

            user.IsActive = true;
            await _context.SaveChangesAsync();

            await _auditTrail.LogActionAsync(new AuditLog
            {
                Action = "USER_ACTIVATE",
                EntityType = "USER",
                EntityId = user.Id.ToString(),
                UserName = User.Identity?.Name ?? "System",
                Comments = $"Activated {user.Email}"
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user");
            return StatusCode(500, new { error = "Failed to activate user" });
        }
    }

    /// <summary>
    /// Get activity log for a specific user.
    /// GET api/users/5/activity
    /// </summary>
    [HttpGet("{userId}/activity")]
    public async Task<ActionResult<List<AuditLog>>> GetUserActivity(int userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { error = $"User {userId} not found" });

            var logs = await _context.AuditLogs
                .Where(l => l.UserName == user.Username)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user activity");
            return StatusCode(500, new { error = "Failed to retrieve user activity" });
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static TeamMemberDto MapToTeamMember(User user, int assignedCases, int reviewedTransactions)
    {
        return new TeamMemberDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Role = user.Role,
            RoleName = user.Role.ToString(),
            IsActive = user.IsActive,
            LastLogin = user.LastLogin,
            AssignedCases = assignedCases,
            ReviewedTransactions = reviewedTransactions
        };
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────

public class TeamMemberDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? LastLogin { get; set; }
    public int AssignedCases { get; set; }
    public int ReviewedTransactions { get; set; }
}

public class InviteUserRequest
{
    public string Email { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Role { get; set; } = "3"; // Default: Viewer
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = "";
}
