# Repository Guidelines

## Project Structure & Module Organization

- `src/project_symphony/`: Python package with the LangGraph-based orchestration engine.
- `src/project_symphony/outer_graph.py`: Outer StateGraph builder — governance gates, HITL, repair routing.
- `src/project_symphony/stages/`: Inner subgraphs per pipeline stage (S1-S6).
- `src/project_symphony/nodes/`: LangGraph node implementations (plan, codegen, fix, review, release, etc.).
- `config/pipeline.yaml`: Team-tier gate predicates, stage config, role routing.
- `config/governance.yaml`: Org-tier (non-overridable) governance predicates.
- `src/project_symphony/schemas.py`: Pydantic schemas for typed artifacts.
- `outputs/run_<id>/`: Per-run artifact directory (generated specs, tests, reports).
- `tests/`: Reserved for repo-level tests (currently minimal/empty).

## Build, Test, and Development Commands

This repo uses `uv` and a LangGraph-based pipeline (no CrewAI, no litellm — both removed).

- Install deps: `uv sync`
- Run workflow (epic text as arg): `uv run project_symphony "Your epic text"`
- Python sanity check: `uv run python -m py_compile src/project_symphony/*.py`
- Run tests: `uv run pytest tests/ -q`

Common workflow knobs (env vars):
- Fast dev loop: `SYMPHONY_PROFILE=mistral-fast` (default; no review loop, minimal cost)
- Preferred profile switch: `SYMPHONY_PROFILE=mistral-fast|mistral-balanced|mistral-full`
- Enable review/fix loop: `SYMPHONY_ENABLE_REVIEW_LOOP=true` and optionally `SYMPHONY_REVIEW_MAX_ITERS=1`
- Include delivery artifacts (Docker/CI scaffolding): `SYMPHONY_INCLUDE_DELIVERY_ARTIFACTS=true`
- Include extended tasks (security/docs/release): `SYMPHONY_INCLUDE_EXTENDED_TASKS=true`
- Include scaffold task (pom.xml + package.json): `SYMPHONY_INCLUDE_SCAFFOLD=true` (default); set `false` to exclude `scaffold_project_task`
 - Memory knobs:
   - `SYMPHONY_AGENT_MEMORY` (default: `true`) — toggles the governed Agent Memory runtime (Spec 008). Cross-run memory items (prior build briefs, review findings, architecture decisions, failure patterns, repair recipes) are retrieved and injected into S2/S3/S4 stages via `memory-context.json` or `TaskSpec.context_hint["memory_context"]`. Memory is stored in a LangGraph BaseStore backend (in-memory for tests, persistent for production). Set `false` only for the explicit kill switch, deterministic isolation, or incident investigation.
   - `SYMPHONY_MEMORY_REQUIRED` (default: `false`) — when `true`, memory retrieval failure blocks the stage (returns `status: halted`). When `false`, failure gracefully degrades to cold-start (empty context).
   - `SYMPHONY_MEMORY_TOP_K` (default: `5`) — max items retrieved per memory kind.
   - `SYMPHONY_MEMORY_TOKEN_BUDGET` (default: `4000`) — total token budget for injected memory.
   - `SYMPHONY_MEMORY_TTL_DAYS` (default: `180`) — items older than this are excluded as stale.
   - `SYMPHONY_MEMORY_PROMOTION_THRESHOLD` (default: `3`) — occurrence count before a memory item is promoted from `observed` to `candidate`/`accepted`.
   - `SYMPHONY_CROSS_RUN_MEMORY` — toggles raw ArtifactStore persistence (build briefs, review findings, architecture decisions).
   - `SYMPHONY_STORE_BACKEND` — backend type (`memory` for tests, `sqlite` for persistent local/CI runs).
   - `SYMPHONY_CHECKPOINT_DB` (default: `symphony_checkpoints.db`) — LangGraph SQLite checkpointer path; enables resumable runs via `SYMPHONY_RESUME_RUN_ID=<run_id>`

## Autonomous Controller (Spec 049)

The standalone controller runs outside the pipeline package and invokes the
installed Symphony CLI through a subprocess. It uses only Python's standard
library and may be started with:

```bash
python bin/symphony-controller.py
```

Controller configuration is supplied through these environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `SYMPHONY_CONTROLLER_OWNER` | — | GitHub repository owner (required) |
| `SYMPHONY_CONTROLLER_REPO` | — | GitHub repository name (required) |
| `SYMPHONY_CONTROLLER_TOKEN` | `GITHUB_TOKEN` fallback | GitHub API token |
| `SYMPHONY_CONTROLLER_LABELS` | `symphony-run` | Comma-separated labels that select open issues |
| `SYMPHONY_CONTROLLER_BLOCK_LABELS` | `controller-paused,do-not-automate` | Comma-separated labels that suppress an issue without recording state |
| `SYMPHONY_CONTROLLER_POLL_INTERVAL` | `60` | Seconds between polls; stale-running recovery uses twice this value |
| `SYMPHONY_CONTROLLER_MAX_CONCURRENT` | `1` (enforced) | Maximum concurrent pipeline runs; v1 processes issues serially |
| `SYMPHONY_CONTROLLER_STATE_PATH` | `symphony_controller/state.json` | Append-only NDJSON idempotency state |
| `SYMPHONY_CONTROLLER_HISTORY_PATH` | `symphony_controller/cycle-history.json` | Append-only cycle history used for warm starts |
| `SYMPHONY_CONTROLLER_OUTPUTS_ROOT` | `outputs` | Root used to find mission economics artifacts |
| `SYMPHONY_CONTROLLER_WARM_START` | `false` | Pass the last three history entries as `SYMPHONY_CYCLE_CONTEXT` |
| `SYMPHONY_MAX_DAILY_COST_USD` | `0` (disabled) | Pause new cycles when recent economics meet this limit |

The controller always sets `SYMPHONY_WORK_PERSIST=true` for its pipeline
subprocess. `SIGTERM` finishes the active subprocess, records an interrupted
cycle for retry, and exits cleanly.

## Autonomous Self-Modification (Spec 053)

For a controlled self-modification run, point the patch strategy at the
checked-out Symphony source tree and keep delivery explicit:

| Variable | Required value / default | Purpose |
|----------|--------------------------|---------|
| `SYMPHONY_WORK_PERSIST` | `true` | Persist the collected issue and mission work objects |
| `SYMPHONY_COLLECT_MODE` | `live` | Collect the GitHub issue through the configured provider |
| `SYMPHONY_DELIVERY_MODE` | `live` | Enable real branch and PR delivery after verification |
| `SYMPHONY_SOURCE_ROOT` | `<path>` | Source checkout that PatchStrategy modifies and verifies |
| `SYMPHONY_OUTPUT_ROOT` | parent of `output_dir` | Root directory for per-run output folders. Used by the dirty-state self-heal scan (`_try_revert_prior_receipt`) to locate apply receipts from prior runs. Defaults to the parent of the current run's output_dir. |
| `SYMPHONY_AUTO_MERGE` | `false` | Explicit opt-in to the 047 merge path. See merge execution modes table below. |
| `SYMPHONY_GITHUB_CLOSE_ON_RELEASE` | `false` | Explicit opt-in to closing the source issue after release |
| `GITHUB_TOKEN` | — | GitHub API credential for live collection/delivery |
| `MISTRAL_API_KEY` | — | Credential for the configured planning/codegen adapter |
| `SYMPHONY_CONTROLLER_OWNER` | — | Repository owner when using the controller |
| `SYMPHONY_CONTROLLER_REPO` | — | Repository name when using the controller |
| `SYMPHONY_CONTROLLER_LABELS` | `symphony-run` | Labels selecting issues for controller processing |
| `SYMPHONY_SPEC_FORMAT` | `none` | Deferred external renderers (Specs 054-B/C/D); native `SymphonySpec` generation in 054-A does not depend on this setting |

### Merge execution modes (Spec 055)

| Mode | `SYMPHONY_AUTO_MERGE` | `SYMPHONY_DELIVERY_MODE` | What happens |
|---|---|---|---|
| Intent-only (default) | `false` | any | PR opened (when `DELIVERY_MODE != disabled`), not merged; pipeline reports `released`; `execution_status=not_requested` |
| Execute — live | `true` | `live` | `GitHubAdapter.merge_pr()` called; PR merged on GitHub; `production_verified=True`; `execution_status=verified` |
| Execute — simulate | `true` | `simulate` | Merge recorded as simulated; no GitHub API call; `production_verified=False`; `execution_status=simulated`; WARNING logged at startup. Only for `optional`-policy routes (fast/standard); `architecture`/`high_risk` routes remain blocked. |

The controller workflow is: create or identify a GitHub issue, add the
`symphony-run` label, and run `bin/symphony-controller.py`. Patch-mode runs
detect the repository from `SYMPHONY_SOURCE_ROOT`, verify the patched source
tree before commit, and keep `.original` context files out of the source
checkout. The controller derives `GITHUB_REPOSITORY` from the issue payload
when it is not already set; an explicit operator value remains the delivery
target override (for example, when delivering to a fork).

## Kiro 2.x ACP Adapter (Spec 058)

Kiro is an explicit opt-in adapter for the standard S1-S5 route. It uses
Kiro 2.x JSON profiles under `.kiro/agents/` and the documented ACP boundary;
it does not use classic chat mode or trust flags.

```bash
kiro-cli login                         # local cached-session authentication
kiro-cli acp --agent epic-classifier   # documented Kiro 2.x profile probe
SYMPHONY_TOOL_ADAPTER=kiro \
SYMPHONY_DELIVERY_MODE=simulate \
SYMPHONY_ENV=test \
uv run project_symphony "Add a health check endpoint"
```

Kiro calls may consume model credits even when delivery is simulated. Do not
place credentials in generated profiles or commit secrets. CI, remote-host
authentication, API-key forwarding, and Kiro v3 host-authenticated ACP are
outside Spec 058; remote/API-key behavior is specified separately in Spec 059.
Layered and persona routes remain explicitly unsupported by the 058 standard
route acceptance claim.

## Agent Memory Architecture (Spec 008)

The Agent Memory Runtime provides cross-run learning for the pipeline:

- **Storage**: LangGraph BaseStore with governed namespaces (`agent_memory`, `agent_memory_usage`, `agent_memory_counters`, `agent_memory_tombstones`). No Chroma/CrewAI memory dependencies.
- **Retrieval**: `MemoryRetriever` normalizes both governed items and legacy raw ArtifactStore data into typed `MemoryItem` objects. `MemorySelector` filters by lifecycle, tombstone, TTL, consumer policy, evidence-only exclusion, and token budget.
- **Injection**: Memory is injected per-stage via `build_memory_context()`. S2 uses `context_hint["memory_context"]`; S3/S4 use `memory-context.json` files. Current-run inputs always take precedence.
- **Governed Writes**: `MemoryWriteValidator` rejects secrets/PII/empty/tombstoned content. `MemoryWriter` persists candidates, tracks occurrence counters, promotes after threshold, and requires governance approval for gate-affecting kinds.
- **Lifecycle**: Items progress through `observed → candidate → accepted → superseded/expired/rejected/deleted`. Only `accepted` and `candidate` are injectable.
- **Kill switch**: `SYMPHONY_AGENT_MEMORY=false` disables all memory retrieval/injection/writes — existing pipeline behavior is preserved exactly.

## Coding Style & Naming Conventions

- Python: 4-space indentation, type hints where practical, prefer small pure helpers.
- YAML: 2-space indentation; keep task/agent keys stable (renames are breaking).
- Artifacts: write outputs under `outputs/run_<id>/` with deterministic filenames.

## Testing Guidelines

- This repo’s primary “tests” are workflow-generated projects (e.g., `backend/`, `ui/`) and their tool-based gates.
- When adding repo tests, use `pytest` in `tests/` and name files `test_*.py`.

## Commit & Pull Request Guidelines

Git history is currently minimal; use a consistent convention going forward:
- Commits: `type(scope): summary` (e.g., `perf(crew): reduce callback overhead`)
- PRs: include a short description, run command used, and the key artifacts produced in `outputs/run_<id>/`.

## Security & Configuration Tips

- Required: set `MISTRAL_API_KEY` in `.env` (do not commit secrets).
- Optional: set `OPENAI_API_KEY` only when explicitly using OpenAI profiles.
- Optional: `SERPER_API_KEY` enables web search tools for some agents.


## Agent Primitives (Spec 013)

Canonical agent primitive registry and tool-native sync system.

### Workflow

```bash
# Validate registry (CI / startup default)
uv run python -m project_symphony.agent_primitives.cli --check

# Validate with JSON output
uv run python -m project_symphony.agent_primitives.cli --check --report-json

# Regenerate native agent files from canonical sources (local dev)
uv run python -m project_symphony.agent_primitives.cli --write
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `SYMPHONY_AGENT_PRIMITIVES_DIR` | `agents/primitives` | Canonical source root |
| `SYMPHONY_AGENT_PRIMITIVES_MANIFEST` | `agents/primitives/manifest.yaml` | Registry file |
| `SYMPHONY_AGENT_PRIMITIVES_SYNC` | `validate` | Startup: `off`, `validate`, or `write` |
| `SYMPHONY_REPO_ROOT` | auto-detect | Override repo root for monorepos or non-root cwd |

### Key Concepts

- **Canonical sources**: `agents/primitives/roles/*.md` (engine roles) + `agents/primitives/helpers/*.md` (personas)
- **Manifest**: `agents/primitives/manifest.yaml` — declares all primitives with kind, stage, capabilities, native support
- **Resolver**: `resolve_agent_reference(tool, role, capability)` — shared by all adapters
- **Sync**: generates `.cursor/agents/*.md` and `.claude/agents/*.md` from canonical sources with freshness headers
- **Provenance**: `agent-primitive-usage.json` written per-run alongside `memory-usage.json`

### Adding a New Engine Role

1. Add canonical source: `agents/primitives/roles/{role-name}.md`
2. Add entry to `agents/primitives/manifest.yaml` under `primitives:`
3. Run `--write` to generate native targets
4. Run `--check` to verify coverage
