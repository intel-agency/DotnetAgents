# IntelAgent Agent Evaluation CI/CD Plan – Review

## Summary
- Current plan establishes clear intent to combine Evidently, DeepEval, and Braintrust but lacks implementation-critical detail.
- Multiple prerequisites (CLI shim, datasets, secrets, scripts) are undefined in the repository, blocking execution.
- Governance sections note ownership needs yet do not assign accountable roles or success criteria.

## Notable Strengths
- Articulates objectives that align with quality gating and longitudinal monitoring.
- Maps each eval tool to a lifecycle stage (local, PR, nightly) with rough workflow triggers.
- Lists shared prerequisites up front, helping surface cross-cutting enablers.

## Gaps & Risks

### Prerequisite Readiness
- **CLI shim unspecified** *(Shared Prerequisites §1)*: No IntelAgent.Cli project exists; lacking interface contract (input schema, stdout format, deterministic seeding) makes every downstream step speculative.
- **Dataset lifecycle undefined** *(Shared Prerequisites §2)*: Plan names folder structure but omits sourcing, versioning, PII review, or validation scripts; without these, regression signals cannot be trusted.
- **Secret management clarity** *(Shared Prerequisites §3)*: Reference to multiple API keys without environment separation, rotation workflow, or local fallback; risk of blocking contributors and leaking secrets.
- **Baseline policy enforcement** *(Shared Prerequisites §4)*: States targets but no storage location (repo vs. external), update cadence, or approval gate—baseline drift likely.

### Pipeline Design
- **Developer flow coupling** *(Pipeline Blueprint §1)*: Lacks guidance on Python environment management (uv/venv), dependency pinning, and how CLI shim artifacts feed DeepEval tests; no strategy for running on Windows vs. Linux.
- **Evidently workflow assumptions** *(Pipeline Blueprint §2)*: Expects CSV generation and Evidently Cloud integration but omits dataset schema contract, storage of `artifacts/responses.csv`, and handling of model access failures; `cloud://intelagent-regression` path requires validation.
- **Braintrust action setup** *(Pipeline Blueprint §3)*: Uses `pnpm` without ensuring it is available in repo; missing definition files under `evals/braintrust/` and mapping between GitHub workflow secrets and CLI config.
- **Hybrid benchmark job** *(Pipeline Blueprint §4)*: Mentions weekly job but no triggering workflow definition, retention, or integration with security review; merging DeepEval and Evidently outputs is unspecified.

### Governance & Execution
- **Implementation roadmap acceptance missing** *(Implementation Roadmap)*: Phases lack deliverable definitions, entry/exit criteria, or owners; progress cannot be verified objectively.
- **Incident response** *(Reporting & Governance)*: Playbook suggests reruns but lacks communication tree, rollback guidelines, or SLA targets, risking slow triage.
- **Dataset stewardship** *(Reporting & Governance)*: Calls for owners yet does not name roles/functions; adds approval requirement without process or tooling integration.
- **Tool failure mitigation**: No contingency plan if any service (Braintrust, Evidently, OpenRouter) degrades; pipeline reliability risk remains unaddressed.

## Recommendations

### Must Address First
- Define and implement IntelAgent execution shim: project path, CLI arguments, JSON schema, configuration surface (model provider, seed) with automated tests.
- Draft dataset governance doc (`docs/evals/DATASETS.md`) covering sourcing, review, anonymization, versioning (Git LFS or storage), and validation script requirements.
- Produce secrets management playbook: map secrets to environments, document local `.env.template`, and outline rotation + GitHub environment usage with least privilege.
- Attach acceptance criteria to each roadmap phase (deliverables, exit tests, owners) to enable tracking.

### Address Next
- Specify Python environment/tooling strategy (uv or venv + requirements lockfile) and integrate with CLI shim build step for DeepEval tests.
- Flesh out Evidently workflow: schema contract for generated CSV, caching strategy, failure-handling (retry/fallback), and artifact retention naming.
- Document Braintrust setup prerequisites: Node/pnpm installation, `package.json` updates, evaluation config examples, and how experiment IDs feed `metrics/history.json`.
- Design contingency procedures for third-party outages (skip logic with notifications, degraded mode thresholds).

### Future Enhancements
- Add automated dataset validation check (schema + statistical drift) before running eval suites.
- Integrate observability (structured logs, telemetry tags) across eval runs to improve diagnostics.
- Extend governance with RACI matrix assigning planner, QA, platform, and security roles.

## Evidence Sources
- Reviewed `docs/cicd_eval_fx.md` (Objectives, Shared Prerequisites, Pipeline Blueprint, Implementation Roadmap, Reporting & Governance, Reference Resources).