namespace ExciseTaxAudit.API.Models
{
    public class AuditComment
    {
        public int Id { get; set; }
        public int AuditCaseId { get; set; }
        public AuditCase AuditCase { get; set; }
        
        public int UserId { get; set; }
        public User User { get; set; }
        
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsInternal { get; set; } // Internal notes vs client-facing
    }
}
