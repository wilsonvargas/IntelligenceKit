using IntelligenceKit.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntelligenceKit.AspNetCore;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// One-line setup for ASP.NET Core:
    /// <code>builder.UseIntelligenceKit("http://key@host:7099/my-api");</code>
    /// Captures unhandled request exceptions (with route/method/trace context),
    /// logged errors, per-request breadcrumbs and per-route latency. Sessions are
    /// off by default (they model app usage, not server uptime); the environment
    /// defaults to the host's (Development/Staging/Production).
    /// </summary>
    public static WebApplicationBuilder UseIntelligenceKit(this WebApplicationBuilder builder, string dsn,
        Action<IntelligenceKitHostOptions>? configure = null)
    {
        builder.Services.AddIntelligenceKitAspNetCore(dsn, options =>
        {
            options.Environment = builder.Environment.EnvironmentName.ToLowerInvariant();
            configure?.Invoke(options);
        });
        return builder;
    }

    /// <summary>
    /// <see cref="UseIntelligenceKit"/> for hosts configured through <see cref="IServiceCollection"/> directly.
    /// </summary>
    public static IServiceCollection AddIntelligenceKitAspNetCore(this IServiceCollection services, string dsn,
        Action<IntelligenceKitHostOptions>? configure = null)
    {
        services.AddIntelligenceKit(dsn, options =>
        {
            options.EnableAutoSessionTracking = false;
            configure?.Invoke(options);
        });
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, IntelligenceKitStartupFilter>());
        return services;
    }
}
