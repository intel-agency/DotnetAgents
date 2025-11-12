using DotnetAgents.Core.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DotnetAgents.Core
{
    public class OpenAiClient : IOpenAiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenAiClient> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public OpenAiClient(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<OpenAiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            _baseUrl = config["OpenRouter:BaseUrl"]/* ?? "https://openrouter.ai/api/v1"*/;
            _apiKey = config["OpenRouter:ApiKey"] ?? throw new InvalidOperationException("OpenRouter:ApiKey not configured");
            // TEMPORARY DEBUGGING:
            //_apiKey = "sk-or-v1-alskdfjldaksfjaksdjfkaljfapjfk pajf ;lsdksla;faDUMMY_KEYdskljmfpiksojf";
            // TEMPORARY DEBUGGING:
            _model = config["OpenRouter:Model"] ?? throw new InvalidOperationException("OpenRouter:Model not configured");
        }

        public async Task<LlmResponse> GetCompletionAsync(List<Message> history, List<string> toolSchemas)
        {
            var client = _httpClientFactory.CreateClient("OpenAiClient");
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // OpenRouter requires these headers
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/nam20485/DotnetAgents");
            client.DefaultRequestHeaders.Add("X-Title", "DotnetAgents");

            var requestPayload = BuildRequestPayload(history, toolSchemas);

            try
            {
                var response = await client.PostAsJsonAsync("chat/completions", requestPayload, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("LLM API call failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                    return new LlmResponse($"Error: API call failed. {errorBody}", null);
                }
                
                // First, read the response as a string
                var responseBody = await response.Content.ReadAsStringAsync();

                OpenAiResponse openAiResponse;
                try
                {
                    // Now, try to deserialize the string
                    openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseBody, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    // If it fails, log the *actual* HTML we received. This is the real error.
                    _logger.LogError(jsonEx, "Failed to deserialize LLM response. The server (OpenRouter) sent HTML/XML instead of JSON. Response Body: {ResponseBody}", responseBody);
                    return new LlmResponse($"Error: Invalid JSON response from server. {jsonEx.Message}", null);
                }                

                var choice = openAiResponse?.Choices.FirstOrDefault();
                if (choice == null)
                {
                    _logger.LogWarning("LLM response was successful but contained no choices.");
                    return new LlmResponse("Error: No response choice from model.", null);
                }

                // Map to our core LlmResponse
                string responseContent = choice.Message.Content;
                List<ToolCall> domainToolCalls = null;

                if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Any())
                {
                    responseContent ??= "[tool call]"; // Ensure content isn't null if only tools are called
                    domainToolCalls = choice.Message.ToolCalls
                        .Select(tc => new ToolCall(tc.Function.Name, tc.Function.Arguments))
                        .ToList();
                }

                return new LlmResponse(responseContent, domainToolCalls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LLM API call.");
                return new LlmResponse($"Error: {ex.Message}", null);
            }
        }

        private OpenAiRequest BuildRequestPayload(List<Message> history, List<string> toolSchemas)
        {
            var request = new OpenAiRequest
            {
                Model = _model,
                Messages = history.Select(m => new RequestMessage(m.Role, m.Content)).ToList()
            };

            if (toolSchemas != null && toolSchemas.Any())
            {
                request.Tools = toolSchemas.Select(schema =>
                    new RequestTool(JsonSerializer.Deserialize<JsonElement>(schema))
                ).ToList();
            }

            return request;
        }

        #region Private Helper Classes for JSON Serialization

        private class OpenAiRequest
        {
            public string Model { get; set; }
            public List<RequestMessage> Messages { get; set; }
            public List<RequestTool> Tools { get; set; }
        }

        private record RequestMessage(string Role, string Content);

        private class RequestTool
        {
            public string Type { get; } = "function";
            public JsonElement Function { get; set; }

            public RequestTool(JsonElement functionSchema)
            {
                Function = functionSchema;
            }
        }

        private class OpenAiResponse
        {
            public List<ResponseChoice> Choices { get; set; }
        }

        private class ResponseChoice
        {
            public ResponseMessage Message { get; set; }
        }

        private class ResponseMessage
        {
            public string Content { get; set; }
            public List<ResponseToolCall> ToolCalls { get; set; }
        }

        private class ResponseToolCall
        {
            public string Id { get; set; }
            public string Type { get; } = "function";
            public ResponseFunction Function { get; set; }
        }

        private class ResponseFunction
        {
            public string Name { get; set; }
            public string Arguments { get; set; }
        }

        #endregion
    }
}