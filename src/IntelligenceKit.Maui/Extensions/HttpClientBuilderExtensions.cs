using IntelligenceKit.Core.Diagnostics;
using IntelligenceKit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligenceKit.Maui.Extensions;

public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Records every request made through this client as an <c>http</c> breadcrumb
    /// and a performance span:
    /// <c>builder.Services.AddHttpClient&lt;MyApi&gt;().AddIntelligenceKitHandler();</c>
    /// </summary>
    public static IHttpClientBuilder AddIntelligenceKitHandler(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler(sp => new IntelligenceKitHttpHandler(
            sp.GetRequiredService<IIntelligenceKit>(),
            sp.GetService<IPerformanceMonitor>()));
}
