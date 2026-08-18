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

    public DbSet<Issue> Issues => Set<Issue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stored = modelBuilder.Entity<StoredEvent>();
        stored.HasKey(e => e.Id);
        // Common query paths: newest-first within a project, and by event type.
        stored.HasIndex(e => new { e.ProjectId, e.ReceivedAt });
        stored.HasIndex(e => e.EventType);
        // Listing the events that belong to an issue.
        stored.HasIndex(e => new { e.ProjectId, e.Fingerprint });

        var screenshot = modelBuilder.Entity<StoredScreenshot>();
        screenshot.HasKey(s => s.EventId);

        var issue = modelBuilder.Entity<Issue>();
        issue.HasKey(i => i.Id);
        // One issue per (project, fingerprint); also the upsert lookup path.
        issue.HasIndex(i => new { i.ProjectId, i.Fingerprint }).IsUnique();
        issue.HasIndex(i => new { i.ProjectId, i.LastSeen });
    }
}
