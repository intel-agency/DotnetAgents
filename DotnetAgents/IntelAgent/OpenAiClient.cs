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
        private readonly string? _baseUrl;
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

            _baseUrl = config["OpenRouter:BaseUrl"];
            _apiKey = config["OpenRouter:ApiKey"] ?? throw new InvalidOperationException("OpenRouter:ApiKey (OPENAI_API_KEY) not configured");
            _model = config["OpenRouter:Model"] ?? throw new InvalidOperationException("OpenRouter:Model (OPENAI_MODEL_NAME) not configured");
        }

        public async Task<LlmResponse> GetCompletionAsync(List<Message> history, List<string> toolSchemas)
        {
            var client = _httpClientFactory.CreateClient("OpenAiClient");
            
            // Ensure we have a base URL
            if (string.IsNullOrEmpty(_baseUrl))
            {
                _logger.LogError("OpenRouter BaseUrl is not configured");
                return new LlmResponse("Error: OpenRouter BaseUrl is not configured", null);
            }

            client.BaseAddress = new Uri(_baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60); // Set explicit 60 second timeout
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // OpenRouter requires these headers
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/nam20485/DotnetAgents");
            client.DefaultRequestHeaders.Add("X-Title", "DotnetAgents");

            var requestPayload = BuildRequestPayload(history, toolSchemas);

            _logger.LogInformation("Sending LLM request to {BaseUrl} with model {Model}", _baseUrl, _model);

            try
            {
                var response = await client.PostAsJsonAsync("chat/completions", requestPayload, _jsonOptions);

                _logger.LogInformation("Received LLM response with status code {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("LLM API call failed with status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                    return new LlmResponse($"Error: API call failed. {errorBody}", null);
                }

                // First, read the response as a string
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("LLM response body: {ResponseBody}", responseBody);

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
                        .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))
                        .ToList();
                }

                return new LlmResponse(responseContent, domainToolCalls);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "LLM API call timed out after 60 seconds");
                return new LlmResponse("Error: Request timed out", null);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "LLM API call was cancelled");
                return new LlmResponse("Error: Request was cancelled", null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during LLM API call");
                return new LlmResponse($"Error: HTTP request failed - {ex.Message}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LLM API call.");
                return new LlmResponse($"Error: {ex.Message}", null);
            }
        }

        private OpenAiRequest BuildRequestPayload(List<Message> history, List<string> toolSchemas)
        {
            // Detect provider from model name
            bool isAnthropic = _model.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                               _model.Contains("anthropic", StringComparison.OrdinalIgnoreCase);
            bool isGemini = _model.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
                            _model.Contains("google", StringComparison.OrdinalIgnoreCase);
            
            var request = new OpenAiRequest
            {
                Model = _model,
                Messages = history.Select(m =>
                {
                    // For tool results, format according to provider
                    if (m.Role == "tool" && !string.IsNullOrEmpty(m.ToolCallId))
                    {
                        if (isAnthropic)
                        {
                            // Anthropic/Claude format
                            return new RequestMessage(
                                "user", // Anthropic uses "user" role for tool results
                                JsonSerializer.Serialize(new
                                {
                                    type = "tool_result",
                                    tool_use_id = m.ToolCallId,
                                    content = m.Content
                                })
                            );
                        }
                        else if (isGemini)
                        {
                            // Google Gemini format
                            return new RequestMessage("function", m.Content);
                        }
                        else
                        {
                            // OpenAI/default format
                            return new RequestMessage("tool", m.Content);
                        }
                    }
                    return new RequestMessage(m.Role, m.Content);
                }).ToList()
            };

            if (toolSchemas != null && toolSchemas.Any())
            {
                request.Tools = toolSchemas.Select(schema =>
                    new RequestTool(JsonSerializer.Deserialize<JsonElement>(schema))
                ).ToList();
            }

            // Log the actual JSON being sent to help debug
            var jsonPayload = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogInformation("Request payload being sent: {JsonPayload}", jsonPayload);

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