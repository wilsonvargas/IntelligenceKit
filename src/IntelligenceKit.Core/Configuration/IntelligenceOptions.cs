namespace IntelligenceKit.Core.Configuration;

public class IntelligenceOptions
{
    public string ApplicationName { get; set; } = string.Empty;

    public string ApplicationVersion { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public project identifier used to route events to the right project on
    /// the server. Not a secret.
    /// </summary>
    public string ProjectKey { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Deployment environment, e.g. "production", "staging". Attached to every event.</summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Release/build identifier. When left empty, the application version is used.
    /// </summary>
    public string Release { get; set; } = string.Empty;

    /// <summary>Maximum breadcrumbs retained in the local ring buffer.</summary>
    public int BreadcrumbCapacity { get; set; } = 50;

    /// <summary>Automatically record a breadcrumb whenever a page appears.</summary>
    public bool EnableNavigationBreadcrumbs { get; set; } = true;

    /// <summary>
    /// Capture a downscaled screenshot of the current screen (proactively, on
    /// navigation) and attach the last one to crashes/exceptions. OFF by default:
    /// screenshots can contain personal data, so it's an explicit opt-in.
    /// </summary>
    public bool EnableScreenCapture { get; set; } = false;

    /// <summary>Longest edge of the stored screenshot, in pixels.</summary>
    public int ScreenCaptureMaxDimension { get; set; } = 640;

    /// <summary>JPEG quality for the stored screenshot, 0.0–1.0.</summary>
    public float ScreenCaptureJpegQuality { get; set; } = 0.6f;

    /// <summary>
    /// Page type names to never capture (e.g. "LoginPage", "PaymentPage"). When
    /// the user is on one of these, no screenshot is kept.
    /// </summary>
    public HashSet<string> ScreenCaptureExcludedPages { get; set; } = new();

    public bool EnableCrashReporting { get; set; } = true;

    public bool EnableDeviceInfo { get; set; } = true;
}
