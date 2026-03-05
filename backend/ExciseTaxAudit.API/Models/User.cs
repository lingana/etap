namespace ExciseTaxAudit.API.Models
{
    public enum UserRole
    {
        Auditor,
        Manager,
        Admin,
        Viewer
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public string FullName => $"{FirstName} {LastName}";
    }
}
