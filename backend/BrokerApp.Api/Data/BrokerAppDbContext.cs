using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BrokerApp.Api.Data;

public sealed class BrokerAppDbContext : DbContext
{
    private readonly bool _usesInMemoryProvider;
    private readonly string _providerName;

    public BrokerAppDbContext(DbContextOptions<BrokerAppDbContext> options)
        : base(options)
    {
        _providerName = options.Extensions
            .Select(extension => extension.GetType().FullName ?? string.Empty)
            .FirstOrDefault(name => name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                || name.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        _usesInMemoryProvider = _providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanAction> LoanActions => Set<LoanAction>();
    public DbSet<ActionEvent> ActionEvents => Set<ActionEvent>();
    public DbSet<LoanNote> LoanNotes => Set<LoanNote>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, BrokerAppModelCacheKeyFactory>();
    }

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
            if (_usesInMemoryProvider)
            {
                entity.Ignore(e => e.RowVersion);
            }
            else
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
            }
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
            if (_usesInMemoryProvider)
            {
                entity.Ignore(e => e.RowVersion);
            }
            else
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
            }
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
            if (_usesInMemoryProvider)
            {
                entity.Ignore(e => e.RowVersion);
            }
            else
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
            }
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
            if (_usesInMemoryProvider)
            {
                entity.Ignore(e => e.RowVersion);
            }
            else
            {
                entity.Property(e => e.RowVersion).IsRowVersion();
            }
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

        modelBuilder.Entity<LoanNote>(entity =>
        {
            entity.Property(e => e.Body).HasMaxLength(2000).IsRequired();
            entity.HasIndex(e => new { e.OrganizationId, e.LoanId, e.CreatedAtUtc });
            entity.HasOne(e => e.Organization)
                .WithMany(e => e.Notes)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Loan)
                .WithMany(e => e.Notes)
                .HasForeignKey(e => e.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LoanAction)
                .WithMany(e => e.Notes)
                .HasForeignKey(e => e.LoanActionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private sealed class BrokerAppModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is BrokerAppDbContext brokerAppDbContext)
            {
                return (context.GetType(), brokerAppDbContext._providerName, designTime);
            }

            return (context.GetType(), designTime);
        }
    }
}
