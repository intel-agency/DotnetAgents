# Environment Versioning & Tags

This repository publishes documentation, coverage, and health artifacts for three long-lived environment branches: `development`, `staging`, and `production`. Each deployment is versioned using the semantic tag pattern:

```
<branch>-<VERSION_PREFIX>.<github.run_number>
```

- `<branch>` resolves to the branch name (`development`, `staging`, or `production`).
- `<VERSION_PREFIX>` is a numeric `Major.Minor` string defined in `.env` (e.g., `2025.11`). In CI, it is supplied via the `VERSION_PREFIX` GitHub secret; locally, developers can set it in `.env` or export it in the shell.
- `<github.run_number>` is automatically provided by GitHub Actions and increments per workflow run.

## Local Development Workflow

1. Copy `.env.example` to `.env` and set `VERSION_PREFIX` to the desired release train (e.g., `2025.11`).
2. Run commands with `dotenvx` to ensure the environment variable is available:
   ```bash
   dotenvx run -- dotnet test
   dotenvx run -- docfx build
   ```
3. Override `VERSION_PREFIX` temporarily for preview branches by exporting the variable or using `dotenvx run --env preview.env`.

## CI Requirements

- The `VERSION_PREFIX` GitHub secret **must** exist for all environment branches. The workflows fail fast if the secret is missing or does not match the `^[0-9]+\.[0-9]+$` pattern.
- Nightly or manual preview deployments may override the secret by passing an explicit input to `workflow_dispatch`.

## Tag Consumption

GitHub Pages publishes documentation and coverage assets to `pages/<branch>/<VERSION_PREFIX>.<github.run_number>/`. Status badges and coverage history consumers read the latest entry to display branch health.

## Updating the Prefix

Increment `VERSION_PREFIX` whenever a new release train starts (e.g., beginning of a month or major feature wave). Update both the GitHub secret and local `.env` files used by contributors. The previous artifacts remain available under their historic version tags.
