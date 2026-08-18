using IntelligenceKit.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntelligenceKit.Server.Migrations.PostgreSql;

/// <summary>
/// Design-time context used only by `dotnet ef` to generate this provider's
/// migrations. Never connects at generation time.
/// </summary>
public class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<IntelligenceDbContext>
{
    public IntelligenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntelligenceDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=intelligencekit;Username=postgres",
                x => x.MigrationsAssembly(typeof(PostgreSqlDesignTimeFactory).Assembly.GetName().Name))
            .Options;

        return new IntelligenceDbContext(options);
    }
}
