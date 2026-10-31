# PlaywrightAutomationFramework
   [![Playwright Tests](https://github.com/luisStPaul/PlaywrightProjects/actions/workflows/playwright-tests.yml/badge.svg)](https://github.com/luisStPaul/PlaywrightProjects/actions/workflows/playwright-tests.yml)
   
A single, config-driven C# + Playwright + NUnit framework that runs **UI tests across
Chromium/Firefox/Webkit** and **browserless API tests** through one unified `BaseTest`.

## How the unification works

1. `Config/ExecutionTarget.cs` defines one enum: `Chromium | Firefox | Webkit | Api`.
2. `Config/FixtureSources.cs` exposes two static generators of that enum:
   - `UiBrowserFixtures()` — one value per browser listed in `appsettings.json`.
   - `ApiFixture()` — a single `Api` value.
3. Each concrete test class picks a generator via NUnit's `[TestFixtureSource]` attribute
   and forwards the enum value into `BaseTest`'s constructor. NUnit instantiates the class
   once per value, giving you one fixture per browser (or one fixture, browser-free, for API).
4. `Base/BaseTest.cs` branches **only** on that one enum value:
   - `Api` → creates an `IAPIRequestContext` (no `IBrowser`/`IPage` at all).
   - Anything else → launches the matching Playwright browser, opens a context/page, and
     wires up tracing/video per `ReportingSettings`.

Every test class — UI or API — inherits the same config loading, timeouts, and
screenshot/video/trace-on-failure reporting from one place.

## Project layout

```
PlaywrightAutomationFramework/
├── appsettings.json                # provided settings (browsers, timeouts, reporting, etc.)
├── Config/
│   ├── PlaywrightSettings.cs       # POCOs bound from appsettings.json
│   ├── ConfigReader.cs             # loads + binds appsettings.json once (lazy, thread-safe)
│   ├── ExecutionTarget.cs          # the enum: Chromium | Firefox | Webkit | Api
│   └── FixtureSources.cs           # TestFixtureSource generators
├── Base/
│   └── BaseTest.cs                 # unified OneTimeSetUp/SetUp/TearDown/OneTimeTearDown
└── Tests/
    ├── Ui/PlaywrightHomePageTests.cs   # sample UI tests, runs x3 (one per browser)
    └── Api/OpenMeteoApiTests.cs        # sample API tests, runs x1 (no browser)
```

## Setup

```bash
# from the PlaywrightAutomationFramework directory
dotnet restore
dotnet build

# install the Playwright browsers (one-time, or after upgrading the Playwright package)
pwsh bin/Debug/net8.0/playwright.ps1 install --with-deps
# (on machines without PowerShell: dotnet tool install --global Microsoft.Playwright.CLI
#  then: playwright install --with-deps)
```

## Running

```bash
dotnet test
```

This runs both UI fixture sets (Chromium, Firefox, Webkit) and the API fixture set.
Filter as usual with NUnit's category/name filters, e.g.:

```bash
dotnet test --filter "FullyQualifiedName~PlaywrightHomePageTests"   # UI only
dotnet test --filter "FullyQualifiedName~OpenMeteoApiTests"         # API only
```

## Adding a new UI test class

```csharp
[TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.UiBrowserFixtures))]
public class MyFeatureTests : BaseTest
{
    public MyFeatureTests(ExecutionTarget target) : base(target) { }

    [Test]
    public async Task Something_ShouldHappen()
    {
        await Page.GotoAsync("/some-path");
        // ...
    }
}
```

## Adding a new API test class

```csharp
[TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.ApiFixture))]
public class MyApiTests : BaseTest
{
    public MyApiTests(ExecutionTarget target) : base(target) { }

    [Test]
    public async Task Endpoint_ShouldReturnExpectedShape()
    {
        var response = await ApiContext.GetAsync("/some/endpoint");
        Assert.That(response.Ok, Is.True);
    }
}
```

## Configuration notes

- `ReportingSettings.TakeScreenshot`, `RecordVideo`, and `TraceMode` each accept
  `Off` / `RetainOnFailure` / `On` (screenshot also treats `OnFailure` as an alias for
  "capture only on failure", matching the value already in `appsettings.json`).
- `BrowserSettings.TargetBrowsers` directly controls how many UI fixture instances get
  created — trim it to `["chromium"]` for a fast local loop, for example.
- `UserAccounts` (Admin/Standard) are bound into `Settings.UserAccounts` for tests that need
  credentials; treat the values in source control as placeholders and inject real secrets via
  environment variables (`PlaywrightSettings__UserAccounts__AdminUser__Password=...`) or your
  CI's secret store rather than committing them — `ConfigReader` already layers in environment
  variables on top of the JSON file.
- To point at a different environment, set `PW_ENVIRONMENT=QA` (etc.) and drop an optional
  `appsettings.QA.json` next to `appsettings.json` with just the overridden keys.
- The UI tests need to be run with the setting 'headless: false' due to the site’s current detection
  rules: it displays a CAPTCHA if not run in headed mode. 
  
