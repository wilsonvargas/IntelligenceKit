using IntelligenceKit.Core.Models;
using ObjCRuntime;

namespace IntelligenceKit.Maui.CrashReporting;

public partial class CrashReporter
{
    partial void RegisterPlatformHandlers()
    {
        // Catches managed exceptions as they cross into native (Objective-C) code,
        // which is where most iOS crashes surface in a .NET MAUI app. Pure
        // managed unhandled exceptions are already handled by the shared
        // AppDomain hook in CrashReporter.cs.
        Runtime.MarshalManagedException += OnMarshalManagedException;
    }

    private void OnMarshalManagedException(object? sender, MarshalManagedExceptionEventArgs args)
    {
        // Persist synchronously (local write, fast) so the crash isn't lost if
        // the process is torn down right after. Deduped against the managed
        // AppDomain handler so the same crash isn't reported twice.
        CaptureFatal(ExceptionInfo.FromException(args.Exception));
    }
}
