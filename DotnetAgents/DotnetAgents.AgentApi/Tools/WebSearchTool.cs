using DotnetAgents.Core.Interfaces;
using Microsoft.Extensions.Configuration; // For IConfiguration
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System; // For Uri
using System.Linq;

namespace DotnetAgents.AgentApi.Tools
{
    public class WebSearchTool : ITool
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public WebSearchTool(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public string Name => "web_search";
        public string Description => "Searches the web for a query and returns the top results.";

        public string GetJsonSchema()
        {
            return @"
            {
                ""type"": ""object"",
                ""properties"": { ""query"": { ""type"": ""string"" } },
                ""required"": [""query""]
            }";
        }

        private record SearchArgs(string query);
        private record SearchResult(string title, string snippet, string source);

        public async Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JsonSerializer.Deserialize<SearchArgs>(jsonArguments);

            if (args == null || string.IsNullOrWhiteSpace(args.query))
            {
                return "Error: Invalid arguments for web_search tool.";
            }

            var apiKey = _config["GoogleSearch:ApiKey"];
            var cxId = _config["GoogleSearch:CxId"];
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(cxId))
            {
                return "Error: WebSearchTool is not configured. Missing ApiKey or CxId.";
            }

            var client = _httpClientFactory.CreateClient("GoogleSearch");
            var url = $"https[://]www.googleapis.com/customsearch/v1?key={apiKey}&cx={cxId}&q={Uri.EscapeDataString(args.query)}";

            try
            {
                
                // This is mock data, as per the guide [cite: 283-284]
                // You would deserialize the real Google response here.
                var mockResults = new[]
                {
                    new SearchResult("Example Title 1", "Snippet for result 1...", "example.com"),
                    new SearchResult("Example Title 2", "Snippet for result 2...", "anothersite.org")
                };

                // Simulating a successful call
                await Task.CompletedTask;

                return "Search results:\n" + string.Join("\n---\n", mockResults.Select(r => $"Title: {r.title}\nSnippet: {r.snippet}\nSource: {r.source}"));
            }
            catch (Exception ex)
            {
                return $"Error during web search: {ex.Message}";
            }
        }
    }
}