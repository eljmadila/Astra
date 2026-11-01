using AspnetCoreMvcFull.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AspnetCoreMvcFull.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailOutboxItem> EmailOutbox => Set<EmailOutboxItem>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectClient> ProjectClients => Set<ProjectClient>();
    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize Identity Table Names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(u => u.Name).HasMaxLength(150).IsRequired();
            entity.Property(u => u.Department).HasMaxLength(100).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles");
        });

        builder.Entity<IdentityUserRole<int>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<int>>().ToTable("user_tokens");

        // Audit Logs
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Entity).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(100);
            entity.Property(a => a.IpAddress).HasMaxLength(45);
            entity.Property(a => a.Details).HasColumnType("nvarchar(max)");
            entity.Property(a => a.CreatedAt).IsRequired();

            entity.HasIndex(a => a.CreatedAt);
            entity.HasIndex(a => new { a.Entity, a.EntityId });

            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Notifications
        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Type).HasMaxLength(50).IsRequired();
            entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.Link).HasMaxLength(500);

            entity.HasIndex(n => new { n.UserId, n.IsRead });

            entity.HasOne(n => n.User)
                  .WithMany()
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Email Outbox
        builder.Entity<EmailOutboxItem>(entity =>
        {
            entity.ToTable("email_outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ToAddress).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Clients
        builder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.ContactPerson).HasMaxLength(150);
            entity.Property(c => c.Email).HasMaxLength(256);
            entity.Property(c => c.Phone).HasMaxLength(50);
            entity.Property(c => c.Notes).HasColumnType("nvarchar(max)");
            entity.Property(c => c.RowVersion).IsRowVersion();

            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasIndex(c => c.IsActive);
        });

        // Project Roles
        builder.Entity<ProjectRole>(entity =>
        {
            entity.ToTable("project_roles");
            entity.HasKey(pr => pr.Id);
            entity.Property(pr => pr.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(pr => pr.Name).IsUnique();
        });

        // Projects
        builder.Entity<Project>(entity =>
        {
            entity.ToTable("projects", t =>
            {
                t.HasCheckConstraint("CK_projects_planned_end", "[PlannedEndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_projects_actual_end", "[ActualEndDate] IS NULL OR [ActualEndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_projects_progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100");
            });

            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasColumnType("nvarchar(max)");
            entity.Property(p => p.Status).HasConversion<int>().IsRequired();
            entity.Property(p => p.Priority).HasConversion<int>().IsRequired();
            entity.Property(p => p.ProgressMode).HasConversion<int>().IsRequired();
            entity.Property(p => p.RowVersion).IsRowVersion();

            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasIndex(p => new { p.Status, p.IsArchived });
            entity.HasIndex(p => p.LeadId);
            entity.HasIndex(p => p.PlannedEndDate);

            entity.HasOne(p => p.Lead)
                  .WithMany()
                  .HasForeignKey(p => p.LeadId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CreatedBy)
                  .WithMany()
                  .HasForeignKey(p => p.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Project-Clients (Many-to-Many Join)
        builder.Entity<ProjectClient>(entity =>
        {
            entity.ToTable("project_clients");
            entity.HasKey(pc => new { pc.ProjectId, pc.ClientId });

            entity.HasOne(pc => pc.Project)
                  .WithMany(p => p.ProjectClients)
                  .HasForeignKey(pc => pc.ProjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pc => pc.Client)
                  .WithMany(c => c.ProjectClients)
                  .HasForeignKey(pc => pc.ClientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
