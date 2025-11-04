# Environment Variable Management

The DotnetAgents stack consumes several environment variables across services, tests, and CI workflows. This guide explains local setup, secrets hygiene, and tooling expectations.

## Core Variables

| Name | Purpose | Scope |
| ---- | ------- | ----- |
| `VERSION_PREFIX` | Semantic version prefix for environment deployments | Required in CI and local runs that build docs/coverage |
| `OPENAI_API_KEY` | API key for live-model smoke tests | Optional locally, required for scheduled CI smoke job |
| `OPENAI_MODEL_NAME` | OpenAI/OpenRouter model identifier | Optional locally, required for scheduled CI smoke job |
| `OPENAI_ENDPOINT` | Override endpoint for OpenAI-compatible client | Optional |

## Local Setup

1. Duplicate `.env.example` to `.env`.
2. Populate the necessary variables. Many developers only need `VERSION_PREFIX`; leave OpenAI values empty unless running the live smoke tests.
3. Ensure `.env` is **never committed**—it is ignored via `.gitignore`.
4. Run commands using `dotenvx` to load variables:
   ```bash
   dotenvx run -- dotnet test
   dotenvx run -- docfx build
   dotenvx run -- npm test --workspace DotnetAgents.Web/e2e
   ```

## CI Integration

- Secrets are sourced from repository-level GitHub Secrets and exported into `$GITHUB_ENV` for each job.
- Workflows validate `VERSION_PREFIX` before executing builds to catch misconfigurations early.
- Live-model tests run only in gated, scheduled workflows. Secrets are masked using `::add-mask::` to keep transcripts secure.

## Adding New Variables

1. Update `.env.example` with documentation and sensible defaults.
2. Document usage in this file and any relevant project README.
3. Ensure workflows fetch the secret via `${{ secrets.YOUR_VARIABLE }}` and mask output if the value may appear in logs.

Maintaining consistent documentation and examples ensures contributors can reproduce CI behavior locally without leaking sensitive information.
