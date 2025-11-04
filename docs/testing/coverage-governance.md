# Coverage Governance

This document defines how DotnetAgents manages code coverage baselines, thresholds, and exemptions.

## Data Collection

- All test projects run with a shared `coverlet.runsettings` file.
- Coverage is produced in Cobertura (`coverage.cobertura.xml`), HTML (`coverage-report/index.html`), and JSON (`coverage.json`) formats.
- Generated Aspire assemblies (`DotnetAgents.AppHost.*`, `DotnetAgents.ServiceDefaults.*`) are excluded.

## Baseline Process

1. First two successful environment runs capture baseline metrics and write them to `coverage-baseline.json`.
2. The baseline file is stored in the repository to enable deterministic gating.
3. After team approval, trigger the `promote-coverage-baseline` workflow to update the baseline from the latest passing run.

## Thresholds

- Phase 1: Only catastrophic regressions (<50% line coverage overall) fail the build.
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

## Exemptions

- Submit exemption requests via issue template `coverage-regression-exemption.md` (TBD).
- Include assembly, reason, mitigation plan, and expiration date.
- Exemptions automatically expire after two sprints unless renewed.

By following this governance model, we incrementally raise coverage expectations without blocking early adoption of the new pipeline.
