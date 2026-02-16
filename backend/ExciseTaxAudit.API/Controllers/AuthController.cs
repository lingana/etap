using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExciseTaxAudit.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAuditService _auditService;
        private readonly AuditContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IAuditService auditService,
            AuditContext context,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _auditService = auditService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _authService.LoginAsync(request);
            if (!response.Success)
                return Unauthorized(response);

            return Ok(response);
        }

        [Authorize]
        [HttpGet("current-user")]
        public IActionResult GetCurrentUser()
        {
            var user = _authService.GetCurrentUser(User);
            if (user == null)
                return Unauthorized();

            return Ok(user);
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // JWT tokens don't require server-side logout, but we can log it
            _logger.LogInformation($"User {User.FindFirst("username")?.Value} logged out");
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("register")]
        [ApiExplorerSettings(IgnoreApi = true)] // Hidden from Swagger (for demo only)
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check if user exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);

                if (existingUser != null)
                    return BadRequest(new { message = "Username or email already exists" });

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PasswordHash = _authService.HashPassword(request.Password),
                    Role = UserRole.Auditor,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName,
                    Role = user.Role,
                    IsActive = user.IsActive
                };

                return Ok(new { message = "User registered successfully", user = userDto });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error registering user: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred during registration" });
            }
        }

        [HttpPost("seed-demo-users")]
        [ApiExplorerSettings(IgnoreApi = true)] // Hidden from Swagger (demo only)
        public async Task<IActionResult> SeedDemoUsers()
        {
            try
            {
                // Check if demo users already exist
                if (await _context.Users.AnyAsync(u => u.Username == "auditor"))
                    return Ok(new { message = "Demo users already seeded" });

                var demoUsers = new[]
                {
                    new User
                    {
                        Username = "auditor",
                        Email = "auditor@excise.local",
                        FirstName = "John",
                        LastName = "Smith",
                        PasswordHash = _authService.HashPassword("password123"),
                        Role = UserRole.Auditor,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "manager",
                        Email = "manager@excise.local",
                        FirstName = "Sarah",
                        LastName = "Johnson",
                        PasswordHash = _authService.HashPassword("password123"),
                        Role = UserRole.Manager,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Username = "admin",
                        Email = "admin@excise.local",
                        FirstName = "Mike",
                        LastName = "Brown",
                        PasswordHash = _authService.HashPassword("password123"),
                        Role = UserRole.Admin,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                _context.Users.AddRange(demoUsers);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Demo users seeded successfully",
                    users = new[]
                    {
                        new { username = "auditor", password = "password123", role = "Auditor" },
                        new { username = "manager", password = "password123", role = "Manager" },
                        new { username = "admin", password = "password123", role = "Admin" }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error seeding demo users: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred during seeding" });
            }
        }
    }

    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
    }
}
