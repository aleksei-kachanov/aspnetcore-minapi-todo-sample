#!/usr/bin/env zsh
# ─────────────────────────────────────────────────────────────────────────────
# Run Symphony factory against this repo.
#
# Usage:
#   ./run-symphony.sh              # live run — picks up open issues with symphony-run label
#   ./run-symphony.sh --dry-run    # simulate, no writes
#
# Prerequisites:
#   - gh auth login                (GitHub CLI authenticated)
#   - SYMPHONY_DIR set or default  (path to project_symphony checkout)
#   - project_symphony/.env        (contains MISTRAL_API_KEY)
#
# Runtime flags documented here (discovered during live runs on this repo):
#   NOTE: SYMPHONY_TOOL_ADAPTER is retired (Spec 145) — raises RuntimeConfigurationError
#   SYMPHONY_GATE_MODE=observe            waive S4 review blocks locally
#   SYMPHONY_STALL_EMPTY_TOOL=10          prevent premature Kiro session kills
#   SYMPHONY_PARALLEL_REVIEWERS=true      all 4 reviewers concurrently → -200s S4
#   SYMPHONY_ENV=local                    required for non-enforce gate mode
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

# ── Delivery mode ─────────────────────────────────────────────────────────────
DELIVERY_MODE="live"
[[ "${1:-}" == "--dry-run" ]] && { DELIVERY_MODE="simulate"; echo "DRY RUN"; }

# ── Source root = this repo ───────────────────────────────────────────────────
SOURCE_ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
echo "Source: $(git -C "$SOURCE_ROOT" log --oneline -1)"
echo "Factory: $SYMPHONY_DIR"
echo ""

# ── Run ───────────────────────────────────────────────────────────────────────
cd "$SYMPHONY_DIR"

GITHUB_TOKEN="$GITHUB_TOKEN" \
SYMPHONY_SOURCE_ROOT="$SOURCE_ROOT" \
SYMPHONY_DELIVERY_MODE="$DELIVERY_MODE" \
SYMPHONY_AUTO_MERGE=false \
SYMPHONY_COLLECT_MODE=live \
SYMPHONY_ENV=local \
SYMPHONY_GATE_MODE=observe \
SYMPHONY_STALL_EMPTY_TOOL=10 \
SYMPHONY_PROFILE=mistral-fast \
SYMPHONY_PARALLEL_REVIEWERS=true \
SYMPHONY_CONTROLLER_LABELS=symphony-run \
SYMPHONY_CONTROLLER_POLL_INTERVAL=30 \
uv run python bin/symphony-controller.py
