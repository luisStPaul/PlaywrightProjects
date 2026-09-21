using Microsoft.Playwright;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using PlaywrightAutomationFramework.Config;

namespace PlaywrightAutomationFramework.Base;

/// <summary>
/// Single base class for the whole framework. Concrete test classes decorate themselves with
/// [TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.UiBrowserFixtures))] to get
/// one run per browser with a live Page, or
/// [TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.ApiFixture))] to get a single
/// browserless run with a live IAPIRequestContext.
///
/// BaseTest itself never knows which concrete subclasses exist - it only branches on the
/// ExecutionTarget value NUnit hands it through the constructor. That's what keeps UI and API
/// tests sharing one setup/teardown pipeline (config, timeouts, screenshots/video/trace) without
/// the API tests ever paying for a browser process.
/// </summary>
public abstract class BaseTest
{
    protected readonly ExecutionTarget Target;
    protected static PlaywrightSettings Settings => ConfigReader.Settings;

    private IPlaywright _playwright = null!;
    private IBrowser? _browser;

    /// <summary>Populated for UI targets only.</summary>
    protected IBrowserContext Context { get; private set; } = null!;

    /// <summary>Populated for UI targets only.</summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>Populated for the Api target only.</summary>
    protected IAPIRequestContext ApiContext { get; private set; } = null!;

    protected bool IsApiTarget => Target == ExecutionTarget.Api;

    protected BaseTest(ExecutionTarget target)
    {
        Target = target;
    }

    // ---------------------------------------------------------------------
    // Fixture-level (runs once per browser/API fixture instance, not per test)
    // ---------------------------------------------------------------------

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _playwright = await Playwright.CreateAsync();

        if (IsApiTarget)
        {
            var BaseURL = Settings.TestSettings.ApiBaseUrl;
            ApiContext = await _playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                BaseURL = Settings.TestSettings.ApiBaseUrl,
                Timeout = Settings.TimeoutSettings.DefaultTimeout,
                IgnoreHTTPSErrors = true
            });
            return;
        }

        _browser = await LaunchBrowserAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (IsApiTarget)
        {
            if (ApiContext is not null)
            {
                await ApiContext.DisposeAsync();
            }
        }
        else if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright.Dispose();
    }

    private Task<IBrowser> LaunchBrowserAsync()
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = Settings.BrowserSettings.Headless,
            SlowMo = Settings.BrowserSettings.SlowMo,
            Args = Settings.BrowserSettings.Args
        };

        return Target switch
        {
            ExecutionTarget.Chromium => _playwright.Chromium.LaunchAsync(launchOptions),
            ExecutionTarget.Firefox => _playwright.Firefox.LaunchAsync(launchOptions),
            ExecutionTarget.Webkit => _playwright.Webkit.LaunchAsync(launchOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(Target), Target, "Unsupported browser target.")
        };
    }

    // ---------------------------------------------------------------------
    // Test-level (runs before/after every [Test])
    // ---------------------------------------------------------------------

    [SetUp]
    public async Task SetUpAsync()
    {
        if (IsApiTarget)
        {
            // API tests share the fixture-level ApiContext; nothing per-test to create.
            return;
        }

        var contextOptions = new BrowserNewContextOptions
        {
            BaseURL = Settings.TestSettings.UiBaseUrl,
        };

        if (ShouldRecordVideo())
        {
            contextOptions.RecordVideoDir = GetArtifactDirectory("videos");
        }

        Context = await _browser!.NewContextAsync(contextOptions);
        Context.SetDefaultTimeout(Settings.TimeoutSettings.DefaultTimeout);
        Context.SetDefaultNavigationTimeout(Settings.TimeoutSettings.NavigationTimeout);

        if (ShouldTrace())
        {
            await Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }

        Page = await Context.NewPageAsync();
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        if (IsApiTarget)
        {
            return;
        }

        var testFailed = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed;
        var testName = SanitizeFileName($"{TestContext.CurrentContext.Test.Name}_{Target}");

        if (testFailed && ShouldTakeScreenshot())
        {
            var path = GetArtifactPath("screenshots", $"{testName}.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            TestContext.AddTestAttachment(path);
        }

        if (ShouldTrace())
        {
            var keepTrace = !IsRetainOnFailure(Settings.ReportingSettings.TraceMode) || testFailed;
            if (keepTrace)
            {
                var path = GetArtifactPath("traces", $"{testName}.zip");
                await Context.Tracing.StopAsync(new TracingStopOptions { Path = path });
                TestContext.AddTestAttachment(path);
            }
            else
            {
                await Context.Tracing.StopAsync();
            }
        }

        // Video is finalized once the page/context close; capture the path first if we need
        // to discard it for passing tests under "RetainOnFailure".
        var videoPath = ShouldRecordVideo() ? await TryGetVideoPathAsync() : null;

        await Page.CloseAsync();
        await Context.CloseAsync();

        if (videoPath is not null)
        {
            var discardVideo = IsRetainOnFailure(Settings.ReportingSettings.RecordVideo) && !testFailed;
            if (discardVideo && File.Exists(videoPath))
            {
                File.Delete(videoPath);
            }
            else if (File.Exists(videoPath))
            {
                TestContext.AddTestAttachment(videoPath);
            }
        }
    }

    private async Task<string?> TryGetVideoPathAsync()
    {
        var video = Page.Video;
        return video is null ? null : await video.PathAsync();
    }

    // ---------------------------------------------------------------------
    // Reporting helpers
    // ---------------------------------------------------------------------

    private static bool IsRetainOnFailure(string mode) =>
        mode.Equals("RetainOnFailure", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldTakeScreenshot() =>
        !Settings.ReportingSettings.TakeScreenshot.Equals("Off", StringComparison.OrdinalIgnoreCase)
        && !Settings.ReportingSettings.TakeScreenshot.Equals("Never", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldRecordVideo() =>
        !Settings.ReportingSettings.RecordVideo.Equals("Off", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldTrace() =>
        !Settings.ReportingSettings.TraceMode.Equals("Off", StringComparison.OrdinalIgnoreCase);

    private static string GetArtifactDirectory(string subFolder)
    {
        var dir = Path.Combine(Settings.ReportingSettings.ArtifactsPath, subFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetArtifactPath(string subFolder, string fileName) =>
        Path.Combine(GetArtifactDirectory(subFolder), fileName);

    private static string SanitizeFileName(string name)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }
        return name;
    }
}
