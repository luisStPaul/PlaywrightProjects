using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightAutomationFramework.Base;
using PlaywrightAutomationFramework.Config;
using PlaywrightAutomationFramework.Model;
using PlaywrightAutomationFramework.Utilities;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using static PlaywrightAutomationFramework.Utilities.Helpers;

namespace PlaywrightAutomationFramework.Tests.Ui
{
    [TestFixtureSource(typeof(FixtureSources), nameof(FixtureSources.UiBrowserFixtures))]
    public class TestingPlaywrightAPICapabilities : BaseTest
    {
        public TestingPlaywrightAPICapabilities(ExecutionTarget target) : base(target)
        {
        }

        [Test]
        public async Task Test_ModifyRealApiResponse_OnTheFly()
        {
            // Register the interception BEFORE the app makes the call
            await Page.RouteAsync("**/products", async route =>
            {
                // Let the app's request go to the real backend and capture the response
                var response = await route.FetchAsync();

                var root = JsonNode.Parse(await response.TextAsync())!;

                // Modify the fields to verify in the UI
                var products = root["data"]!.AsArray();
                if (products != null && products.Count > 0)
                {
                    products[0]["name"] = "Modified Name";
                }

                // Send the modified response back to the app
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Response = response,              // keeps original status and headers
                    Body = root.ToJsonString(),
                    ContentType = "application/json"
                });
            });

            // Navigate to the app which triggers the API call
            await Page.GotoAsync("/");

            // Verify that some controls on the page are displayed as expected
            await Page.GetByRole(AriaRole.Button, new() { Name = "Categories" }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Hand Tools" }).ClickAsync();
            await Page.Locator("[data-test=\"sort\"]").SelectOptionAsync(new[] { "name,asc" });

            // Verify the UI displays the modified data
            await Assertions.Expect(Page.GetByText("Modified Name")).ToBeVisibleAsync();
        }

        [Test]
        public async Task Test_MockApiResponse_Successfully()
        {
            // Define a NEW brands API response to display on the page
            int brandsCount = 10;
            List<string> brands = GenerateBrandNames(brandsCount);
            List<BrandItem> brandItems = GenerateBrandItems(brands);

            // Intercept profile endpoint
            await Page.RouteAsync("**/brands", async route =>
            {
                // Fetch the response from the actual server
                var response = await route.FetchAsync();

                // Fulfill the route using the updated payload
                await route.FulfillAsync(new()
                {
                    Response = response, // Reuses real status codes and headers
                    Body = JsonSerializer.Serialize(brandItems)
                });
            });

            // Navigate to the app which triggers the API call
            await Page.GotoAsync("https://practicesoftwaretesting.com/");

            // Assert the application adapted properly to the modified API response
            for (int i = 0; i < brandsCount; i++)
            {
                await Assertions.Expect(Page.GetByText(brands[i])).ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task Test_HandleServerAndNetworkFailures_Successfully()
        {
            // Scenario A: Simulate a 500 Internal Server Error
            await Page.RouteAsync("**/users/login", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 401,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new { error = "Unauthorized" })
                });
            });

            // Scenario B: Simulate a broken internet connection / firewall drop
            await Page.RouteAsync("**/api/analytics", async route =>
            {
                // Aborts the connection entirely at the network layer
                await route.AbortAsync("failed");
            });

            await Page.GotoAsync("/");

            await Page.Locator("[data-test=\"nav-sign-in\"]").ClickAsync();
            await Page.Locator("[data-test=\"email\"]").ClickAsync();
            await Page.Locator("[data-test=\"email\"]").FillAsync("admin@practicesoftwaretesting.com");
            await Page.Locator("[data-test=\"password\"]").ClickAsync();
            await Page.Locator("[data-test=\"password\"]").FillAsync("welcome01");
            await Page.Locator("[data-test=\"login-submit\"]").ClickAsync();

            // Verify UI error boundaries display user-friendly error messages
            await Assertions.Expect(Page.GetByText("Invalid email or password")).ToBeVisibleAsync();
        }

        [Test]
        public async Task Test_BlockImagesAndTrackers_Successfully()
        {
            // Intercept all network traffic
            await Page.RouteAsync("**/*", async route =>
            {
                var request = route.Request;

                // Verify resource type classifications
                if (request.ResourceType == "image" || request.ResourceType == "font" || request.Url.Contains("google-analytics"))
                {
                    await route.AbortAsync(); // Drops the request immediately
                }
                else
                {
                    await route.ContinueAsync(); // Allows document, stylesheet, and core scripts to pass
                }
            });

            // Page will load noticeably faster without pulling image bytes
            await Page.GotoAsync("/");

            // Verify some controls on the page (only images should be blocked, all other elements should be correctly 
            // rendered and functional
            await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Practice Software Testing -" })).ToBeVisibleAsync();
            await Assertions.Expect(Page.GetByText("Practice Black Box Testing &")).ToBeVisibleAsync();
            await Assertions.Expect(Page.Locator("[data-test=\"search-query\"]")).ToBeVisibleAsync();

            // Uses Browser's document to verify images were not actually rendered
            var loadedImages = await Page.EvaluateAsync<int>(
                @"() => [...document.querySelectorAll('img')]
                    .filter(i => i.complete && i.naturalWidth > 0).length");
            Assert.That(loadedImages, Is.EqualTo(0));

            // The elements are still there (optional sanity check)
            var images = Page.Locator("img.card-img-top");
            await Assertions.Expect(images).Not.ToHaveCountAsync(0);

            // Uses Playwright's selector engine to verify images were not loaded
            var loadedCount = await images.EvaluateAllAsync<int>(
                "imgs => imgs.filter(i => i.complete && i.naturalWidth > 0).length");
            Assert.That(loadedCount, Is.EqualTo(0), "Some images loaded despite the block.");

            // Wait until every matching image is finished loading AND broken (blocked)
            await Page.WaitForFunctionAsync(
                "sel => [...document.querySelectorAll(sel)].every(i => i.complete && i.naturalWidth === 0)",
                "img.card-img-top",
                new() { Timeout = 5000 });
        }

        [Test]
        public async Task Test_ImagesAreBlockedAndNotRendered_Successfully()
        {
            var blocked = new ConcurrentBag<string>();
            var failures = new ConcurrentBag<(string Url, string? Error)>();

            await Page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image")
                {
                    blocked.Add(route.Request.Url);
                    await route.AbortAsync("blockedbyclient");
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            // The requestfailed error text is what proves why an image isn't displayed. Without it,
            // an image could be missing because of a 404, a lazy-load bug, or a real defect, and you
            // couldn't distinguish those from your block.
            Page.RequestFailed += (_, request) =>
            {
                if (request.ResourceType == "image")
                {
                    failures.Add((request.Url, request.Failure));
                }
            };

            await Page.GotoAsync("/", new()
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            // The interceptor fired, and failures match the block reason
            Assert.That(blocked, Is.Not.Empty, "Route handler never intercepted an image.");
            Assert.That(failures.All(f => f.Error == "net::ERR_BLOCKED_BY_CLIENT.Inspector"), Is.True,
                "Some image requests failed for a reason other than the block.");

            // No <img> actually rendered
            var loadedImages = await Page.EvaluateAsync<int>(
                @"() => [...document.querySelectorAll('img')]
                    .filter(i => i.complete && i.naturalWidth > 0).length");

            Assert.That(loadedImages, Is.EqualTo(0));
        }


        [Test]
        [Ignore("Vision check is unreliable with local Ollama models")]
        // Vision models are probabilistic, so a pass or fail can flip between runs.
        // Pair the AI verdict with the deterministic results, so that when the two disagree
        // (e.g., the DOM says zero loaded images but the model sees a picture),
        // it may be a particular situation like an image served via inline SVG or a canvas.
        public async Task Test_ImagesAreBlockedAndNotRendered_OllamaAgent_Successfully()
        {
            /// Handlers can fire on different threads, so I used ConcurrentBag rather than List for the captured data.
            var blocked = new ConcurrentBag<string>();

            await Page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image")
                {
                    blocked.Add(route.Request.Url);
                    await route.AbortAsync("blockedbyclient");
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await Page.GotoAsync("/", new()
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var screenshot = await Page.ScreenshotAsync(new() { FullPage = true });
            var verdict = await Helpers.AreImagesVisibleAsync_OllamaAgent(screenshot);

            Assert.That(verdict.ImagesVisible, Is.False,
                $"Vision model saw images: {verdict.Evidence}");
        }

        [Test]
        [Ignore("Run this test only if Claude AI is available (requires key in Environment Variables)")]
        // This test is identical to the previous test except that it uses Claude as the 
        // AI agent that performs the visual verification. Again the results are probabilistic
        // so you should always run the deterministic test. 
        public async Task Test_ImagesAreBlockedAndNotRendered_Claude_Successfully()
        {
            /// Handlers can fire on different threads, so I used ConcurrentBag rather than List for the captured data.
            var blocked = new ConcurrentBag<string>();

            await Page.RouteAsync("**/*", async route =>
            {
                if (route.Request.ResourceType == "image")
                {
                    blocked.Add(route.Request.Url);
                    await route.AbortAsync("blockedbyclient");
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            await Page.GotoAsync("/", new()
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var screenshot = await Page.ScreenshotAsync(new() { FullPage = true });
            var verdict = await Helpers.AreImagesVisibleAsync_Claude(screenshot);

            Assert.That(verdict.ImagesVisible, Is.False,
                $"Vision model saw images: {verdict.Evidence}");
        }
    }
}
