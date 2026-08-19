using System.Net.Http.Headers;
using IntelligenceKit.Server.Data;
using IntelligenceKit.Server.Retention;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntelligenceKit.Server.Tests;

/// <summary>
/// Hosts the real Server app for integration tests, but swaps its file-backed
/// SQLite database for a single shared in-memory connection (kept open for the
/// factory's lifetime so the schema survives between requests) and pins a known
/// read token so the auth matrix is deterministic.
/// </summary>
public sealed class ServerAppFactory : WebApplicationFactory<Program>
{
    public const string TestToken = "test-admin-token";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _environment;
    private readonly bool _withToken;
    private readonly IReadOnlyDictionary<string, string?>? _extraSettings;
    private readonly bool _disableHostedServices;

    // xUnit's IClassFixture requires exactly one public constructor; it yields the
    // default configuration (Development + a configured read token). Other
    // configurations are created explicitly via the Create* factory methods.
    public ServerAppFactory() : this("Development", withToken: true)
    {
    }

    private ServerAppFactory(
        string environment,
        bool withToken,
        IReadOnlyDictionary<string, string?>? extraSettings = null,
        bool disableHostedServices = false)
    {
        _environment = environment;
        _withToken = withToken;
        _extraSettings = extraSettings;
        _disableHostedServices = disableHostedServices;
        _connection.Open();
    }

    /// <summary>A factory with an explicit environment / token configuration,
    /// for tests that exercise the fail-open vs fail-closed auth matrix.</summary>
    public static ServerAppFactory Create(string environment, bool withToken)
        => new(environment, withToken);

    /// <summary>A factory with retention enabled at <paramref name="days"/> and the
    /// timed background sweeper removed, so tests drive <see cref="RetentionSweeper"/>
    /// deterministically instead of racing the hosted service.</summary>
    public static ServerAppFactory CreateWithRetention(int days)
        => new("Development", withToken: true,
            new Dictionary<string, string?>
            {
                ["Retention:Enabled"] = "true",
                ["Retention:Days"] = days.ToString(),
            },
            disableHostedServices: true);

    /// <summary>A factory with retention explicitly disabled (and the background
    /// sweeper removed), for asserting the no-op path.</summary>
    public static ServerAppFactory CreateWithRetentionDisabled()
        => new("Development", withToken: true,
            new Dictionary<string, string?> { ["Retention:Enabled"] = "false" },
            disableHostedServices: true);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
            };
            if (_withToken)
                settings["Auth:ReadToken"] = TestToken;

            if (_extraSettings is not null)
            {
                foreach (var kv in _extraSettings)
                    settings[kv.Key] = kv.Value;
            }

            // Added last, so it wins over the app's appsettings.json.
            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // Replace the DbContext registration (Program wired it to a file) with
            // one bound to our shared in-memory connection. Migrations still run on
            // startup against it via the Sqlite migrations assembly.
            RemoveAll(services, typeof(DbContextOptions<IntelligenceDbContext>));
            RemoveAll(services, typeof(IntelligenceDbContext));

            services.AddDbContext<IntelligenceDbContext>(options =>
                options.UseSqlite(
                    _connection,
                    x => x.MigrationsAssembly("IntelligenceKit.Server.Migrations.Sqlite")));

            // Drop background hosted services (the retention sweeper) so tests can
            // drive their logic directly without a timer racing them.
            if (_disableHostedServices)
                RemoveAll(services, typeof(IHostedService));
        });
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == serviceType)
                services.RemoveAt(i);
        }
    }

    /// <summary>An HttpClient carrying the admin read token (passes read-side auth).</summary>
    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
