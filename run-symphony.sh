#!/usr/bin/env zsh
# ─────────────────────────────────────────────────────────────────────────────
# Run Symphony factory against this repo.
#
# Usage:
#   ./run-symphony.sh              # live run — picks up open issues with symphony-run label
#   ./run-symphony.sh --dry-run    # inspect queue/config only; no pipeline execution
#   ./run-symphony.sh --health     # local/controller readiness report
#
# Prerequisites:
#   - gh auth login                (GitHub CLI authenticated)
#   - SYMPHONY_DIR set or default  (path to project_symphony checkout)
#   - project_symphony/.env        (contains MISTRAL_API_KEY)
#
# Runtime flags documented here (discovered during live runs on this repo):
#   NOTE: SYMPHONY_TOOL_ADAPTER is retired (Spec 145) — raises RuntimeConfigurationError
#   SYMPHONY_GATE_MODE=enforce           fail closed on S2-S5 gate failures
#   SYMPHONY_PARALLEL_REVIEWERS=true      all 4 reviewers concurrently → -200s S4
#   SYMPHONY_ENV=local                    default; override for the target environment
#   SYMPHONY_CHECKPOINT_PURPOSE=dev       optional purpose-specific DB/state suffix
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# ── Locate Symphony ───────────────────────────────────────────────────────────
SYMPHONY_DIR="${SYMPHONY_DIR:-${HOME}/Projects/project_symphony}"
if [[ ! -d "$SYMPHONY_DIR" ]]; then
  echo "ERROR: Symphony not found at $SYMPHONY_DIR"
  echo "Set SYMPHONY_DIR=<path-to-project_symphony>"
  exit 1
fi

# ── Load .env from Symphony ───────────────────────────────────────────────────
if [[ -f "$SYMPHONY_DIR/.env" ]]; then
  set -a; source "$SYMPHONY_DIR/.env"; set +a
else
  echo "ERROR: $SYMPHONY_DIR/.env not found (needs MISTRAL_API_KEY)"
  exit 1
fi

# ── GitHub token ──────────────────────────────────────────────────────────────
GITHUB_TOKEN="${GITHUB_TOKEN:-$(gh auth token 2>/dev/null)}"
[[ -z "$GITHUB_TOKEN" ]] && { echo "ERROR: gh auth login required"; exit 1; }

# ── Command and delivery mode ────────────────────────────────────────────────
COMMAND="${1:-live}"
if [[ "$COMMAND" != "live" && "$COMMAND" != "--dry-run" && "$COMMAND" != "--health" ]]; then
  echo "ERROR: unsupported argument '$COMMAND' (use --dry-run or --health)"
  exit 2
fi
DELIVERY_MODE="live"
if [[ "$COMMAND" == "--dry-run" ]]; then
  echo "DRY RUN (queue/configuration inspection only; no worktree, LLM, or state mutation)"
  DELIVERY_MODE="disabled"
elif [[ "$COMMAND" == "--health" ]]; then
  echo "HEALTH CHECK (non-mutating)"
fi

# Keep local development as the safe default, but allow the caller to select
# the execution environment/purpose without editing this launcher. The
# checkpoint purpose may intentionally differ from the gate environment (for
# example, a local observe run writing to a dedicated UAT rehearsal DB).
RUN_ENV="${SYMPHONY_ENV:-local}"
RAW_CHECKPOINT_PURPOSE="${SYMPHONY_CHECKPOINT_PURPOSE:-$RUN_ENV}"
case "${RAW_CHECKPOINT_PURPOSE:l}" in
  local|development|develop) CHECKPOINT_PURPOSE="dev" ;;
  testing) CHECKPOINT_PURPOSE="test" ;;
  staging|stage) CHECKPOINT_PURPOSE="uat" ;;
  production|live) CHECKPOINT_PURPOSE="prod" ;;
  *) CHECKPOINT_PURPOSE="$RAW_CHECKPOINT_PURPOSE" ;;
esac
RUN_GATE_MODE="${SYMPHONY_GATE_MODE:-enforce}"
AUTO_MERGE="${SYMPHONY_AUTO_MERGE:-false}"

# Every launcher purpose gets a distinct persistence namespace.  The explicit
# overrides are useful for CI/UAT, while the default keeps development output,
# checkpoint history, memory, scopes, and telemetry out of other purposes.
PERSIST_ROOT="${SYMPHONY_PERSISTENCE_ROOT:-$SYMPHONY_DIR/.symphony/$CHECKPOINT_PURPOSE}"
OUTPUT_ROOT="${SYMPHONY_OUTPUT_ROOT:-$PERSIST_ROOT/outputs}"
CHECKPOINT_ROOT="${SYMPHONY_CHECKPOINT_ROOT:-$PERSIST_ROOT/checkpoints}"
TELEMETRY_ROOT="${SYMPHONY_TELEMETRY_DIR:-$PERSIST_ROOT/telemetry}"
EVENTS_ROOT="${SYMPHONY_HARNESS_EVENTS_DIR:-$PERSIST_ROOT/harness-events}"
SCOPE_ROOT="${SYMPHONY_SCOPE_MANIFEST_DIR:-$PERSIST_ROOT/scopes}"
MEMORY_DB="${SYMPHONY_STORE_SQLITE_PATH:-$PERSIST_ROOT/memory/langgraph_store.db}"

# Provider selection is explicit and contract-bound. The default lane uses
# Codex for concept/design/release work and Antigravity for implementation and
# review; Mistral is the provider-neutral inference plane. Set
# SYMPHONY_HARNESS_PROVIDER=kiro (or codex/antigravity) for a single-provider
# rehearsal, or provide your own stage/role map. This is a matrix, not a
# fallback chain: an unavailable selected provider fails the run with evidence.
EXPLICIT_HARNESS_PROVIDER="${SYMPHONY_HARNESS_PROVIDER:-}"
HARNESS_PROVIDER="${EXPLICIT_HARNESS_PROVIDER:-codex}"
if [[ "$HARNESS_PROVIDER" == "agy" ]]; then HARNESS_PROVIDER="antigravity"; fi
if [[ -n "${SYMPHONY_HARNESS_PROVIDER_MAP:-}" ]]; then
  HARNESS_PROVIDER_MAP="$SYMPHONY_HARNESS_PROVIDER_MAP"
elif [[ -n "$EXPLICIT_HARNESS_PROVIDER" ]]; then
  HARNESS_PROVIDER_MAP=""
else
  HARNESS_PROVIDER_MAP="s1_concept=codex,s2_design=codex,s3_development=antigravity,s4_review=antigravity,s5_release=codex,s6_operate=codex"
fi
INFERENCE_PROVIDER="${SYMPHONY_INFERENCE_PROVIDER_REF:-mistral}"

# ── Source root = this repo ───────────────────────────────────────────────────
SOURCE_ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
echo "Source: $(git -C "$SOURCE_ROOT" log --oneline -1)"
echo "Factory: $SYMPHONY_DIR"
echo ""

# ── Run ───────────────────────────────────────────────────────────────────────
cd "$SYMPHONY_DIR"

export GITHUB_TOKEN="$GITHUB_TOKEN"
export SYMPHONY_SOURCE_ROOT="$SOURCE_ROOT"
export SYMPHONY_PERSISTENCE_ROOT="$PERSIST_ROOT"
export SYMPHONY_OUTPUT_ROOT="$OUTPUT_ROOT"
export SYMPHONY_CONTROLLER_OUTPUTS_ROOT="$OUTPUT_ROOT"
export SYMPHONY_CONTROLLER_STATE_PATH="${SYMPHONY_CONTROLLER_STATE_PATH:-$PERSIST_ROOT/controller/state.json}"
export SYMPHONY_CONTROLLER_HISTORY_PATH="${SYMPHONY_CONTROLLER_HISTORY_PATH:-$PERSIST_ROOT/controller/cycle-history.json}"
export SYMPHONY_CONTROLLER_LOCK_PATH="${SYMPHONY_CONTROLLER_LOCK_PATH:-$PERSIST_ROOT/controller/controller.lock}"
export SYMPHONY_CHECKPOINT_ROOT="$CHECKPOINT_ROOT"
export SYMPHONY_CHECKPOINT_DB="${SYMPHONY_CHECKPOINT_DB:-$CHECKPOINT_ROOT/symphony_checkpoints.$CHECKPOINT_PURPOSE.db}"
export SYMPHONY_TELEMETRY_DIR="$TELEMETRY_ROOT"
export SYMPHONY_HARNESS_EVENTS_DIR="$EVENTS_ROOT"
export SYMPHONY_SCOPE_MANIFEST_DIR="$SCOPE_ROOT"
export SYMPHONY_STORE_SQLITE_PATH="$MEMORY_DB"
export SYMPHONY_DELIVERY_MODE="$DELIVERY_MODE"
export SYMPHONY_AUTO_MERGE="$AUTO_MERGE"
export SYMPHONY_COLLECT_MODE=live
export SYMPHONY_ENV="$RUN_ENV"
export SYMPHONY_CONTROLLER_PURPOSE="$CHECKPOINT_PURPOSE"
export SYMPHONY_CHECKPOINT_PURPOSE="$CHECKPOINT_PURPOSE"
export SYMPHONY_GATE_MODE="$RUN_GATE_MODE"
export SYMPHONY_PROFILE=mistral-fast
export SYMPHONY_PARALLEL_REVIEWERS=true
export SYMPHONY_OPENAI_COMPAT_ENDPOINT=https://api.mistral.ai/v1
export SYMPHONY_OPENAI_COMPAT_MODELS=devstral-small-latest,mistral-small-latest,mistral-medium-latest
export SYMPHONY_INFERENCE_PROVIDER_REF="$INFERENCE_PROVIDER"
export SYMPHONY_INFERENCE_CREDENTIAL_REF="${SYMPHONY_INFERENCE_CREDENTIAL_REF:-mistral}"
export SYMPHONY_PACK_PROVIDER=local
export SYMPHONY_PROVIDER_ROOT="$SYMPHONY_DIR/packs/builtin/kiro-native-factory"
export SYMPHONY_PACK_DESCRIPTOR=pack.yaml
export SYMPHONY_PACK_CAPABILITY=kiro-native-factory
export SYMPHONY_REPO_ROOT="$SYMPHONY_DIR"
export SYMPHONY_HARNESS_PROVIDER="$HARNESS_PROVIDER"
export SYMPHONY_HARNESS_PROVIDER_MAP="$HARNESS_PROVIDER_MAP"
export SYMPHONY_WORKTREE_ISOLATION=true
export SYMPHONY_CONSTRUCTION_AUTONOMY_MODE=autonomous
export SYMPHONY_OPERATOR_ID=controller
export SYMPHONY_CONTROLLER_OWNER=aleksei-kachanov
export SYMPHONY_CONTROLLER_REPO=aspnetcore-minapi-todo-sample
export SYMPHONY_CONTROLLER_LABELS=symphony-run
export SYMPHONY_CONTROLLER_POLL_INTERVAL=30

if [[ "$HARNESS_PROVIDER" == "kiro" ]]; then
  # Kiro is an explicit opt-in provider; its profile/session controls are not
  # part of the default Codex + Antigravity matrix.
  export SYMPHONY_KIRO_PROFILE_ROOT="${SYMPHONY_KIRO_PROFILE_ROOT:-$SYMPHONY_DIR}"
  export SYMPHONY_KIRO_SESSION_REUSE="${SYMPHONY_KIRO_SESSION_REUSE:-false}"
else
  unset SYMPHONY_KIRO_PROFILE_ROOT SYMPHONY_KIRO_SESSION_REUSE
fi

if [[ "$COMMAND" == "--dry-run" ]]; then
  export SYMPHONY_CONTROLLER_DRY_RUN=true
  exec uv run python bin/symphony-controller.py --dry-run
elif [[ "$COMMAND" == "--health" ]]; then
  exec uv run python bin/symphony-controller.py --health
fi

exec uv run python bin/symphony-controller.py
