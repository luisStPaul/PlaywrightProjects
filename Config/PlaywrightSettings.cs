namespace PlaywrightAutomationFramework.Config;

/// <summary>
/// Root of appsettings.json. Configuration is bound into this object graph once,
/// then exposed to the whole framework via <see cref="ConfigReader"/>.
/// </summary>
public sealed class AppSettingsRoot
{
    public PlaywrightSettings PlaywrightSettings { get; set; } = new();
}

public sealed class PlaywrightSettings
{
    public string Environment { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public TimeoutSettings TimeoutSettings { get; set; } = new();
    public BrowserSettings BrowserSettings { get; set; } = new();
    public TestSettings TestSettings { get; set; } = new();
    public ReportingSettings ReportingSettings { get; set; } = new();
    public UserAccounts UserAccounts { get; set; } = new();
}

public sealed class TimeoutSettings
{
    public float DefaultTimeout { get; set; }
    public float NavigationTimeout { get; set; }
    public float AssertionTimeout { get; set; }
}

public sealed class BrowserSettings
{
    public List<string> TargetBrowsers { get; set; } = new();
    public bool Headless { get; set; }
    public float SlowMo { get; set; }
    public List<string> Args { get; set; } = new();
}

public sealed class TestSettings
{
    public string UiBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
}

public sealed class ReportingSettings
{
    /// <summary>Off | OnFailure | Always</summary>
    public string TakeScreenshot { get; set; } = "OnFailure";

    /// <summary>Off | RetainOnFailure | On</summary>
    public string RecordVideo { get; set; } = "RetainOnFailure";

    /// <summary>Off | RetainOnFailure | On</summary>
    public string TraceMode { get; set; } = "RetainOnFailure";

    public string ArtifactsPath { get; set; } = "artifacts/";
}

public sealed class UserAccounts
{
    public UserAccount AdminUser { get; set; } = new();
    public UserAccount StandardUser { get; set; } = new();
}

public sealed class UserAccount
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
