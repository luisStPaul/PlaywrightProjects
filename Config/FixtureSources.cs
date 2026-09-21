namespace PlaywrightAutomationFramework.Config;

/// <summary>
/// Feeds NUnit's [TestFixtureSource] attribute. Keeping this separate from BaseTest means
/// any UI test class can multiply itself across every configured browser with one line,
/// and any API test class stays single-instance, browser-free.
/// </summary>
public static class FixtureSources
{
    /// <summary>
    /// One fixture instance per browser listed in appsettings.json -> BrowserSettings.TargetBrowsers.
    /// Driven by config (not hardcoded) so trimming the json list trims the test matrix too.
    /// </summary>
    public static IEnumerable<ExecutionTarget> UiBrowserFixtures()
    {
        foreach (var browserName in ConfigReader.Settings.BrowserSettings.TargetBrowsers)
        {
            if (Enum.TryParse<ExecutionTarget>(browserName, ignoreCase: true, out var target)
                && target != ExecutionTarget.Api)
            {
                yield return target;
            }
        }
    }

    /// <summary>Single fixture instance, no browser involved.</summary>
    public static IEnumerable<ExecutionTarget> ApiFixture()
    {
        yield return ExecutionTarget.Api;
    }
}
