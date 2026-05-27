using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Data;

/// <summary>
/// Read-only DbContext for the Portal.Logging database.
/// Configured with NoTracking since we only read log data.
/// </summary>
public class LoggingDbContext : DbContext
{
    public LoggingDbContext(DbContextOptions<LoggingDbContext> options) : base(options) { }

    public DbSet<LogEntry> Logs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.ToTable("Logs", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Message).HasColumnName("Message");
            entity.Property(e => e.MessageTemplate).HasColumnName("MessageTemplate");
            entity.Property(e => e.Level).HasColumnName("Level").HasMaxLength(128);
            entity.Property(e => e.TimeStamp).HasColumnName("TimeStamp");
            entity.Property(e => e.Exception).HasColumnName("Exception");
            entity.Property(e => e.CorrelationId).HasColumnName("CorrelationId").HasMaxLength(128);
            entity.Property(e => e.UserId).HasColumnName("UserId").HasMaxLength(450);
            entity.Property(e => e.BusinessId).HasColumnName("BusinessId");
            entity.Property(e => e.SourceContext).HasColumnName("SourceContext").HasMaxLength(512);
            entity.Property(e => e.RequestPath).HasColumnName("RequestPath").HasMaxLength(512);
            entity.Property(e => e.MachineName).HasColumnName("MachineName").HasMaxLength(128);
        });
    }
}
