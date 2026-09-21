using Microsoft.Extensions.Configuration;

namespace PlaywrightAutomationFramework.Config;

/// <summary>
/// Single source of truth for framework configuration. Reads appsettings.json once
/// (thread-safe, lazy) and hands back a strongly-typed <see cref="PlaywrightSettings"/>.
/// An optional "PW_ENVIRONMENT" environment variable can point at an
/// appsettings.{Environment}.json overlay for CI (e.g. QA, Prod) without touching this file.
/// </summary>
public static class ConfigReader
{
    private static readonly Lazy<PlaywrightSettings> LazySettings = new(LoadSettings, isThreadSafe: true);

    public static PlaywrightSettings Settings => LazySettings.Value;

    private static PlaywrightSettings LoadSettings()
    {
        var basePath = AppContext.BaseDirectory;
        var environmentOverride = System.Environment.GetEnvironmentVariable("PW_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            // Optional overlay, e.g. appsettings.QA.json - only individual keys need to be present.
            builder.AddJsonFile($"appsettings.{environmentOverride}.json", optional: true, reloadOnChange: false);
        }

        // Allows CI to override individual leaf values, e.g. PlaywrightSettings__BrowserSettings__Headless=false
        builder.AddEnvironmentVariables();

        var root = new AppSettingsRoot();
        builder.Build().Bind(root);
        return root.PlaywrightSettings;
    }
}
