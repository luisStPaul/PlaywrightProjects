using PlaywrightAutomationFramework.Model;
using System.Text;
using System.Text.Json;

namespace PlaywrightAutomationFramework.Utilities
{
    public class Helpers
    {
        private static Random rand = new Random();
        public static readonly char[] AllowedChars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        public static readonly char[] LowercaseAllowedChars =
            "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        public static readonly char[] LowercaseAllowedLetters =
            "abcdefghijklmnopqrstuvwxyz".ToCharArray();

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

        public record Result(bool ImagesVisible, string Evidence);

        public static string Generate(int length, char[] allowedChars)
        {
            // Allocates the string once and provides a Span<char> representing its buffer
            return string.Create(length, allowedChars, (buffer, chars) =>
            {
                Random.Shared.GetItems(chars, buffer);
            });
        }

        public static string GenerateSentence(char[] allowedChars, int wordCount)
        {
            StringBuilder sentence = new StringBuilder();

            for (int i = 0; i < wordCount; i++)
            {
                // Determine a random length for the current word (e.g., 3 to 10 characters)
                int wordLength = rand.Next(3, 11);

                for (int j = 0; j < wordLength; j++)
                {
                    char randomChar = allowedChars[rand.Next(allowedChars.Length)];

                    if (j == 0)
                    {
                        sentence.Append(char.ToUpper(randomChar));
                    }
                    else
                    {
                        sentence.Append(char.ToLower(randomChar));
                    }
                }

                // Add a space between words, but not after the final word
                if (i < wordCount - 1)
                {
                    sentence.Append(' ');
                }
            }

            return sentence.ToString();
        }

        public static async Task<Result> AreImagesVisibleAsync_OllamaAgent(byte[] screenshotPng)
        {
            var payload = new
            {
                model = "gemma3:4b",
                //"llama3.2-vision",   // any vision-capable model you've pulled

                stream = false,         // return one JSON object instead of a stream
                format = "json",        // constrains output to valid JSON
                options = new { temperature = 0, num_predict = 300 },
                messages = new[]
                {
            new
            {
                role = "user",
                content = "Respond with JSON only: {\"images_visible\": boolean, \"evidence\": string}. " +
                          "Are any photographs, illustrations, or graphic images rendered on this page? " +
                          "Ignore text, icons drawn with CSS, and broken-image placeholders and" +
                          "text labels and alt text do not count as images.",
                images = new[] { Convert.ToBase64String(screenshotPng) }
            }
        }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await Http.SendAsync(request);
            try
            {
                //response.EnsureSuccessStatusCode();


                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Ollama returned {(int)response.StatusCode}: {body}");
                }

                using var docum = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }


            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var text = doc.RootElement.GetProperty("message").GetProperty("content").GetString()!;

            // Still worth stripping fences; some models add them despite format=json
            text = text.Replace("```json", "").Replace("```", "").Trim();

            using var verdict = JsonDocument.Parse(text);
            return new Result(
                verdict.RootElement.GetProperty("images_visible").GetBoolean(),
                verdict.RootElement.GetProperty("evidence").GetString() ?? "");

        }

        public static async Task<Result> AreImagesVisibleAsync_Claude(byte[] screenshotPng)
        {
            var payload = new
            {
                model = "claude-sonnet-5",
                max_tokens = 300,
                messages = new[]
                {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = "image/png",
                                data = Convert.ToBase64String(screenshotPng)
                            }
                        },
                        new
                        {
                            type = "text",
                            text = "Respond with JSON only: {\"images_visible\": boolean, \"evidence\": string}. " +
                                   "Are any photographs, illustrations, or graphic images rendered on this page? " +
                                   "Ignore text, icons drawn with CSS, and broken-image placeholders."
                        }
                    }
                }
            }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString()!;

            // Strip code fences in case the model wraps the JSON
            text = text.Replace("```json", "").Replace("```", "").Trim();

            using var verdict = JsonDocument.Parse(text);
            return new Result(
                verdict.RootElement.GetProperty("images_visible").GetBoolean(),
                verdict.RootElement.GetProperty("evidence").GetString() ?? "");
        }

        public static List<BrandItem> GenerateBrandItems(List<string> brandItemNames)
        {
            List<BrandItem> brandItems = new List<BrandItem>();
            for (int i = 0; i < brandItemNames.Count; i++)
            {
                var brandItem = new BrandItem();
                brandItem.id = Generate(26, LowercaseAllowedChars);
                brandItem.name = brandItemNames[i];
                brandItem.slug = brandItemNames[i].ToLower().Replace(" ", "-");
                brandItems.Add(brandItem);
            }

            return brandItems;
        }

        public static List<string> GenerateBrandNames(int brandsCount)
        {
            List<string> brands = new List<string>();
            for (int i = 0; i < brandsCount; i++)
            {
                brands.Add(GenerateSentence(LowercaseAllowedLetters, rand.Next(2, 4)));
            }

            return brands;
        }
    }
}
