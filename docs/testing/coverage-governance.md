# Coverage Governance

This document defines how DotnetAgents manages code coverage baselines, thresholds, and exemptions.

## Data Collection

- All test projects run with a shared `coverlet.runsettings` file.
- Coverage is produced in Cobertura (`coverage.cobertura.xml`), HTML (`coverage-report/index.html`), and JSON (`coverage.json`) formats.
- Generated Aspire assemblies (`DotnetAgents.AppHost.*`, `DotnetAgents.ServiceDefaults.*`) are excluded.

## Baseline Process

1. First two successful environment runs capture baseline metrics and write them to `coverage-baseline.json` (including optional `minimumLine`/`minimumBranch` gates).
2. The baseline file is stored in the repository to enable deterministic gating.
3. After team approval, trigger the `promote-coverage-baseline` workflow to update the baseline from the latest passing run.

## Thresholds

- Phase 1: The minimum coverage gates remain disabled until `minimumLine`/`minimumBranch` are explicitly set (or baseline reaches ≥50%).
- Phase 2: Once the baseline is established, the regression script enforces `baseline - 2%` for line and branch coverage.
- Projects may opt into stricter thresholds by updating the runsettings file and regression script inputs.

## Regression Detection

The `scripts/coverage/verify-thresholds.ps1` script compares current coverage JSON with the promoted baseline. It:

- Computes differences per assembly and overall.
- Fails the build if coverage drops beyond the allowed delta.
- Supports a temporary `allow-failure` label to unblock merges while flakiness is investigated.

## History & Reporting

- Each environment branch maintains `coverage-history.json` in GitHub Pages.
- History is trimmed to the latest 90 days to control storage.
- Dashboards render trend charts from the JSON to visualize progress.

## Environment Workflows

- [`ci-environments.yml`](../../.github/workflows/ci-environments.yml) runs on pushes to `development`, `staging`, and `production`. It builds the solution, executes the hermetic test suite with coverage, generates DocFX output, merges coverage history, and uploads a `pages-bundle.tgz` artifact containing docs and coverage assets.
- [`deploy-pages.yml`](../../.github/workflows/deploy-pages.yml) reacts to successful environment runs via the `workflow_run` trigger. It unpacks the bundle, stages artifacts under `pages/<branch>/<VERSION_PREFIX>.<run_number>/`, emits `coverage-history.json` and `badge-status.json`, and deploys to GitHub Pages using `actions/configure-pages` plus `actions/deploy-pages`.
- Both workflows serialize per branch using concurrency groups (`coverage-${{ github.ref }}` and `pages-${{ github.event.workflow_run.head_branch }}` respectively) to prevent conflicting writes.

## Exemptions

- Submit exemption requests via issue template `coverage-regression-exemption.md` (TBD).
- Include assembly, reason, mitigation plan, and expiration date.
- Exemptions automatically expire after two sprints unless renewed.

By following this governance model, we incrementally raise coverage expectations without blocking early adoption of the new pipeline.
