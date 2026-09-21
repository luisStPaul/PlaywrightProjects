using NUnit.Framework;
using PlaywrightAutomationFramework.Base;
using PlaywrightAutomationFramework.Config;
using System.Text.Json;

namespace PlaywrightAutomationFramework.Tests.Api;

/// <summary>
/// ApiFixture() yields exactly one ExecutionTarget.Api value, so this class runs once -
/// BaseTest sees IsApiTarget == true and skips browser/context/page setup entirely.
/// </summary>
[TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.ApiFixture))]
public class OpenMeteoApiTests : BaseTest
{
    public OpenMeteoApiTests(ExecutionTarget target) : base(target)
    {
    }

    [Test]
    public async Task GetForecast_ForBerlin_ShouldReturnOkWithCurrentWeather()
    {
        var response = await ApiContext.GetAsync(
            "/v1/forecast?latitude=52.52&longitude=13.41&current_weather=true");

        Assert.That(response.Ok, Is.True, $"Expected 2xx but got {response.Status} {response.StatusText}");

        using var json = JsonDocument.Parse(await response.TextAsync());
        var hasCurrentWeather = json.RootElement.TryGetProperty("current_weather", out var currentWeather);

        Assert.That(hasCurrentWeather, Is.True, "Response should contain a current_weather object.");
        Assert.That(currentWeather.TryGetProperty("temperature", out _), Is.True, "current_weather should contain a temperature field.");
    }

    [Test]
    public async Task GetForecast_WithoutCoordinates_ShouldReturnBadRequest()
    {
        var response = await ApiContext.GetAsync("/v1/forecast?latitude=452&longitude=1341&current_weather=true");

        Assert.That(response.Status, Is.EqualTo(400));
    }
}
