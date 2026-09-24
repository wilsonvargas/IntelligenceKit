using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace IntelligenceKit.Extensions.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Sends log entries to IntelligenceKit: breadcrumbs from Information, events
    /// from Error (tunable via <paramref name="configure"/>). Requires an
    /// <c>IIntelligenceKit</c> registered by a platform SDK (e.g. <c>UseIntelligenceKit</c>).
    /// Safe to call more than once.
    /// </summary>
    public static ILoggingBuilder AddIntelligenceKit(this ILoggingBuilder builder, Action<IntelligenceKitLoggerOptions>? configure = null)
    {
        var options = new IntelligenceKitLoggerOptions();
        configure?.Invoke(options);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, IntelligenceKitLoggerProvider>(
            sp => new IntelligenceKitLoggerProvider(sp, options)));
        return builder;
    }
}
