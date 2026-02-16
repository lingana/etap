using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Data;

/// <summary>
/// Entity Framework Core DbContext for audit database operations.
/// </summary>
public class AuditContext : DbContext
{
    public AuditContext(DbContextOptions<AuditContext> options) : base(options) { }

    // Tax & Client Management
    public DbSet<TaxType> TaxTypes { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Engagement> Engagements { get; set; }

    // Authentication & Authorization
    public DbSet<User> Users { get; set; }
    public DbSet<AuditCase> AuditCases { get; set; }
    public DbSet<AuditComment> AuditComments { get; set; }

    // Data Management
    public DbSet<TransactionRecord> TransactionRecords { get; set; }
    public DbSet<UploadJobStatus> UploadJobs { get; set; }
    
    // Refund & Recovery
    public DbSet<RefundClaim> RefundClaims { get; set; }
    
    // Compliance & Audit Trail
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<TransactionAttachment> TransactionAttachments { get; set; }
    public DbSet<ExportHistory> ExportHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TaxType configuration
        modelBuilder.Entity<TaxType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Type).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Client configuration
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EIN).IsUnique();
            entity.HasIndex(e => e.TaxTypeId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EIN).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ContactEmail).HasMaxLength(100);
            
            // Relationships
            entity.HasOne(e => e.TaxType)
                .WithMany(t => t.Clients)
                .HasForeignKey(e => e.TaxTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Engagement configuration
        modelBuilder.Entity<Engagement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.FiscalYear);
            entity.HasIndex(e => e.ExternalEngagementId);
            entity.Property(e => e.EngagementName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ExternalEngagementId).HasMaxLength(64);
            
            // Relationships
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Engagements)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.LeadAuditor)
                .WithMany()
                .HasForeignKey(e => e.LeadAuditorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        // AuditCase configuration
        modelBuilder.Entity<AuditCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasIndex(e => e.AssignedToUserId);
            
            // Relationships
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.AssignedToUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.ReviewedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditComment configuration
        modelBuilder.Entity<AuditComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuditCaseId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            
            // Relationships
            entity.HasOne(e => e.AuditCase)
                .WithMany()
                .HasForeignKey(e => e.AuditCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TransactionRecord configuration
        modelBuilder.Entity<TransactionRecord>(entity =>
        {
            entity.HasKey(e => e.RecordID);
            entity.HasIndex(e => e.EngagementId);
            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.MerchantState);
            entity.HasIndex(e => e.FuelType);
            entity.HasIndex(e => e.AnomalyScore);
            entity.HasIndex(e => e.IsReviewed);
        });

        // UploadJobStatus configuration
        modelBuilder.Entity<UploadJobStatus>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UploadedAt);
        });

        // RefundClaim configuration
        modelBuilder.Entity<RefundClaim>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ClaimNumber).IsUnique();
            entity.HasIndex(e => e.EngagementId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TaxYear);
            entity.HasIndex(e => e.CreatedDate);
            entity.HasIndex(e => e.SubmittedDate);
            
            entity.Property(e => e.ClaimedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ApprovedAmount).HasPrecision(18, 2);
            entity.Property(e => e.PaidAmount).HasPrecision(18, 2);
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4);
            
            entity.HasOne(e => e.Engagement)
                .WithMany()
                .HasForeignKey(e => e.EngagementId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.AuditCase)
                .WithMany()
                .HasForeignKey(e => e.AuditCaseId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
