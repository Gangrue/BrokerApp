using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Data;

public sealed class BrokerAppDbContext : DbContext
{
    public BrokerAppDbContext(DbContextOptions<BrokerAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanAction> LoanActions => Set<LoanAction>();
    public DbSet<ActionEvent> ActionEvents => Set<ActionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TimeZoneId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.OrganizationId, e.Email }).IsUnique();
            entity.HasOne(e => e.Organization)
                .WithMany(e => e.Users)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Phone).HasMaxLength(40);
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.OrganizationId, e.LastName, e.FirstName });
            entity.HasIndex(e => new { e.OrganizationId, e.Email });
            entity.HasOne(e => e.Organization)
                .WithMany(e => e.Customers)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.Property(e => e.LoanNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Stage).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.OrganizationId, e.LoanNumber }).IsUnique();
            entity.HasOne(e => e.Organization)
                .WithMany(e => e.Loans)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer)
                .WithMany(e => e.Loans)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OwnerUser)
                .WithMany(e => e.OwnedLoans)
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanAction>(entity =>
        {
            entity.Property(e => e.PublicId).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Section).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.WorkflowStatus).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Priority).HasMaxLength(40).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.OrganizationId, e.PublicId }).IsUnique();
            entity.HasIndex(e => new { e.OrganizationId, e.AssignedUserId, e.DueDate, e.Priority, e.WorkflowStatus });
            entity.HasOne(e => e.Organization)
                .WithMany(e => e.Actions)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Loan)
                .WithMany(e => e.Actions)
                .HasForeignKey(e => e.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedUser)
                .WithMany(e => e.AssignedActions)
                .HasForeignKey(e => e.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActionEvent>(entity =>
        {
            entity.Property(e => e.EventType).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.OldValue).HasMaxLength(1000);
            entity.Property(e => e.NewValue).HasMaxLength(1000);
            entity.HasIndex(e => new { e.LoanActionId, e.OccurredAtUtc });
            entity.HasOne(e => e.LoanAction)
                .WithMany(e => e.Events)
                .HasForeignKey(e => e.LoanActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
