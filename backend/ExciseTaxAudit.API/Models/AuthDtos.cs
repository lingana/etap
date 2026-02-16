namespace ExciseTaxAudit.API.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
        public string Token { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public string RoleName => Role.ToString();
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class AuditCaseDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public string State { get; set; }
        public DateTime AuditPeriodStart { get; set; }
        public DateTime AuditPeriodEnd { get; set; }
        public string AuditType { get; set; }
        public string Status { get; set; }
        public int TotalRecords { get; set; }
        public int FlaggedRecords { get; set; }
        public int ReviewedRecords { get; set; }
        public int ReviewProgress { get; set; }
        public decimal PotentialRecovery { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public UserDto CreatedByUser { get; set; }
        public UserDto AssignedToUser { get; set; }
        public UserDto ReviewedByUser { get; set; }
    }

    public class CreateAuditRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Company { get; set; }
        public string State { get; set; }
        public DateTime AuditPeriodStart { get; set; }
        public DateTime AuditPeriodEnd { get; set; }
        public string AuditType { get; set; }
    }

    public class UpdateAuditStatusRequest
    {
        public string Status { get; set; }
    }
}
