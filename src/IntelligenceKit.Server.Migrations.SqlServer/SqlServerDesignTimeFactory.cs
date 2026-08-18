using IntelligenceKit.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntelligenceKit.Server.Migrations.SqlServer;

/// <summary>
/// Design-time context used only by `dotnet ef` to generate this provider's
/// migrations. Never connects at generation time.
/// </summary>
public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<IntelligenceDbContext>
{
    public IntelligenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntelligenceDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=intelligencekit;Trusted_Connection=True;TrustServerCertificate=True",
                x => x.MigrationsAssembly(typeof(SqlServerDesignTimeFactory).Assembly.GetName().Name))
            .Options;

        return new IntelligenceDbContext(options);
    }
}
