# Phase 5 SignalR Remediation Action Plan

_Last updated: 2025-11-22_

## 1. Context & Objectives
- **Scope**: Address review feedback and quality-gate failures for PR [#12](https://github.com/intel-agency/DotnetAgents/pull/12) implementing the shared SignalR TaskHub client.
- **Goals**:
  1. Restore a reliable console experience (accurate headers, graceful shutdown, working agent interactions).
  2. Harden SignalR client lifecycle management across web/console front ends.
  3. Eliminate duplication and thread-safety issues that triggered the SonarCloud gate.
  4. Provide portable documentation and concrete evidence (logs/tests) required by Issue [#11](https://github.com/intel-agency/DotnetAgents/issues/11).

## 2. Key Findings Snapshot
| Area                                          | Issue                                                                                                    | Impact                                                                                                         |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Console UI (`MainWindow.axaml.cs`)            | `Agent` never instantiated, header refactor incomplete, `_shutdownCts` undisposed, fire-and-forget tasks | UI stuck in "Agent not initialized", erroneous status banners, potential unobserved exceptions, resource leaks |
| SignalR client base (`TaskHubClientBase`)     | `_state` not thread-safe; derived clients duplicate lifecycle code; `DisposeAsync` calls `StopAsync`     | Race conditions during connection events; Sonar reported 7.9% duplication; redundant stop/dispose sequences    |
| Hosted service (`TaskHubClientHostedService`) | Disposes singleton client during `StopAsync`, no logging on stop failure                                 | Consumers can observe disposed instances, errors hidden                                                        |
| Resilience/logging                            | Fire-and-forget reconnect loops without observation/logging                                              | Silent failures if reconnect task dies                                                                         |
| Documentation                                 | Hard-coded absolute Windows paths; no Aspire/validation artefacts captured                               | Instructions unusable on other machines, Issue #11 acceptance criteria unmet                                   |

## 3. Remediation Workstreams

### Workstream A – Console UI lifecycle & UX (Priority P0)
- **Tasks**
  - Reinstate `Agent` initialization or gate features until ready.
  - Replace legacy `UpdateHeader(string)` calls by managing `_headerStatus`/`_connectionStatus` directly; update header once per state transition.
  - Move status transitions inside `try/catch` for `OnSend`; keep "Processing" active until completion.
  - Convert `_shutdownCts` to a linked token source, dispose in `OnClosed`, and await stop/cleanup to observe exceptions.
  - Track the `MaintainSignalRConnectionAsync` task and log failures via `ILogger`.
- **Deliverables**: Updated `MainWindow.axaml.cs`, new unit test (if feasible) verifying header helper, manual console smoke test logs.

### Workstream B – SignalR client lifecycle safety & duplication removal (Priority P0)
- **Tasks**
  - Introduce a shared `SignalRHubClientBase` (name TBD) encapsulating semaphore, handler registration, and reconnection wiring for both console and web implementations.
  - Update `TaskHubClientBase` to make `_state` volatile or use `Interlocked.Exchange`; guard `ConnectionState` reads appropriately.
  - Adjust `DisposeAsync` implementations to only release unmanaged resources (connection + semaphores) and avoid redundant stop calls—document the contract that callers must invoke `StopAsync`.
  - Address Sonar warning on `TaskHubEndpointResolver.DefaultBaseUrl` by documenting the default and/or making it configurable via options.
- **Deliverables**: Refactored core/shared classes, updated console/web clients, expanded `TaskHubClientBaseTests` covering concurrency and endpoint resolution precedence.

### Workstream C – Hosting integration & resilience (Priority P1)
- **Tasks**
  - Remove `DisposeAsync` call from `TaskHubClientHostedService.StopAsync`; let DI dispose the singleton.
  - Add `try/catch` with logging around stop failures.
  - Audit all fire-and-forget tasks (console reconnect loop, hosted service start) and ensure exceptions are logged.
- **Deliverables**: Updated hosted service + Program wiring, test(s) verifying start/stop invocation order, clean shutdown logs.

### Workstream D – Documentation & evidence (Priority P1)
- **Tasks**
  - Replace absolute Windows paths in `docs/Phase5_Web_SignalR_Client_Walkthrough.md` with repo-relative commands (e.g., `cd DotnetAgents.Console`).
  - Add a "Verification Artefacts" section linking Aspire logs/screenshots stored under `docs/verification/phase5/`.
  - Summarize manual validation steps (console + web) and link to captured logs.
- **Deliverables**: Updated walkthrough, new log assets (e.g., `phase5-signalr-console.log`, `phase5-signalr-web.log`).

### Workstream E – Testing & validation coverage (Priority P2)
- **Tasks**
  - Extend `TaskHubClientBaseTests` to cover thread-safety cases and environment precedence.
  - Add hosted-service unit test (can use `FakeTaskHubClient`).
  - Document manual Aspire run with timestamps, ensuring both clients connect successfully.
- **Deliverables**: New/updated tests, CI evidence (`dotnet build`, `dotnet test`), manual run logs.

## 4. Acceptance Criteria
1. **Console header and UX fixes** (addresses PR comments on redundant `UpdateHeader` calls):
  - `_headerStatus` / `_connectionStatus` updates occur exactly once per state transition, “Processing…” remains visible until the agent call finishes, and error banners appear only on actual failures.
  - `_shutdownCts` is disposed, shutdown awaits `StopAsync`/`DisposeAsync`, and reconnect loops are logged (no fire-and-forget tasks without observation).
2. **SignalR client lifecycle parity**:
  - Shared lifecycle helper removes duplicated semaphore/handler code across web + console clients, cutting Sonar duplication below 5%.
  - `TaskHubClientBase` uses thread-safe state management; derived clients no longer call `StopAsync` inside `DisposeAsync`.
3. **Hosted service resilience**:
  - `TaskHubClientHostedService.StopAsync` logs exceptions and does not dispose the singleton client, preventing ObjectDisposedExceptions noted in PR feedback.
4. **Documentation + evidence**:
  - `docs/Phase5_Web_SignalR_Client_Walkthrough.md` and related guides use repo-relative commands and reference captured Aspire logs stored under `docs/verification/phase5/`.
5. **Validation artifacts**:
  - `dotnet build` + `dotnet test` succeed, manual Aspire run logs are attached, and SonarCloud shows a passing quality gate.
6. **Issue linkage**:
  - Issue [[TASK] Phase 5 SignalR clients #11](https://github.com/intel-agency/DotnetAgents/issues/11) checklist updated with links to this plan, validation evidence, and notes referencing all resolved PR comments.

## 5. Validation Strategy
1. `dotnet build DotnetAgents/DotnetAgents.slnx` (ensures entire solution compiles post-refactor).
2. `dotnet test DotnetAgents/DotnetAgents.Tests/DotnetAgents.Tests.csproj` (new tests included).
3. Re-run SonarCloud analysis (target duplication < 5%, no new blockers).
4. Manual Aspire session:
   - `dotnet run DotnetAgents/DotnetAgents.AppHost`
   - Launch console (`cd DotnetAgents.Console && dotnet run`) and capture header behavior/logs.
   - Launch web UI; verify SignalR lifecycle logs.
   - Archive relevant logs/screenshots under `docs/verification/phase5/` and reference them in the walkthrough + issue #11.
5. Update Issue #11 checklist with links to artefacts and describe the remediation steps.

## 6. Execution Timeline & Owners
| Sequence | Owner       | Deliverable                                             | ETA      |
| -------- | ----------- | ------------------------------------------------------- | -------- |
| A1       | Core dev    | Console header + lifecycle fixes, logging improvements  | Day 1    |
| B1       | Core dev    | Shared SignalR lifecycle refactor + thread-safety fixes | Days 1-2 |
| C1       | Backend dev | Hosted service stop/logging updates                     | Day 2    |
| D1       | Docs owner  | Walkthrough + evidence updates                          | Day 3    |
| E1       | QA/Dev      | Expanded automated tests + manual Aspire logs           | Day 3    |
| Final    | Dev lead    | Sonar rerun + issue #11 update + PR refresh             | Day 4    |

## 7. Dependencies & Risks
- **Aspire environment** must be available to capture validation logs; schedule time on shared infrastructure.
- **Refactor overlap**: Shared lifecycle changes affect both console and web clients—coordinate merges to avoid conflicts.
- **Thread-safety adjustments** may surface latent bugs; ensure thorough regression testing before merging.

## 8. Communication Plan
- Share this action plan in PR #12 and issue #11 for alignment.
- Post daily progress updates (mirroring Issue #11 comment thread) listing completed workstreams/checks.
- Once remediation completes, request another round of automated + human review before merging.
