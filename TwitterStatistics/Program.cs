using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TwitterApp {
    public class Program {
        private static readonly string BearerToken = Environment.GetEnvironmentVariable("BearerToken");

        private static readonly HttpClient client = new HttpClient();
        public static readonly ConcurrentDictionary<string, int> hashtags = new ConcurrentDictionary<string, int>();
        private static int tweetCount = 0;

        public static async Task Main(string[] args) {
            try {
                // Replace YOUR_API_KEY and YOUR_API_SECRET with your actual API key and API secret
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + BearerToken);

                var payload = new {
                    add = new[] {
                        new {
                            value = "place_country:US"
                        },
                        new {
                            value = "\"#\""
                        }
                    },
                    expansions = new[] {
                        "author_id"
                    },
                    user_fields = new[] {
                        "username"
                    }
                 };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send the request
                string url = "https://api.twitter.com/2/tweets/search/stream/rules";
                using (HttpResponseMessage response = await client.PostAsync(url, content)) {
                    // Print the response status code
                    Console.WriteLine($"Status code: {(int)response.StatusCode}");

                    // Read and print the response body
                    string body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(body);
                }

                // Start consuming the Twitter API stream endpoint
                string streamUrl = "https://api.twitter.com/2/tweets/sample/stream";
                using (HttpResponseMessage response = await client.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
                using (var stream = await response.Content.ReadAsStreamAsync()) {
                    // Process the stream asynchronously
                    await ProcessStreamAsync(stream);
                }
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public static async Task ProcessStreamAsync(System.IO.Stream stream) {
            // Use a CancellationTokenSource to cancel the stream reader
            var cts = new CancellationTokenSource();
            var buffer = new byte[1024];

            // Set up a timer to log the stats every 5 seconds
            Timer statsTimer = new Timer(async (state) => {
                Console.WriteLine($"Number of tweets received: {tweetCount}");
                Console.WriteLine("Top 10 hashtags for this batch of tweets:");
                foreach (var hashtag in hashtags.OrderByDescending(x => x.Value).Take(10)) {
                    Console.WriteLine($"{hashtag.Key}: {hashtag.Value}");
                    Console.WriteLine("---------------------");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            // Read the stream asynchronously
            using (var streamReader = new System.IO.StreamReader(stream)) {
                while (!streamReader.EndOfStream && !cts.Token.IsCancellationRequested) {
                    // Read a line from the stream
                    string line = await streamReader.ReadLineAsync();

                    if (line != null && line.Contains("#")) {
                        // Process the line asynchronously
                        _ = Task.Run(() => ProcessLineAsync(line));
                    }
                }
            }

            // Cancel the timer and dispose of the CancellationTokenSource
            statsTimer.Dispose();
            cts.Cancel();
            cts.Dispose();
        }

        public static async Task ProcessLineAsync(string line) {
            if (!string.IsNullOrEmpty(line)) {
                // Parse the line as JSON
                JObject tweet = JObject.Parse(line);

                // Increment the tweet count
                Interlocked.Increment(ref tweetCount);

                string input = line;

                var regex = new Regex(@"(?<=^|\s)#([A-Za-z0-9_]+)");

                MatchCollection matches = regex.Matches(input);

                foreach (Match match in matches) {
                    string hashtag = match.Value;
                    hashtags.AddOrUpdate(hashtag, 1, (key, oldValue) => oldValue + 1);
                }
            }
        }

        public static async Task<List<KeyValuePair<string, int>>> GetTopHashtagsAsync(int topCount) {
            return await Task.Run(() => hashtags.OrderByDescending(kvp => kvp.Value).Take(topCount).ToList());
        }
    }
}