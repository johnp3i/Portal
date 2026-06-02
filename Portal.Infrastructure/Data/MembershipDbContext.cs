using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Identity;

namespace Portal.Infrastructure.Data;

/// <summary>
/// EF Core context for the Membership database (Identity tables + Invitation).
/// </summary>
public class MembershipDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public MembershipDbContext(DbContextOptions<MembershipDbContext> options) : base(options) { }

    public DbSet<Invitation> Invitations { get; set; } = null!;
    public DbSet<PendingRegistration> PendingRegistrations { get; set; } = null!;
    public DbSet<UserBusiness> UserBusinesses { get; set; } = null!;
    public DbSet<UserBusinessPermission> UserBusinessPermissions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Invitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(128).IsRequired();
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        });

        builder.Entity<PendingRegistration>(entity =>
        {
            entity.ToTable("PendingRegistration", "membership");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.PromoCodeId).IsRequired(false);
            entity.Property(e => e.CreatedAtUtc).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(e => e.User)
                  .WithOne()
                  .HasForeignKey<PendingRegistration>(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<UserBusiness>(entity =>
        {
            entity.ToTable("UserBusiness", "membership");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.BusinessId }).IsUnique();
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<UserBusinessPermission>(entity =>
        {
            entity.ToTable("UserBusinessPermission", "membership");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserBusinessId, e.Module }).IsUnique();
            entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccessLevel).HasMaxLength(20).IsRequired();
            entity.HasOne(e => e.UserBusiness)
                  .WithMany()
                  .HasForeignKey(e => e.UserBusinessId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
