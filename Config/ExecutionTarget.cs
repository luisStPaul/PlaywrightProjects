namespace PlaywrightAutomationFramework.Config;

/// <summary>
/// Every value this enum takes becomes one NUnit test-fixture instance via TestFixtureSource.
/// UI values map 1:1 to a Playwright browser engine; Api means "no browser, just an
/// IAPIRequestContext". BaseTest branches on this single value to decide what to spin up.
/// </summary>
public enum ExecutionTarget
{
    Chromium,
    Firefox,
    Webkit,
    Api
}
