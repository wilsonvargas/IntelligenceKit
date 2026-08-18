using Microsoft.EntityFrameworkCore;

namespace IntelligenceKit.Server.Data;

public class IntelligenceDbContext : DbContext
{
    public IntelligenceDbContext(DbContextOptions<IntelligenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoredEvent> Events => Set<StoredEvent>();

    public DbSet<StoredScreenshot> Screenshots => Set<StoredScreenshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stored = modelBuilder.Entity<StoredEvent>();
        stored.HasKey(e => e.Id);
        // Common query paths: newest-first within a project, and by event type.
        stored.HasIndex(e => new { e.ProjectId, e.ReceivedAt });
        stored.HasIndex(e => e.EventType);

        var screenshot = modelBuilder.Entity<StoredScreenshot>();
        screenshot.HasKey(s => s.EventId);
    }
}
