using IntelligenceKit.Maui.Extensions;
using Microsoft.Extensions.Logging;

namespace Sample.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                // DSN: http://{projectKey}@{host}:{port}/{projectId}
                // 10.0.2.2 is the Android emulator's alias for the host machine,
                // where IntelligenceKit.Server runs (http profile, port 7099).
                .UseIntelligenceKit("http://demo-key@10.0.2.2:7099/demo-maui", options =>
                {
                    // Opt-in: capture the last screen and attach it to crashes.
                    // (Off by default because screenshots can contain personal data.)
                    options.EnableScreenCapture = true;
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
