using IntelligenceKit.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntelligenceKit.Server.Migrations.Sqlite;

/// <summary>
/// Design-time context used only by `dotnet ef` to generate/scaffold this
/// provider's migrations. Never connects at generation time.
/// </summary>
public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<IntelligenceDbContext>
{
    public IntelligenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntelligenceDbContext>()
            .UseSqlite(
                "Data Source=design.db",
                x => x.MigrationsAssembly(typeof(SqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;

        return new IntelligenceDbContext(options);
    }
}
