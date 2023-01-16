using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TwitterStatistics.Models;

namespace TwitterApp {
    public class Program {
        // Grab the twitter token from the environment variables. Keeps the token more secure to not have it in the local config.
        private static readonly string? _bearerToken = Environment.GetEnvironmentVariable("BearerToken");

        // Instantiate HTTP Client
        private static readonly HttpClient _httpClient = new HttpClient();

        // Instantiate the dictionary that the various concurrent threads can write to.
        public static readonly ConcurrentDictionary<string, int> _hashtags = new ConcurrentDictionary<string, int>();

        // And an int that we can keep track of the accuring tweet count.
        public static int _tweetCount = 0;

        // Declare the rules payload list
        public static List<Add> _rulesPayload { get; set; } = new List<Add>();

        public static async Task Main(string[] args) {
            var configuration = new ConfigurationBuilder()
                                    .AddJsonFile($"appsettings.json", false);

            var _config = configuration.Build();

            try {
                // Add Bearer Token to all requests
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _bearerToken);

                //Filter by chatGPT and by Hashtag. Could turn this into user input in the future.
                AddRules("chatgpt");
                AddRules("#");

                // Call the rules API with any provided rules.
                await ProcessRulesAsync(_config["TwitterConfig:RulesApiUrl"]);
                await ConsumeFilteredStreamAsync(_config["TwitterConfig:StreamApiUrl"]);
                
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static void AddRules(string value) {
            // Add the provided rule
            Console.WriteLine($"Adding new rule: {value}");
            Add add = new Add();
            add.value = value;

            _rulesPayload.Add(add);
        }

        private static async Task ProcessRulesAsync(string url) {
            try {
                Rules _rules = new Rules();
                _rules.add = _rulesPayload;

                var json = JsonConvert.SerializeObject(_rules);
                var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");

                // Send the rules request to the Rules API
                string rulesUrl = url;

                // Make the API Call
                using (HttpResponseMessage response = await _httpClient.PostAsync(rulesUrl, content)) {
                    // Print the response status code
                    Console.WriteLine($"Status code: {(int)response.StatusCode}");

                    // Read and print the response body
                    string body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(body);
                }
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static async Task ConsumeFilteredStreamAsync(string url) {
            // Start consuming the Twitter filtered stream endpoint
            string streamUrl = url;

            using (HttpResponseMessage response = await _httpClient.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None))
            using (var stream = await response.Content.ReadAsStreamAsync()) {
                // Process the stream asynchronously
                await ProcessStreamAsync(stream);
            }
        }

        public static async Task ProcessStreamAsync(System.IO.Stream stream) {
            // Use a CancellationTokenSource to cancel the stream reader
            var cts = new CancellationTokenSource();
            var buffer = new byte[1024];

            // Set up a timer to write log the stats every 5 seconds
            Timer statsTimer = new Timer(async (state) => {
                Console.WriteLine($"Number of tweets received: {_tweetCount}");
                Console.WriteLine("Top 10 hashtags for this batch of tweets:");

                foreach (var hashtag in _hashtags.OrderByDescending(x => x.Value).Take(10)) {
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
                        _ = Task.Run(() => ProcessTweetAsync(line));
                    }
                }
            }

            // Cancel the timer and dispose of the CancellationTokenSource
            statsTimer.Dispose();
            cts.Cancel();
            cts.Dispose();
        }

        public static int GetNextIndex() {
            return Interlocked.Increment(ref _tweetCount);
        }

        public static async Task ProcessTweetAsync(string line) {
            if (!string.IsNullOrEmpty(line)) {
                // Parse the line as JSON
                JObject tweet = JObject.Parse(line);

                // Increment the tweet count
                GetNextIndex();

                string input = line;

                var regex = new Regex(@"(?<=^|\s)#([A-Za-z0-9_]+)");

                MatchCollection matches = regex.Matches(input);

                foreach (Match match in matches) {
                    string hashtag = match.Value;
                    _hashtags.AddOrUpdate(hashtag, 1, (key, oldValue) => oldValue + 1);
                }
            }
        }

        public static async Task<List<KeyValuePair<string, int>>> GetTopHashtagsAsync(int topCount) {
            return await Task.Run(() => _hashtags.OrderByDescending(kvp => kvp.Value).Take(topCount).ToList());
        }
    }
}