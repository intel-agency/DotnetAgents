# Phase 4–8 Parallel Delegation Plan

## Context & Status
- **Phases 2–3**: Complete (SignalR hub + agent/worker instrumentation are merged per `LLM_Response_Routing_Implementation_Plan.md`).
- **Phase 4**: Actively in progress (`feature/phase-4-api-endpoints`); endpoints and OpenAPI contract still missing.
- **Phase 5**: Partially implemented in the web UI; console parity and resilience work remain.
- **Phase 6**: Not started — requires both the Phase 4 API surfaces and Phase 5 client infrastructure.
- **Phase 7**: Web walkthrough is complete, but console experience still depends on Phase 5 outputs.
- **Phase 8**: Backend instrumentation exists; insights are not yet surfaced consistently in the UI/console.

The goal of this document is to identify independent workstreams that can be delegated to specialized subagents so that Phase 4+ work progresses in parallel without blocking on a single contributor.

## Workstream Dependency Matrix
| ID   | Workstream                                                  | Depends On                                                     | Ready To Start                               | Suggested Lead                 | Key Outputs                                                     |
| ---- | ----------------------------------------------------------- | -------------------------------------------------------------- | -------------------------------------------- | ------------------------------ | --------------------------------------------------------------- |
| WS-A | Phase 4 API surfaces (list/stats/detail + OpenAPI)          | Phases 2–3 ✅                                                   | **Now**                                      | Backend developer              | Endpoints, DTOs, EF queries, unit tests                         |
| WS-B | API contract packaging & verification                       | WS-A (code complete candidate)                                 | When WS-A exposes DTOs                       | QA/Tech writer                 | Published OpenAPI file, Swagger evidence, curl scripts          |
| WS-C | Shared DTOs + SignalR client infrastructure (web + console) | WS-A (schema), partial dependency on WS-B                      | As soon as DTOs are frozen                   | Frontend/Platform engineer     | Shared models, `TaskHubService` hardening, console hub skeleton |
| WS-D | Console parity sweep (Chat + future dashboard)              | WS-C                                                           | After hub client prototype                   | Console UI engineer            | Console status/progress UX, parity checklist                    |
| WS-E | Phase 6 Tasks monitoring UX (web + console)                 | WS-A + WS-C (data + transport)                                 | Web scaffolding can start now with mock data | Frontend + Console pair        | `/tasks` page, reusable components, console dashboard           |
| WS-F | Database insights surfacing & performance validation        | WS-A (stats data) + WS-E (UI hooks)                            | After first dashboard iteration              | Backend/Observability engineer | DB metrics widgets, load-test report                            |
| WS-G | Phase 6 walkthrough authoring                               | Implementation plan + `Complete_Implementation_Walkthrough.md` | **Now**                                      | Documentation specialist       | `docs/Phase6_Tasks_Monitoring_Page_Walkthrough.md` (new)        |

## Workstream Details

### WS-A – Phase 4 API Surfaces
- **Scope**: Implement `GET /api/tasks`, `GET /api/tasks/stats`, enrich `GET /api/tasks/{id}`, push pagination/filter DTOs into `DotnetAgents.Core`, and add unit coverage for query projections.
- **Entry Criteria**: Phases 2–3 merged (✅); database schema already contains fields introduced earlier.
- **Exit Criteria**:
  - Endpoints return the fields enumerated in `docs/Phase4_API_Endpoints_Walkthrough.md`.
  - Tests in `DotnetAgents.Tests` cover pagination, stats math, and detail calculations.
  - Manual evidence (Swagger screenshots + curl output) archived.
- **Parallelization Notes**: Once DTOs & response shapes stabilize, WS-B and WS-C can begin even before WS-A merges, provided a release branch is published for them to target.
- **Key References**: `docs/Phase4_API_Endpoints_Walkthrough.md`, `DotnetAgents.AgentApi/Program.cs`, `LLM_Response_Routing_Implementation_Plan.md`.

### WS-B – API Contract Packaging & Verification
- **Scope**: Extract the OpenAPI spec, document query parameters, add examples, and script regression checks (e.g., Postman/newman or `dotnet` http files).
- **Entry Criteria**: WS-A exposes stable minimal API definitions.
- **Exit Criteria**:
  - `swagger.json` (or `openapi.yaml`) exported and committed under `docs/api/` (or equivalent) with versioning.
  - Verification bundle: curl commands + sample responses saved under `docs/verification/phase4/`.
  - Checklist items under Phase 4 updated to reference evidence links.
- **Parallelization Notes**: QA/Docs subagent can run this in lockstep with WS-A finishing touches; no need to wait for UI work.
- **Key References**: `docs/Phase4_API_Endpoints_Walkthrough.md`, `docs/LLM_Response_Routing_Implementation_Plan.md` (Phase 4 AC), Swagger UI output.

### WS-C – Shared DTOs + SignalR Client Infrastructure
- **Scope**:
  - Relocate task update DTOs into `DotnetAgents.Core` so both Blazor and console apps share structs.
  - Finalize `TaskHubService` (web) and introduce a mirrored service for the console host.
  - Implement reconnection policies, logging, and cancellation hooks.
- **Dependencies**: Requires WS-A schemas; benefits from WS-B contract but can start once DTOs are known.
- **Exit Criteria**:
  - Shared DTO project referenced by API, web, and console.
  - Unit tests covering hub connection callbacks.
  - Documentation snippet (appendix in `Phase5_Web_SignalR_Client_Walkthrough.md`) describing configuration + console wiring.
- **Parallelization Notes**: Run concurrently with WS-A final testing; coordinate via shared `contracts/` folder.
- **Key References**: `docs/Phase5_Web_SignalR_Client_Walkthrough.md`, `docs/Phase7_Update_Chat_UI_Walkthrough.md` (for final UX expectations).

### WS-D – Console Parity Sweep
- **Scope**: Apply WS-C abstractions to the console experience (chat + future dashboard), ensuring parity with Blazor behaviors (status chips, progress text, completion summaries).
- **Entry Criteria**: WS-C provides reusable hub client + DTOs.
- **Exit Criteria**:
  - Console chat displays live status/progress/result without manual refresh.
  - Parity checklist appended to `LLM_Response_Routing_Implementation_Plan.md` (and future PR templates).
  - Manual transcript recorded (e.g., asciinema) for documentation.
- **Parallelization Notes**: Can run in parallel with WS-E once the console hub client skeleton exists.
- **Key References**: `docs/Phase7_Update_Chat_UI_Walkthrough.md`, console project under `IntelAgent.Console/` (if applicable).

### WS-E – Phase 6 Tasks Monitoring UX
- **Scope**: Build `/tasks` page (web) plus console dashboard covering:
  - Active task grid with SignalR updates.
  - Detail panel showing progress, timestamps, and DB metrics.
  - Quick stats header fed by `/api/tasks/stats`.
- **Entry Criteria**: WS-A endpoints available; WS-C provides client plumbing. UI scaffolding (layout, mock data) can start immediately using fake repositories.
- **Exit Criteria**:
  - Razor components (`TaskCard`, `TaskTimeline`, `DatabaseMetrics`) created with unit/UI tests where feasible.
  - Console dashboard renders equivalent data sections.
  - Screenshots + console captures stored in `docs/Phase6_*` (once authored).
- **Parallelization Notes**: Split between Frontend and Console subagents; they can collaborate via shared interface definitions.
- **Key References**: `docs/IMPLEMENTATION_PLAN_SIGNALR_TASKS.md`, `docs/ARCHITECTURE_DOCUMENTATION.md` (Current Implementation Status + Data Flow sections).

### WS-F – Database Insights Surfacing & Validation
- **Scope**: Consume Phase 8 metrics in both UIs, add alerting thresholds, and run load tests to ensure instrumentation overhead stays <5%.
- **Entry Criteria**: WS-E surfaces available spots for metrics widgets.
- **Exit Criteria**:
  - `/api/database/metrics` data visualized on both web and console dashboards.
  - Performance report appended to `docs/Phase8_Database_Insights_Walkthrough.md`.
  - Feature flag + telemetry sampling documented.
- **Parallelization Notes**: Backend engineer can begin verifying the interceptor outputs immediately (curl + logs), then partner with UI owners once WS-E has placeholder components.
- **Key References**: `docs/Phase8_Database_Insights_Walkthrough.md`.

### WS-G – Phase 6 Walkthrough Authoring
- **Scope**: Draft `docs/Phase6_Tasks_Monitoring_Page_Walkthrough.md`, aligning with the structure used by Phases 4, 7, and 8.
- **Entry Criteria**: Implementation guidance already exists in `docs/IMPLEMENTATION_PLAN_SIGNALR_TASKS.md` and `docs/Complete_Implementation_Walkthrough.md`.
- **Exit Criteria**:
  - New walkthrough file committed with overview, learning objectives, step-by-step instructions, verification checklist, and console parity notes.
  - Task added to `LLM_Response_Routing_Implementation_Plan.md` (see WS-G dependency in main plan).
- **Parallelization Notes**: Documentation specialist can start immediately; no code dependency.

## Delegation Recommendations
1. **Backend Subagent** – Own WS-A and partner with Docs for WS-B. Publish interim OpenAPI files so downstream teams can mock API responses.
2. **QA/Technical Writer** – Drive WS-B and WS-G simultaneously; they only need access to Swagger + existing walkthrough templates.
3. **Platform/Frontend Engineer** – Lead WS-C, ensuring shared DTOs unblock both UI teams.
4. **Console UI Engineer** – Focus on WS-D and the console half of WS-E.
5. **Web UI Engineer** – Build the Blazor components for WS-E
6. **Observability Engineer** – Handle WS-F once WS-E provides visual hooks; coordinate with Backend for load testing scripts.

## Reference Library
| Document                                       | Purpose                                                                 |
| ---------------------------------------------- | ----------------------------------------------------------------------- |
| `docs/IMPLEMENTATION_PLAN_SIGNALR_TASKS.md`    | End-to-end implementation blueprint, especially Phase 6 requirements.   |
| `docs/Phase4_API_Endpoints_Walkthrough.md`     | Detailed instructions for WS-A deliverables.                            |
| `docs/Phase7_Update_Chat_UI_Walkthrough.md`    | Target UX for chat parity (inputs to WS-D).                             |
| `docs/Phase8_Database_Insights_Walkthrough.md` | Instrumentation expectations for WS-F.                                  |
| `docs/ARCHITECTURE_DOCUMENTATION.md`           | System topology and current-state checkpoints to keep branches aligned. |
| `docs/Complete_Implementation_Walkthrough.md`  | Template to follow when drafting the Phase 6 walkthrough (WS-G).        |

---
*Prepared 2025-11-21 to coordinate subagent delegation across Phases 4–8.*
