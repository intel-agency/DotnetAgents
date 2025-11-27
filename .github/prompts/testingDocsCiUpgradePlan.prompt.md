---
description: Plan to enhance testing, documentation, and environment-specific CI deployments for DotnetAgents.
---

## Testing, Documentation & Environment CI Upgrade Plan

Strengthen chat-focused testing, enforce XML documentation, and wire DocFX plus Coverlet outputs into GitHub Actions that deploy to GitHub Pages for environment branches (`development`, `staging`, `production`). Each deployment uses the semantic version tag `<branch>-<VERSION_PREFIX>.<github.run_number>`, archives coverage history as JSON, and surfaces latest branch health badges—drawing `VERSION_PREFIX` from `.env`.

### Objectives
- Expand tests covering the agent chat workflow (web frontend, API, CLI) while improving overall coverage.
- Enforce XML documentation comments on all public APIs and publish HTML API docs via DocFX.
- Capture Cobertura + HTML + JSON coverage artifacts per environment branch and retain historical snapshots.
- Automate build/test/doc publishing through GitHub Actions triggered on environment-branch pushes using the repo’s semantic versioning scheme.
- Publish combined docs/coverage/history and latest status badges to GitHub Pages for easy inspection.

### Review Feedback

#### Highlights
- Goals align with repository needs: stronger chat-flow coverage, XML documentation enforcement, and environment-aware publishing.
- Deliverables capture the multi-format artifact output required for GitHub Pages consumers.

#### Must Address Before Execution
1. **Mock external dependencies:**
   - Record deterministic OpenAI transcripts (prompt, tool calls, assistant replies) into `tests/fixtures/openai/*.json` and load them through a `FixtureChatCompletionClient` injected via `IChatCompletionClient` abstraction.
   - Provide an opt-in `LiveChatCompletionClient` wired behind an `ILiveModelProbe` interface; run it only in a nightly/on-demand workflow guarded by an `if: github.event_name == 'schedule'` conditional and secrets retrieved with `${{ secrets.OPENAI_API_KEY }}`.
   - Update dependency injection in `IntelAgent/Program.cs` and test startup helpers to swap between fake and live clients via `IServiceCollection.AddSingleton<IChatCompletionClient>(...)`.
   - Mask captured content in logs (`***redacted***`) and ensure the replay fixtures exclude PII; document fixture refresh protocol in `docs/testing/openai-fixtures.md`.

2. **Define coverage governance:**
   - Add `coverlet.runsettings` at the repo root with multi-format output (`<DataCollectors>` block emitting Cobertura, HTML, JSON) and exclusions for generated Aspire assemblies (`DotnetAgents.AppHost.*`, `DotnetAgents.ServiceDefaults.*`).
   - Update every test project to invoke `dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"` via a shared `.config/dotnet/test.runsettings` import.
   - Phase 1 (runs 1-2): record baseline line/branch coverage into `coverage-baseline.json`; fail only on catastrophic regressions (coverage < 50%).
   - Phase 2 (run ≥3): enforce `baseline - 2%` thresholds and regression detection with `coverlet.console` JSON comparison script invoked inside CI (`scripts/coverage/verify-thresholds.ps1`).

3. **Persist coverage history safely:**
   - Implement `scripts/coverage/merge-history.ps1` to download `gh-pages:pages/<branch>/coverage-history.json`, merge with the current run's JSON using run_number ordering, and emit both the latest snapshot and a rolling 30-entry history.
   - Upload the merged JSON as a workflow artifact (`actions/upload-artifact`) prior to deploying Pages; set concurrency `group: coverage-${{ github.ref }}` with `cancel-in-progress: false` to serialize writes.
   - During Pages deploy, check out `gh-pages`, write to `pages/<branch>/<VERSION_PREFIX>.<github.run_number>/coverage/`, and push via `actions/deploy-pages`.

4. **Plan DocFX rollout:**
   - Sequence projects: `DotnetAgents.AgentApi` (Sprint 1) → `DotnetAgents.Web` (Sprint 2) → `IntelAgent` (Sprint 3) → `IntelAgents.Cli` (Sprint 4).
   - For each sprint, enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<NoWarn>$(NoWarn);CS1591</NoWarn>` in the project file, audit undocumented APIs using `dotnet build /warnaserror:CS1591` in a dedicated Doc debt pipeline, and create remediation tickets in `docs/docfx/undocumented-api-report.md`.
   - After each project's documentation debt reaches zero, remove the CS1591 suppression in that project and update DocFX config to include the project's XML output.

5. **Detail GitHub Pages workflow:**
   - Build/Test workflow (`ci-environments.yml`): triggered on pushes to `development|staging|production`, runs `dotnet build`, `dotnet test` with coverage, `docfx build`, archives `pages-bundle.tgz` containing `docs/_site`, coverage reports, and `coverage-history.json` (pre-merge) using `actions/upload-artifact` (retention: 14 days).
   - Deploy workflow (`deploy-pages.yml`): `workflow_run` on successful `ci-environments`, downloads artifact, runs merge script, stages outputs under `pages/<branch>/<VERSION_PREFIX>.<github.run_number>/`, generates badges, and deploys with `actions/upload-pages-artifact` + `actions/deploy-pages` (concurrency `pages-${{ github.ref }}`).
   - Document rollback procedure: re-run deploy with previous artifact via manual `workflow_dispatch` pointing to a selected `run_id`.

6. **Clarify VERSION_PREFIX sourcing:**
   - Commit `.env.example` with guidance (`VERSION_PREFIX=dev`, comments for per-branch overrides) and add developer docs describing `dotenvx run -- dotnet test` usage.
   - Workflows export `${{ secrets.VERSION_PREFIX }}` into `$GITHUB_ENV`; guard missing secret with a validation step that fails fast (`VERSION_PREFIX` must match `^[0-9]+\.[0-9]+$`).
   - Add `docs/operations/versioning.md` explaining semantic tag composition `<branch>-<VERSION_PREFIX>.<github.run_number>` and local override instructions for preview branches.

#### User Input
1. Create mocks, but include the live model test also. Secrets can be injected from GitHub Secrets.
2. Coverage thresholds can be added later, but we should have a plan to do so.
3. We can use GitHub Actions artifacts to store coverage history between runs.
4. Disable warning CS1591 for now, and enable it project by project.
5. Use the standard GitHub Pages actions, and store artifacts in the `gh-pages` branch. We can use the `concurrency` feature to avoid overwrites.
6. Add a `.env.example` file, and use GitHub Secrets to store the actual values.

#### Should Refine Soon
- Add concrete test blueprints (Aspire integration, UI automation framework, CLI assertions) to guide estimations and ownership.
- Decide on badge generation approach (Shields.io endpoints vs. CI-rendered SVG) and capture dependencies.
- Sketch GitHub Pages navigation and coverage trend visualization, and outline artifact retention limits.
- Reconcile new workflows with existing `.github/workflows` jobs to prevent redundant builds.

### Detailed Implementation Notes

#### Testing Blueprints & Tooling
- **Backend/API:** Expand `DotnetAgents.Tests` with Aspire-hosted integration suites using `WebApplicationFactory<Program>` and fake OpenAI client injection. Add load shedding tests ensuring rate-limit handling and error translations.
- **Web UI:** Adopt Playwright for end-to-end chat flows (form submission, streaming response rendering, error banner). Use `npm` workspace under `DotnetAgents.Web/e2e` with environment-aware base URLs, recording snapshots for regression.
- **CLI:** Leverage `CliWrap` or `DotNet.Testcontainers` to spawn CLI commands, validating prompts, retries, and structured output parsing.
- **Contract Tests:** Introduce shared `ChatScenario` fixtures consumed by API, Web, and CLI tests to guarantee consistent behavior across surfaces.
- **Nightly Live Test:** Single Playwright scenario hitting the live model via the opt-in client; secrets pulled from GitHub and masked via `::add-mask::`.

#### Coverage Governance Roadmap
- Store governance docs in `docs/testing/coverage-governance.md` outlining baseline capture, threshold math, and exemption request workflow.
- Automate baseline promotion via a manual `workflow_dispatch` job that copies the latest passing run into `coverage-baseline.json` once team agrees.
- Fail builds when coverage JSON regression script reports >2% drop, but allow `allow-failure` label for known flaky suites while triaged.

#### Coverage History Persistence & Visualization
- `coverage-history.json` schema: `{ "branch": "development", "runs": [{ "version": "development-2025.11.04.123", "line": 78.4, "branch": 73.1, "timestamp": "2025-11-04T18:22:00Z" }, ...] }`.
- Reducer script trims to last 90 days of data and produces derivative stats (last, best, rolling average) for dashboard rendering.
- Publish a D3.js chart in GitHub Pages landing page pulling from the JSON for trend visualization.

#### DocFX Rollout & XML Documentation Debt Tracking
- Create `docs/docfx/docfx.json` with `metadata` referencing each project's csproj as they onboard.
- Generate `docs/docfx/api/<project>` outputs and host in Pages. Add `docs/docfx/STATUS.md` with per-project completion percentages and owners.
- Track undocumented API counts using `dotnet format analyzers --severity error` gated in optional pipeline to avoid blocking main branch until each sprint completes.

#### GitHub Pages Deployment & Badge Generation
- Badge strategy: use Shields.io dynamic JSON badges served from the deployed `badge-status.json` per branch (generated during deploy via `scripts/badges/render-badges.ts`).
- Pages navigation: top-level index lists `development`, `staging`, `production` with latest version badge; each branch page includes API docs link, coverage HTML, JSON history chart, and latest status summary.
- Artifact pruning: weekly scheduled job cleans directories older than 90 days per branch while retaining latest 10 runs for auditors.

#### VERSION_PREFIX & Secrets Management
- Document local bootstrap in `docs/operations/environment-variables.md`, instructing developers to copy `.env.example` to `.env` and set `VERSION_PREFIX` (e.g., `2025.11`).
- Use `dotenvx run -- dotnet test` and `dotenvx run -- docfx build` in developer guidance to mirror CI semantics.
- Add pre-commit hook (optional) verifying `.env` is ignored and `.env.example` remains updated when new secrets introduced.

#### Workflow Harmonization
- Audit existing workflows (`build.yml`, `test.yml`) and consolidate overlapping steps into reusable composite actions under `.github/actions/dotnet-build-test`.
- Introduce matrix to reuse build outputs between CI and deploy using build artifacts keyed by commit SHA to avoid redundant compilation.
- Ensure concurrency groups between old and new workflows do not conflict by namespacing with `ci-${{ github.ref_name }}`.

#### Risks to Track
- Secret leakage or rate limits if tests continue hitting external APIs.
- Build failures from immediate CS1591 enforcement without remediation runway.
- Concurrent updates corrupting coverage history absent locking or serialization.
- Pages storage growth if versioned artifacts are never pruned.

#### Next Actions
- Update this plan with the required detail above before work begins.
- Review the refined plan with test, backend, frontend, and DevOps owners to confirm tooling choices and sequencing.

### Key Steps
1. **Audit Existing Tests & Docs**
   - Review `DotnetAgents/DotnetAgents.Tests` and `DotnetAgents/IntelAgent.Tests` to catalogue gaps, especially around chat prompt/response flows.
   - Identify public API surfaces lacking XML documentation across `DotnetAgents.AgentApi`, `DotnetAgents.Web`, `IntelAgent`, and CLI projects.

2. **Design and Expand Testing**
   - Specify new unit, integration, and UI tests covering:
     - Web chat UI requests → `AgentApiClient`.
     - API controllers and services handling agent prompts/responses.
     - CLI prompt pipelines and error handling.
   - Introduce injectable fakes/recordings for OpenAI and other external dependencies, keeping the hermetic suite fast while adding a nightly/on-demand live smoke test wired to GitHub Secrets.
   - Update test project csproj files to integrate Coverlet, emitting Cobertura, HTML, and JSON history (`coverage-history.json`) into `${{ github.ref_name }}/${VERSION_PREFIX}.${{ github.run_number }}/`, and capture two baseline runs before enforcing threshold regressions.

3. **Enforce XML Documentation & Generate API Docs**
   - Enable `GenerateDocumentationFile` project-by-project (AgentApi → Web → IntelAgent → CLI) while keeping CS1591 as a warning until each project’s public surface is documented.
   - Add XML summaries to all public types, methods, properties, and parameters, tracking remaining debt per project so CS1591 can graduate back to warning-as-error once complete.
   - Scaffold DocFX (e.g., `docs/docfx.json`, `docs/api/**`) and wire it to consume XML docs, plus expose coverage JSON and status badges.

4. **Configure CI Workflows & Versioning**
   - Commit `.env.example` with documented `VERSION_PREFIX` usage; require contributors to create a local `.env` while CI injects `${{ secrets.VERSION_PREFIX }}` via `$GITHUB_ENV`.
   - Build/Test workflow (branches `development`, `staging`, `production`) executes `dotnet build`, runs tests with coverage, uploads the combined docs/coverage bundle, and publishes `coverage-history.json` as an artifact.
   - Deploy workflow uses `actions/configure-pages`, `actions/upload-pages-artifact`, and `actions/deploy-pages` to publish into `gh-pages`, first downloading the prior branch history, merging JSON, and writing to `pages/<branch>/<VERSION_PREFIX>.<github.run_number>/`.
   - Concurrency group `pages-${{ github.ref }}` plus short-lived (14-day) artifact retention prevents overwrites while allowing retries, and the deploy job updates branch badges sourced from the merged coverage JSON.

5. **Documentation & Communication**
   - Update README and `docs/README.md` (or create new docs) with:
     - Testing strategy, how to run new suites, and expectations for coverage.
     - XML documentation requirements and how DocFX consumes them.
     - CI workflow triggers, versioning/tagging rules, and artifact locations.
   - Guidance on browsing GitHub Pages (per-environment version directories), interpreting the JSON coverage history, reading branch badges, and managing `.env`/secret configuration locally vs. CI.

### Deliverables
- Expanded test suites validating agent chat flows across backend, frontend, and CLI.
- XML-documented public APIs with DocFX-generated HTML documentation.
- GitHub Actions pipelines producing coverage (Cobertura + HTML + JSON) and site deployments restricted to environment branches.
- GitHub Pages site hosting versioned API docs, coverage reports, JSON history, and latest status badges per branch.
- Updated developer documentation outlining workflows, artifacts, and viewing instructions.
- Committed `.env.example` plus contributor guidance, with VERSION_PREFIX managed through GitHub Secrets and local `.env` parity.
- Coverage governance playbook capturing thresholds, merge scripts, and the two-phase enforcement timeline.

### Open Items
- **Coverage history format:** JSON snapshots per environment branch (confirmed).
- **Status badges:** Display latest run results only; explore Shields.io or custom SVG generation.
- **Landing page layout:** Determine navigation for `/development`, `/staging`, `/production` and integrate coverage trend visualization fed by JSON history.

### Todo Checklist
- [x] Catalogue current test gaps and undocumented public APIs for chat flow (owners: Backend, Frontend, QA).
- [x] Record OpenAI transcripts, implement fake/live client swap, and document replay fixtures.
- [x] Finalize coverlet.runsettings, baseline capture process, and regression script.
- [x] Script coverage-history reducer and integrate artifact retention + concurrency safeguards.
- [ ] Sequence DocFX rollout per sprint and publish documentation debt tracker.
- [x] Implement GitHub Actions build/test and deploy workflows with Pages integration and badge generation.
- [ ] Refresh README/docs and new ops guides for testing, DocFX, versioning, and secrets usage.
- [ ] Publish `.env.example`, environment variable guidance, and developer bootstrap instructions.
- [ ] Define badge rendering pipeline and GitHub Pages navigation layout.
- [ ] Audit overlapping workflows and extract reusable composite actions to avoid redundant builds.
