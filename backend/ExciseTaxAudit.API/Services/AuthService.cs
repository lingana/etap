using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ExciseTaxAudit.API.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        UserDto GetCurrentUser(ClaimsPrincipal user);
        string GenerateJwtToken(User user);
        bool VerifyPassword(string password, string hash);
        string HashPassword(string password);
    }

    public class AuthService : IAuthService
    {
        private readonly AuditContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AuditContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning($"Login attempt for non-existent user: {request.Username}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning($"Failed login attempt for user: {request.Username}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Update last login
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);
                var userDto = MapToUserDto(user);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    User = userDto,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in LoginAsync: {ex.Message}");
                return new LoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public UserDto GetCurrentUser(ClaimsPrincipal claimsPrincipal)
        {
            var userIdClaim = claimsPrincipal.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            return user != null ? MapToUserDto(user) : null;
        }

        public string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? "your-secret-key-min-32-characters-long-please"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("username", user.Username),
                new Claim("email", user.Email),
                new Claim("role", user.Role.ToString()),
                new Claim("name", user.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "ExciseTaxAudit",
                audience: _configuration["Jwt:Audience"] ?? "ExciseTaxAuditUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                var hashBytes = Convert.FromBase64String(hash);
                var salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                var hash2 = pbkdf2.GetBytes(20);

                for (int i = 0; i < 20; i++)
                {
                    if (hashBytes[i + 16] != hash2[i])
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string HashPassword(string password)
        {
            using (var salt = new RNGCryptoServiceProvider())
            {
                var saltBytes = new byte[16];
                salt.GetBytes(saltBytes);

                var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
                var hash = pbkdf2.GetBytes(20);

                var hashBytes = new byte[36];
                Array.Copy(saltBytes, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 20);

                return Convert.ToBase64String(hashBytes);
            }
        }

        private UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                LastLogin = user.LastLogin
            };
        }
    }
}
