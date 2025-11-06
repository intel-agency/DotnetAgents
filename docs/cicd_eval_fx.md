# IntelAgent Agent Evaluation CI/CD Plan

## Objectives
- Gate IntelAgent.Agent changes on behavioral quality, not just unit tests.
- Detect regressions quickly during pull requests while maintaining long-term trends.
- Standardize datasets, metrics, and reporting across engineering, QA, and product teams.

## Tool Portfolio

| Tool | Primary Role | CI/CD Touchpoints | Notable Capabilities |
| --- | --- | --- | --- |
| [Evidently GitHub Action](https://www.evidentlyai.com/blog/llm-unit-testing-ci-cd-github-actions) | Regression testing on curated datasets | PR workflows, push to `main` | Wraps Evidently CLI, supports local/Cloud datasets, fails workflow on threshold breach, produces detailed reports & dashboards. |
| [Confident AI DeepEval](https://github.com/confident-ai/deepeval) | Metric-driven unit & component tests | Developer laptops, CI smoke stage | PyTest-style assertions, rich LLM metrics (G-Eval, hallucination, RAG), tracing decorators, cloud dashboard for sharing runs. |
| [Braintrust Eval Action](https://www.braintrust.dev/articles/best-ai-evals-tools-cicd-2025) / [GitHub Action](https://github.com/braintrustdata/eval-action) | Experiment tracking & trend analysis | Nightly + release branches | Posts PR comments, tracks experiments with git metadata, manages concurrency, provides side-by-side comparisons over time. |

## Shared Prerequisites

1. **Agent execution shim**  
	- Expose `dotnet run --project IntelAgent.Cli` (new console host) that accepts JSON prompts from stdin and emits responses.
	- Ensure deterministic inputs via seeded randomness and configurable model parameters.

2. **Canonical eval datasets**  
	- Store under `DotnetAgents/Evals/` with subfolders per evaluation suite (`baseline`, `safety`, `regression`).  
	- Maintain schema: `id`, `input`, `expected_output` (optional), `metadata` (JSON for tags such as scenario, risk, persona).

3. **Secrets & configuration**  
	- Introduce `EVAL_OPENROUTER_API_KEY`, `BRAINTRUST_API_KEY`, `DEEPEVAL_API_KEY`, `EVIDENTLY_API_KEY` managed via GitHub environment secrets.
	- Provide local `.env.template` files for contributors, keeping actual secrets out of source control.

4. **Baseline scoring policy**  
	- Document target thresholds per suite (e.g., correctness ≥0.7, toxicity ≤0.1).  
	- Record baseline experiment IDs (Braintrust) and baseline report timestamps (Evidently Cloud) for comparison.

## Pipeline Blueprint

### 1. Local Developer Flow (DeepEval)
1. Install Python tooling (`pip install deepeval`) via a dedicated virtual environment.  
2. Provide `deepeval_tests/` with PyTest-style cases exercising key agent behaviors using the CLI shim.  
3. Add `dotnet test` companion to ensure .NET builds before Python evals.  
4. Offer `pwsh scripts/run-deepeval.ps1` to invoke `deepeval test run` with developer-friendly defaults.  
5. Encourage pre-commit hook (optional) to run a targeted subset (`deepeval test run tests/smoke.py`).

### 2. Pull Request Quality Gates (Evidently Action)
1. Create `.github/workflows/evidently-agent.yml` triggered on `pull_request` and `push` to protected branches.  
2. Use `actions/setup-python@v5` then `pip install -r DotnetAgents/Evals/requirements.txt` (contains `evidently[llm]`, `openai`).  
3. Run `dotnet build` + CLI shim to generate responses saved to `artifacts/responses.csv`.  
4. Invoke `evidentlyai/evidently-report-action@v1` with:
	- `input_path`: local CSV / Cloud dataset ID.  
	- `config_path`: `DotnetAgents/Evals/evidently_config.py` describing descriptors (LLM judge, word count, safety checks) and pass/fail tests.  
	- `output`: `cloud://intelagent-regression` to persist results.  
5. Upload report artifacts and expose summary via GitHub Check; fail workflow on threshold breach to block merge.  
6. Schedule weekly dataset refresh tasks (e.g., `workflow_dispatch` job) to sync new examples into Evidently Cloud.

### 3. Nightly Experiment Tracking (Braintrust)
1. Add `.github/workflows/braintrust-nightly.yml` on cron (e.g., `0 6 * * *`) and release branches.  
2. Set up Node.js 20 runtime (`actions/setup-node@v4`) with `braintrust` CLI in `package.json`.  
3. Run `pnpm braintrust eval` (or Python equivalent) referencing `evals/braintrust/` definitions that call the CLI shim.  
4. Use `braintrustdata/eval-action@v1` with PR comment permissions to publish experiment diffs when triggered from PRs.  
5. Capture experiment IDs in artifacts and append to `metrics/history.json` for traceability.  
6. Configure Braintrust project dashboards for: latency, correctness, tool-use accuracy, and watch mode for rapid iteration.

### 4. Scheduled Benchmark Suite (DeepEval + Evidently Hybrid)
- Weekly job invoking `deepeval` red-teaming suites (`deepeval redteam run`) combined with Evidently adversarial dataset to stress-test safety dimensions.  
- Persist merged results to a central storage (e.g., Azure Blob or GitHub artifacts) for compliance reviews.

## Implementation Roadmap

1. **Phase 0 – Setup & Instrumentation**  
	- Build the CLI shim, confirm deterministic outputs, and stub evaluation datasets.  
	- Add documentation in `docs/evals/README.md` explaining execution contract.  

2. **Phase 1 – DeepEval Developer Tests**  
	- Author smoke + component tests mirroring current IntelAgent scenarios (prompt summarization, tool invocation).  
	- Integrate with local scripts and optional pre-commit.  

3. **Phase 2 – PR Regression Gate (Evidently)**  
	- Finalize dataset curation and config; wire GitHub Action to produce Cloud/local reports.  
	- Define failure messaging and triage playbook.  

4. **Phase 3 – Experiment Tracking (Braintrust)**  
	- Onboard to Braintrust, configure project, store API key, and commit eval definitions.  
	- Enable PR comments and nightly cron for longitudinal tracking.  

5. **Phase 4 – Continuous Improvement**  
	- Add adversarial suites, expand datasets, and automate baseline refresh using Braintrust comparisons + Evidently trend dashboards.  
	- Integrate notifications (Teams/Slack) when regressions persist for >2 runs.

## Reporting & Governance

- **Artifact retention**: Publish Evidently HTML/JSON reports and Braintrust experiment summaries as build artifacts with 30-day retention.
- **Trend analysis**: Weekly review meeting leveraging Braintrust experiment comparisons and Evidently dashboard history to adjust thresholds.
- **Dataset stewardship**: Assign owners to approve changes; require PRs with rationale when modifying eval cases.
- **Security**: Scope API keys per environment, rotate quarterly, and ensure secrets are masked in logs.  
- **Incident response**: Document playbook—re-run eval locally, inspect failing cases, patch prompts/code, update baselines.

## Reference Resources
- Evidently blog: CI/CD for LLM apps with GitHub Actions (June 2025).  
- DeepEval GitHub repository (Confident AI).  
- Braintrust AI evals in CI/CD article (Oct 2025) and Braintrust Eval Action README.



###
New Frameworks

* https://llmdocs.deepchecks.com/docs/ci-cd
* https://www.telusdigital.com/insights/data-and-ai/article/continuous-evaluation-of-generative-ai-using-ci-cd-pipelines
* https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/evaluation-github-action?tabs=foundry-project