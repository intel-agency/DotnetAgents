# LLM Response Routing – Multi-Phase Implementation Plan

## Execution Strategy & Task Tracker (2025-11-20)

1. **Re-baseline Phases 2–4 before any new UI work.** Create fresh branches, re-implement TaskHub/SignalR wiring, agent+worker field persistence/broadcasts, and the tasks/stats APIs per `docs/Phase1-4_Audit.md`.
2. **Resume downstream phases only after each prerequisite merges.** Rebase Phase 5+ work atop the fixed foundation and enforce console parity for every PR.
3. **Enforce disciplined workflow.** One branch per phase, consistent PR template, automated + manual testing via Aspire run, and documented evidence/screenshots.
4. **Execution reminder:** keep working through tasks continuously without asking for permission after each step. Only pause if there is a critical blocker or ambiguous requirement; otherwise, implement tasks sequentially until the branch + PR are complete.

### Task Tracker (fine-grained)

#### Phase 2 – SignalR Infrastructure (branch: `feature/phase-2-signalr-api`)
- [x] Add `DotnetAgents.AgentApi/Hubs/TaskHub.cs` with subscribe/unsubscribe/group logging.
- [x] Create `Interfaces/ITaskNotificationService.cs` and implementation in `Services/TaskNotificationService.cs`.
- [x] Register `AddSignalR()`, notification service, and `app.MapHub<TaskHub>("/taskHub")` in `Program.cs`.
- [x] Unit/integration test hub subscription + notification service serialization (`dotnet test DotnetAgents.Tests/DotnetAgents.Tests.csproj`).
- [x] Update docs (`Phase2_*` + audit) with evidence of implemented SignalR infrastructure.

#### Phase 3 – Agent & Worker Updates (branch: `feature/phase-3-agent-worker`)
- [ ] Update `IntelAgent.Agent` to set `StartedAt`, increment `CurrentIteration`, and populate `Result`/`ErrorMessage`.
- [ ] Extend `AgentWorkerService` to track `LastUpdatedAt`, `UpdateCount`, `CompletedAt`, and inject `ITaskNotificationService`.
- [ ] Emit notifications for queued→running, progress loops, completion/failure.
- [ ] Add automated tests (unit or integration) for worker updates + notification calls.
- [ ] Document changes and update audit status once verified.

#### Phase 4 – API Endpoints (branch: `feature/phase-4-api-endpoints`)
- [ ] Implement `GET /api/tasks` with pagination/filter DTOs; include new fields.
- [ ] Implement `GET /api/tasks/stats` aggregating counts, averages, success/failure rate.
- [ ] Enhance `GET /api/tasks/{id}` response with timestamps, progress, database metrics.
- [ ] Add minimal unit tests for handlers + update OpenAPI descriptions.
- [ ] Record manual verification (Swagger screenshots, curl output).

#### Downstream Phases (blocked until 2–4 merge)
- **Phase 5 (`feature/phase-5-web-signalr-client`)**
  - [ ] Port SignalR DTOs to shared project and implement Blazor + console hub services.
  - [ ] Register/start hub clients, add resilience logging, verify via Aspire logs.
- **Phase 6 (`feature/phase-6-web-tasks-dashboard`)**
  - [ ] Build `/tasks` page components + console dashboard synchronized via SignalR/API.
- **Phase 7 (`feature/phase-7-chat-ui`)**
  - [ ] Regression-test web chat, add console chat parity, ensure accessibility.
- **Phase 8 (`feature/phase-8-db-insights`)**
  - [ ] Surface DB metrics in both UIs, validate instrumentation overhead.

*Update checkboxes as each subtask completes; cite `docs/Phase1-4_Audit.md` when closing Phases 2–4 gaps.*

## Acceptance & Success Criteria

### Phase 2 Re-baseline
- [ ] `TaskHub` endpoints reachable at `/taskHub` (verified via Swagger/SignalR client logs).
- [ ] `ITaskNotificationService` methods invoked from a sample worker run (log evidence).
- [ ] Automated test proving subscribe/unsubscribe behavior.

### Phase 3 Re-baseline
- [ ] `AgentTask` rows show `StartedAt`, `CompletedAt`, `CurrentIteration`, `Result/ErrorMessage` after a run.
- [ ] Worker logs show notification broadcasts for start, progress, completion, failure.
- [ ] Regression test (unit/integration) updated to cover new persistence logic.

### Phase 4 Re-baseline
- [ ] `GET /api/tasks` returns paginated payload with new fields.
- [ ] `GET /api/tasks/stats` returns aggregate metrics matching seeded data.
- [ ] `GET /api/tasks/{id}` exposes timestamps/progress/metrics; documented via curl output.

### Downstream Phases (Success Criteria)
- **Phase 5:** both Blazor and console apps maintain active SignalR connections, reflect live task updates without polling, and share common DTOs.
- **Phase 6:** `/tasks` (web) and console dashboards show synchronized task lists, detail panes, and database metrics refreshed in real time.
- **Phase 7:** chat workflows in both UIs display live progress/results with accessibility confirmations (keyboard + screen reader checks).
- **Phase 8:** database insight widgets show metrics that match telemetry/logs, and load tests confirm negligible overhead.

Completion of each phase requires all relevant checkboxes above plus merged PR with evidence (screenshots/log snippets/test output) referenced in the description.

> **Scope** – Ensure LLM-generated responses and tool observations flow end-to-end from the backend pipeline to both user interfaces (Blazor web app + Consolonia console UI). Work is organized into the eight documented phases, with Git branches and PRs created per phase. No code changes are performed yet; this document is the execution guide.

---

## 1. Current Phase Status & Readiness Snapshot

| Phase | Description                                  | Status (2025-11-19)                                          | Branch to Use                                                 | Key Dependencies |
| ----- | -------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------- | ---------------- |
| 1     | Database & model updates                     | ✅ Complete (verify migration applied in all environments)    | `feature/phase-1-db-models` *(historical)*                    | None             |
| 2     | SignalR infrastructure (hub + notifications) | ✅ Complete                                                   | `feature/phase-2-signalr-api` (merged)                        | Phase 1          |
| 3     | Agent + worker updates (broadcasting)        | 🟡 Ready to start                                             | `feature/phase-3-agent-worker`                                | Phases 1-2       |
| 4     | Expanded API endpoints                       | ❌ Incomplete – list/stats endpoints missing from API         | `feature/phase-4-api-endpoints` *(requires reimplementation)* | Phases 1-3       |
| 5     | Web SignalR client service                   | 🟡 In progress (needs verification + console parity planning) | `feature/phase-5-web-signalr-client`                          | Phases 1-4       |
| 6     | Tasks monitoring page (web)                  | ⏳ Not started                                                | `feature/phase-6-web-tasks-dashboard`                         | Phases 1-5, 4    |
| 7     | Chat UI upgrades                             | ✅ Complete *(per docs, re-validate after Phase 5)*           | `feature/phase-7-chat-ui`                                     | Phases 1-5       |
| 8     | Database insights & instrumentation          | ✅ Complete *(per docs; confirm rollout in target envs)*      | `feature/phase-8-db-insights`                                 | Phases 1-4       |

> **Console UI parity:** not covered by historical phases. Each future branch must include a “Console parity” checklist item so both frontends stay in sync once routing infrastructure is live.
>
> **Audit reference:** See `docs/Phase1-4_Audit.md` for detailed evidence behind the Phase 2–4 status regression.

---

## 2. Branch & PR Workflow

1. **One branch per phase**, based off latest `lone-agent` (or mainline) once prior phase PR merges.
2. **Naming convention:** `feature/phase-{n}-{short-name}` (already used historically; keep consistent for discoverability).
3. **PR template:**
   - Summary of scope + linked walkthrough doc + screenshots/gifs for UI phases.
   - Checklist covering backend, web, console, tests, docs, and manual verification.
4. **Task breakdown per phase:** create GitHub issues or Azure DevOps work items referencing this plan. Each issue should be estimable (< 1 day) and traceable to a PR checklist item.
5. **Testing expectations:**
   - Automated: unit tests for services, Hub client wrappers, and console presenters.
   - Manual: run `dotnet run` from `DotnetAgents.AppHost`, validate both UIs, capture logs + screenshots.
6. **Roll-forward strategy:** if a phase uncovers gaps in earlier phases (e.g., missing SignalR event), open a follow-up bug branch rather than modifying already-approved branches.

---

## 3. Cross-Phase Dependencies & Safeguards

- **Data contract alignment:** `AgentTask` payloads must remain backward compatible. Schema updates require migrations + console deserialization updates before deployment.
- **SignalR event catalog:** Centralize event names/payloads in a shared DTO project (`DotnetAgents.Core`). Any change requires simultaneous updates to API hub, web TaskHubService, and console subscription layer.
- **Configuration management:** Aspire provides service discovery. Document overrides (`services:agentapi:https:0`) for console + web so both point to identical hubs.
- **Observability:** Phase 8 metrics should be re-validated whenever new UI listeners are added to avoid regressing DB insights.

---

## 4. Phase Execution Details

### Phase 2 – SignalR Infrastructure (Staged for PR)
- **Goal:** Restore hub, notification service, and hosting configuration so downstream phases can consume real-time events.
- **Branch:** `feature/phase-2-signalr-api` (local changes staged; commit + push still pending).
- **Progress:**
  1. `TaskHub`, `ITaskNotificationService`, and `TaskNotificationService` reintroduced with subscription logging, group management, and strongly typed payloads.
  2. `Program.cs` registers SignalR services and maps `/taskHub`; AppHost updated so both API + web hosts expose the hub endpoint.
  3. Regression tests for hub membership + notification serialization are green; docs/audit entries refreshed with evidence.
- **Next Steps before merge:** finalize commit, push branch, open PR with screenshots/logs proving hub connectivity, and capture test output in the description.

### Phase 3 – Agent & Worker Updates (Execution Plan)
- **Goal:** Ensure agent loop and worker service populate all task-tracking fields and broadcast lifecycle updates through SignalR.
- **Branch:** `feature/phase-3-agent-worker` (create after Phase 2 PR merges).
- **Tasks:**
  1. **Agent instrumentation:** update `IntelAgent.Agent` to set `StartedAt`, increment `CurrentIteration`, and persist `Result`/`ErrorMessage` plus partial progress after each loop.
  2. **Worker lifecycle tracking:** extend `AgentWorkerService` to manage `LastUpdatedAt`, `UpdateCount`, `CompletedAt`, and inject `ITaskNotificationService` for queued→running, iteration progress, success, and failure notifications.
  3. **State persistence:** ensure Redis history saves align with new DB updates; add guardrails for cancellation + exception paths.
  4. **Testing:** cover agent iteration updates and worker notification calls with unit tests (mocking notification service) plus an integration test that exercises a full task execution.
  5. **Documentation & audit:** update `docs/Phase1-4_Audit.md` and this plan once verification evidence (logs, test output) is captured.
- **Deliverables:** updated services, notification hooks, automated tests, and refreshed docs showing database rows populated with new fields.

### Phase 4 – API Endpoints & Stats (Execution Plan)
- **Goal:** Provide paginated task listings, aggregate stats, and enhanced detail responses for both SignalR clients + dashboards.
- **Branch:** `feature/phase-4-api-endpoints` (start after Phase 3 merges).
- **Tasks:**
  1. **DTOs & queries:** add pagination/filter DTOs plus projection models that include timestamps, progress metrics, and notification metadata.
  2. **Endpoints:** implement `GET /api/tasks`, `GET /api/tasks/stats`, and enrich `GET /api/tasks/{id}` responses with newly tracked fields and database metrics.
  3. **OpenAPI & validation:** document request/response schemas, add parameter validation, and ensure Swagger reflects the new endpoints.
  4. **Testing:** create unit tests for handler logic and, if feasible, an integration test hitting the new endpoints against seeded data.
  5. **Manual verification:** capture Swagger screenshots and curl outputs demonstrating pagination, stats accuracy, and detail payloads.
- **Dependencies:** relies on Phase 3 fields/notifications; coordinate DTO updates with upcoming Phase 5 clients.

### Phase 5 – Web SignalR Client (Complete + Console parity)
- **Goal:** Ensure the Blazor app maintains a resilient SignalR connection and exposes events to components; replicate the same event subscription layer in the console UI.
- **Branch:** `feature/phase-5-web-signalr-client` (extend if open; otherwise recreate and cherry-pick as needed).
- **Tasks:**
  1. **Web:** finalize `ITaskHubService` + `TaskHubService`, register in `Program.cs`, and confirm lifecycle (start on host start, stop on shutdown).
  2. **Console:** introduce equivalent `ConsoleTaskHubService` leveraging the same abstractions. Hook into Avalonia/Consolonia lifecycle so console chat surface can receive push updates.
  3. **Shared DTOs:** move `TaskStatusUpdate`/`TaskProgressUpdate` records into `DotnetAgents.Core` to avoid duplication.
  4. **Config:** document hub URL resolution for both hosts; ensure dev/prod parity.
  5. **Testing:** run AppHost, verify both UIs connect, inspect Aspire logs for successful hub connections.
- **Deliverables:** updated services, shared DTOs, documentation snippet in `docs/Phase5*.md`, console parity notes, test evidence.

### Phase 6 – Tasks Monitoring Page (Web + Console dashboards)
- **Goal:** Provide operators with a real-time dashboard of all tasks, accessible via web `/tasks` and console equivalent view.
- **Branch:** `feature/phase-6-web-tasks-dashboard`.
- **Tasks:**
  1. **API Consumption:** leverage Phase 4 endpoints (`GET /api/tasks`, `/stats`). Consider adding lightweight query wrappers in both clients.
  2. **Web UI:** build components `TaskCard`, `TaskTimeline`, `DatabaseMetricsPanel` per implementation plan. Integrate SignalR updates for active tasks list.
  3. **Console UI:** design text-based dashboard (e.g., list view + detail pane) that refreshes via SignalR + periodic API fallback.
  4. **State management:** ensure both UIs share pagination/filter logic through a shared service in `DotnetAgents.Core` or a new client abstractions project.
  5. **Testing:** manual walkthrough capturing screenshots; console recording (gif or ascii capture) to show updates.
- **Dependencies:** Requires Phase 5 hub client to be rock solid; relies on Phase 4 endpoints.

### Phase 7 – Chat UI Enhancements (Regression validation + Console UI gap fill)
- **Goal:** Confirm Blazor chat improvements still work after Phase 5/6 changes and deliver equivalent experience in the console chat window.
- **Branch:** `feature/phase-7-chat-ui` (new verification branch even though docs mark complete).
- **Tasks:**
  1. **Web:** regression-test `AgentChat.razor`, ensure progress bar, status badges, and final responses appear with the latest TaskHubService contract.
  2. **Console:** implement chat workflow with live updates (status/progress/result). Respect console UX constraints (color coding, minimal flicker).
  3. **Accessibility:** confirm keyboard-only navigation and screen reader support for web progress indicators.
  4. **Testing:** cross-browser manual tests + console run-through with multiple concurrent tasks.
- **Exit criteria:** both UIs show real-time task lifecycle with no manual refresh required.

### Phase 8 – Database Insights & Instrumentation (Validation + UI consumption)
- **Goal:** Surface database insight metrics (update counts, latency) in both monitoring surfaces and ensure instrumentation overhead is acceptable.
- **Branch:** `feature/phase-8-db-insights`.
- **Tasks:**
  1. **Backend:** review interceptor configuration, confirm metrics exported via API/stat endpoints consumed by Phase 6 components.
  2. **Web:** wire DatabaseInsights widgets into Tasks page (or dedicated insights tab) with spark lines / charts.
  3. **Console:** add summary block (e.g., “DB Ops / sec”, “Avg latency”).
  4. **Performance testing:** run load test or replay to ensure instrumentation does not degrade worker throughput.
- **Exit criteria:** insights data visible in both UIs and cross-checked against logs/OTel traces.

---

## 5. Console Frontend Alignment Plan

Because the original walkthroughs only reference the Blazor UI, each upcoming phase must explicitly:

1. **Mirror abstractions** – create shared services/interfaces where possible so console + web consume identical hub & API clients.
2. **Define UX parity** – for every new UI widget (status chips, progress bar, insights chart), design a console-friendly analogue (e.g., ASCII bars, color-coded text).
3. **Testing cadence** – add a console checklist to every PR template: open console app, submit prompt, confirm streaming updates, capture transcript.
4. **Documentation** – extend each phase doc with a “Console Implementation Notes” subsection summarizing shortcuts or deviations.

---

## 6. Risk Register & Mitigations

| Risk                                                                    | Phase(s) | Impact | Mitigation                                                                                              |
| ----------------------------------------------------------------------- | -------- | ------ | ------------------------------------------------------------------------------------------------------- |
| Hub contract drift between API, web, console                            | 5-7      | High   | Centralize DTOs, add integration test hitting `/taskHub` with TestServer + client library.              |
| Console UI blocking Aspire startup (due to SignalR connection failures) | 5-7      | Medium | Lazy-start console hub connection and retry with exponential backoff mirroring web logic.               |
| Database insight overhead                                               | 8        | Medium | Feature flag instrumentation; monitor `UpdateCount` deltas before/after enabling.                       |
| Branch overlap / long-lived PRs                                         | All      | Medium | Enforce sequential merges (Phase N must merge before Phase N+1 starts) and keep branches rebased daily. |

---

## 7. Acceptance Criteria Summary

- **Web Chat (Phase 7)** – real-time progress, final response message from LLM, no manual refresh.
- **Console Chat** – identical functional behavior (text-based progress + result output).
- **Tasks Dashboard (Phase 6)** – live list of tasks with filtering + detail pane available in both UIs.
- **Database Insights (Phase 8)** – surfaced metrics match backend telemetry and refresh automatically.
- **Documentation** – each phase walkthrough updated with screenshots, console notes, and test evidence links.

---

## 8. Next Steps Checklist

1. ✅ Finalize this plan and circulate for sign-off.
2. 🔁 Audit current repo state to confirm Phase 2 artifacts match this plan (logs, tests, AppHost wiring). Attach findings to the upcoming PR.
3. 🟡 Commit + push `feature/phase-2-signalr-api`, open the PR, and capture test output/screenshots as evidence.
4. 🟩 Kick off **Phase 3 branch** once Phase 2 PR is raised, executing the agent/worker plan above.
5. ⏭️ Sequence the Phase 4 branch immediately after Phase 3 merges, then proceed to Phase 5+ work with console parity enforced throughout.

---

*Prepared: 2025-11-19 by Cascade. Updated as phases progress.*
