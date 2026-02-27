#!/usr/bin/env bash
set -euo pipefail

ORG="FriendsOfGalaxy"

# Fixed temp root (as requested)
TMP_ROOT="/mnt/g/tmp/${ORG}"

REPO_ROOT=""
TARGET_BASE=""
SHALLOW=1
VERBOSE=1

REPOS=(
  "galaxy-integration-steam"
  "buildtools"
  "galaxy-integration-origin"
  "galaxy-integration-uplay"
  "galaxy-integration-paradox"
  "galaxy-integration-rockstar"
  "galaxy-integration-bethesda"
  "galaxy-integration-psn"
  "galaxy-integration-blizzard"
  "galaxy-integration-wargaming"
  "galaxy-integration-humble"
  "cfg"
  "galaxy-integration-gw2"
  "update-test-integration"
  "galaxy-integrations-synchronizer"
  "galaxy-integration-ffxiv"
  "galaxy-integration-minecraft"
  "galaxy-integration-pathofexile"
  "galaxy-integration-epic"
  "galaxy-integration-battlenet"
  "template-galaxy-integration"
  "galaxy-integrations-python-api"
)

log() { [[ "$VERBOSE" -eq 1 ]] && echo "[$(date +'%H:%M:%S')] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

# Auto-detect repo root
if git_root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
  REPO_ROOT="$git_root"
else
  die "Run inside a git repo."
fi

# Default target = <repo_root>/_references
TARGET_BASE="${REPO_ROOT}/_references"
DEST_ROOT="${TARGET_BASE}/${ORG}"

mkdir -p "$DEST_ROOT"
mkdir -p "$TMP_ROOT"

log "Repo root : $REPO_ROOT"
log "Temp root : $TMP_ROOT"
log "Target    : $DEST_ROOT"
echo

sync_one() {
  local repo="$1"
  local url="https://github.com/${ORG}/${repo}.git"
  local tmp_repo="${TMP_ROOT}/${repo}"
  local dest="${DEST_ROOT}/${repo}"

  log "==> ${repo}"

  # Clean temp folder
  rm -rf "$tmp_repo"

  # Clone into fixed temp path
  if [[ "$SHALLOW" -eq 1 ]]; then
    git clone --depth 1 --quiet "$url" "$tmp_repo" || {
      echo "!! clone failed: $repo" >&2
      return 1
    }
  else
    git clone --quiet "$url" "$tmp_repo" || {
      echo "!! clone failed: $repo" >&2
      return 1
    }
  fi

  local commit
  commit="$(git -C "$tmp_repo" rev-parse HEAD 2>/dev/null || true)"

  mkdir -p "$dest"

  # Rsync excluding git metadata
  rsync -a --delete \
    --exclude='.git' \
    "$tmp_repo/" "$dest/"

  # Provenance marker
  {
    echo "source_url=$url"
    echo "source_commit=$commit"
    echo "synced_at_utc=$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  } > "$dest/.reference_source"

  log "    synced ${repo} @ ${commit:0:12}"
  echo
}

for r in "${REPOS[@]}"; do
  sync_one "$r"
done

log "All done."