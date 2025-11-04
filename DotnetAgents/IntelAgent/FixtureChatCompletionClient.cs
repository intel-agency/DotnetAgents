using Microsoft.Extensions.AI;
using System.Text.Json;

namespace IntelAgent;

/// <summary>
/// Deterministic chat client that replays responses from a JSON transcript.
/// Intended for tests and preview environments.
/// </summary>
public sealed class FixtureChatCompletionClient : IChatCompletionClient
{
    private readonly Queue<FixtureTurn> _turns;

    private FixtureChatCompletionClient(IEnumerable<FixtureTurn> turns)
    {
        _turns = new Queue<FixtureTurn>(turns);
    }

    public static FixtureChatCompletionClient FromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Fixture file not found", path);
        }

        using var stream = File.OpenRead(path);
        var transcript = JsonSerializer.Deserialize<FixtureTranscript>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize fixture transcript.");

        return new FixtureChatCompletionClient(transcript.Transcript);
    }

    public Task<string> GetResponseAsync(string prompt, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_turns.Count == 0)
        {
            throw new InvalidOperationException("No more responses available in fixture.");
        }

        var expected = _turns.Dequeue();

        if (!string.Equals(Normalize(expected.Prompt), Normalize(prompt), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Prompt mismatch. Expected '{expected.Prompt}' but received '{prompt}'.");
        }

        return Task.FromResult(expected.Response);
    }

    private static string Normalize(string value) => value.Trim();

    private sealed record FixtureTranscript(IReadOnlyList<FixtureTurn> Transcript);

    private sealed record FixtureTurn(string Prompt, string Response);
}
