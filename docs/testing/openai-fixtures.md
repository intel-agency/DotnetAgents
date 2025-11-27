# OpenAI Fixture Replay Guide

Hermetic tests rely on deterministic chat transcripts captured from prior OpenAI (or OpenRouter) sessions. This document describes how to create, store, and refresh those fixtures.

## Fixture Layout

Fixtures live under `tests/fixtures/openai/` and follow the naming pattern `<scenario>.json` (e.g., `basic_chat.json`). Each file stores a sequence of request/response exchanges:

```json
{
  "metadata": {
    "model": "openrouter/anthropic/claude-3.5-sonnet",
    "created": "2025-10-31T18:42:00Z",
    "tags": ["web", "happy-path"]
  },
  "transcript": [
    {
      "prompt": "Hello agent!",
      "toolCalls": [],
      "response": "Hello! How can I assist you today?"
    }
  ]
}
```

## Recording a Fixture

1. Trigger the [`Live Model Smoke Test`](../../.github/workflows/live-model-smoke.yml) workflow with `workflow_dispatch` (or allow the daily schedule to run).
2. The workflow executes the `IntelAgent.LiveProbe` console, which uses the `ILiveModelProbe` interface to call the upstream model with redacted logging (`***redacted***`).
3. For fixture refreshes, run `dotnet run --project DotnetAgents/DotnetAgents/IntelAgent.LiveProbe` locally with the required secrets exported, capture the JSON payload, review for PII, and copy the sanitized transcript into `tests/fixtures/openai/`.
4. Update this document with the fixture name, the run URL, and the reason for the refresh.

## Using Fixtures in Tests

- `FixtureChatCompletionClient` loads the JSON and replays responses deterministically.
- Tests inject the fixture client via dependency injection, ensuring they never call external APIs.

## Refresh Protocol

- Refresh fixtures quarterly or when APIs change significantly.
- Create a PR documenting why fixtures changed and link to the live workflow run.
- Redact sensitive literals in both prompts and responses before committing (replace with `***redacted***`).
- Remove superseded fixtures to minimize repository bloat.

Keeping fixtures up to date balances reliable tests with the ability to detect regressions when the external model evolves.
