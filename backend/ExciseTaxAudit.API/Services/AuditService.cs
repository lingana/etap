using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Services
{
    public interface IAuditService
    {
        Task<List<AuditCaseDto>> GetAuditsByUserAsync(int userId, string role);
        Task<AuditCaseDto> GetAuditByIdAsync(int id);
        Task<AuditCaseDto> CreateAuditAsync(CreateAuditRequest request, int createdByUserId);
        Task<AuditCaseDto> UpdateAuditStatusAsync(int id, string newStatus);
        Task<AuditCaseDto> AssignAuditAsync(int id, int? assignedToUserId);
        Task<bool> DeleteAuditAsync(int id);
        Task<AuditCaseDto> UpdateAuditStatsAsync(int id, int totalRecords, int flaggedRecords, int reviewedRecords, decimal recovery);
        Task<List<AuditCommentDto>> GetAuditCommentsAsync(int auditId);
        Task<AuditCommentDto> AddCommentAsync(int auditId, int userId, string comment, bool isInternal);
    }

    public class AuditService : IAuditService
    {
        private readonly AuditContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AuditContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<AuditCaseDto>> GetAuditsByUserAsync(int userId, string role)
        {
            try
            {
                IQueryable<AuditCase> query = _context.AuditCases
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.AssignedToUser)
                    .Include(a => a.ReviewedByUser);

                // Filter based on role
                if (role == "Auditor")
                {
                    // Auditors see their own audits and audits assigned to them
                    query = query.Where(a => a.CreatedByUserId == userId || a.AssignedToUserId == userId);
                }
                else if (role == "Manager")
                {
                    // Managers see all audits
                    // No additional filter needed
                }
                else if (role == "Admin")
                {
                    // Admins see all audits
                    // No additional filter needed
                }

                var audits = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
                return audits.Select(MapToAuditCaseDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting audits for user {userId}: {ex.Message}");
                return new List<AuditCaseDto>();
            }
        }

        public async Task<AuditCaseDto> GetAuditByIdAsync(int id)
        {
            try
            {
                var audit = await _context.AuditCases
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.AssignedToUser)
                    .Include(a => a.ReviewedByUser)
                    .FirstOrDefaultAsync(a => a.Id == id);

                return audit != null ? MapToAuditCaseDto(audit) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting audit {id}: {ex.Message}");
                return null;
            }
        }

        public async Task<AuditCaseDto> CreateAuditAsync(CreateAuditRequest request, int createdByUserId)
        {
            try
            {
                var audit = new AuditCase
                {
                    Name = request.Name,
                    Description = request.Description,
                    Company = request.Company,
                    State = request.State,
                    AuditPeriodStart = request.AuditPeriodStart,
                    AuditPeriodEnd = request.AuditPeriodEnd,
                    AuditType = Enum.Parse<AuditType>(request.AuditType),
                    Status = AuditStatus.Draft,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    TotalRecords = 0,
                    FlaggedRecords = 0,
                    ReviewedRecords = 0,
                    PotentialRecovery = 0
                };

                _context.AuditCases.Add(audit);
                await _context.SaveChangesAsync();

                var createdAudit = await GetAuditByIdAsync(audit.Id);
                return createdAudit;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating audit: {ex.Message}");
                throw;
            }
        }

        public async Task<AuditCaseDto> UpdateAuditStatusAsync(int id, string newStatus)
        {
            try
            {
                var audit = await _context.AuditCases.FindAsync(id);
                if (audit == null)
                    return null;

                audit.Status = Enum.Parse<AuditStatus>(newStatus);
                audit.LastModifiedAt = DateTime.UtcNow;

                // Set timestamp based on status
                if (audit.Status == AuditStatus.DataLoaded && !audit.DataLoadedAt.HasValue)
                    audit.DataLoadedAt = DateTime.UtcNow;
                else if (audit.Status == AuditStatus.PendingApproval && !audit.SubmittedAt.HasValue)
                    audit.SubmittedAt = DateTime.UtcNow;
                else if (audit.Status == AuditStatus.Approved && !audit.ApprovedAt.HasValue)
                    audit.ApprovedAt = DateTime.UtcNow;
                else if (audit.Status == AuditStatus.Closed && !audit.ClosedAt.HasValue)
                    audit.ClosedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return await GetAuditByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating audit status: {ex.Message}");
                throw;
            }
        }

        public async Task<AuditCaseDto> AssignAuditAsync(int id, int? assignedToUserId)
        {
            try
            {
                var audit = await _context.AuditCases.FindAsync(id);
                if (audit == null)
                    return null;

                audit.AssignedToUserId = assignedToUserId;
                audit.LastModifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return await GetAuditByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning audit: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteAuditAsync(int id)
        {
            try
            {
                var audit = await _context.AuditCases.FindAsync(id);
                if (audit == null)
                    return false;

                _context.AuditCases.Remove(audit);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting audit: {ex.Message}");
                return false;
            }
        }

        public async Task<AuditCaseDto> UpdateAuditStatsAsync(int id, int totalRecords, int flaggedRecords, int reviewedRecords, decimal recovery)
        {
            try
            {
                var audit = await _context.AuditCases.FindAsync(id);
                if (audit == null)
                    return null;

                audit.TotalRecords = totalRecords;
                audit.FlaggedRecords = flaggedRecords;
                audit.ReviewedRecords = reviewedRecords;
                audit.PotentialRecovery = recovery;
                audit.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return await GetAuditByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating audit stats: {ex.Message}");
                throw;
            }
        }

        public async Task<List<AuditCommentDto>> GetAuditCommentsAsync(int auditId)
        {
            try
            {
                var comments = await _context.AuditComments
                    .Where(c => c.AuditCaseId == auditId)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return comments.Select(MapToAuditCommentDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting comments for audit {auditId}: {ex.Message}");
                return new List<AuditCommentDto>();
            }
        }

        public async Task<AuditCommentDto> AddCommentAsync(int auditId, int userId, string comment, bool isInternal)
        {
            try
            {
                var auditComment = new AuditComment
                {
                    AuditCaseId = auditId,
                    UserId = userId,
                    Comment = comment,
                    IsInternal = isInternal,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditComments.Add(auditComment);
                await _context.SaveChangesAsync();

                var added = await _context.AuditComments
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == auditComment.Id);

                return MapToAuditCommentDto(added);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding comment: {ex.Message}");
                throw;
            }
        }

        private AuditCaseDto MapToAuditCaseDto(AuditCase audit)
        {
            return new AuditCaseDto
            {
                Id = audit.Id,
                Name = audit.Name,
                Description = audit.Description,
                Company = audit.Company,
                State = audit.State,
                AuditPeriodStart = audit.AuditPeriodStart,
                AuditPeriodEnd = audit.AuditPeriodEnd,
                AuditType = audit.AuditType.ToString(),
                Status = audit.Status.ToString(),
                TotalRecords = audit.TotalRecords,
                FlaggedRecords = audit.FlaggedRecords,
                ReviewedRecords = audit.ReviewedRecords,
                ReviewProgress = audit.ReviewProgress,
                PotentialRecovery = audit.PotentialRecovery,
                CreatedAt = audit.CreatedAt,
                LastModifiedAt = audit.LastModifiedAt,
                CreatedByUser = audit.CreatedByUser != null ? MapToUserDto(audit.CreatedByUser) : null,
                AssignedToUser = audit.AssignedToUser != null ? MapToUserDto(audit.AssignedToUser) : null,
                ReviewedByUser = audit.ReviewedByUser != null ? MapToUserDto(audit.ReviewedByUser) : null
            };
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

        private AuditCommentDto MapToAuditCommentDto(AuditComment comment)
        {
            return new AuditCommentDto
            {
                Id = comment.Id,
                AuditCaseId = comment.AuditCaseId,
                UserId = comment.UserId,
                User = MapToUserDto(comment.User),
                Comment = comment.Comment,
                IsInternal = comment.IsInternal,
                CreatedAt = comment.CreatedAt
            };
        }
    }

    public class AuditCommentDto
    {
        public int Id { get; set; }
        public int AuditCaseId { get; set; }
        public int UserId { get; set; }
        public UserDto User { get; set; }
        public string Comment { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
