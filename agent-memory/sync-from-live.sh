#!/usr/bin/env bash
# Sync Claude Code's live per-project memory directory BACK to the vendored
# snapshot in this folder.
#
# When Claude writes new memory notes during a session (or edits existing
# ones), the writes land in:
#   $HOME/.claude/projects/{project-hash}/memory/
#
# The vendored snapshot in this folder goes stale. Run this script to copy
# the live memory directory back to the vendored snapshot, then git-commit
# the changes so the snapshot stays current for the next fresh-machine clone.
#
# Behavior:
#   - Computes the project-hash from the script's location
#   - Refuses to run if the live memory directory doesn't exist or is empty
#   - Removes the existing vendored .md files (except README + scripts) to
#     ensure deleted memory notes are reflected in the snapshot
#   - Copies all .md files from the live memory dir to this folder
#   - Reports the diff against the previous state via git status (if in a git repo)

set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SOURCE_DIR}/.." && pwd)"

PROJECT_HASH=$(echo "${PROJECT_ROOT}" | tr '/._' '---')
LIVE_DIR="${HOME}/.claude/projects/${PROJECT_HASH}/memory"

echo "Syncing live memory back to vendored snapshot"
echo "  Live source:    ${LIVE_DIR}"
echo "  Vendored dest:  ${SOURCE_DIR}"
echo ""

if [ ! -d "${LIVE_DIR}" ]; then
    echo "ERROR: live memory directory not found at ${LIVE_DIR}" >&2
    echo "Did you mean to run install.sh first?" >&2
    exit 1
fi

if [ -z "$(ls -A "${LIVE_DIR}" 2>/dev/null || true)" ]; then
    echo "ERROR: live memory directory is empty; nothing to sync." >&2
    exit 1
fi

# Remove old vendored .md files (except README) so deleted memory notes
# don't linger in the vendored snapshot
for f in "${SOURCE_DIR}"/*.md; do
    [ -f "${f}" ] || continue
    basename=$(basename "${f}")
    if [ "${basename}" = "README.md" ]; then
        continue
    fi
    rm "${f}"
done

# Copy all .md files from live dir to vendored dir
COPIED=0
for f in "${LIVE_DIR}"/*.md; do
    [ -f "${f}" ] || continue
    cp -p "${f}" "${SOURCE_DIR}/"
    echo "  Synced $(basename "${f}")"
    COPIED=$((COPIED + 1))
done

echo ""
echo "Done. Synced ${COPIED} memory file(s)."
echo ""
echo "Next steps:"
echo "  1. Review the changes:  git status agent-memory/"
echo "  2. Commit the snapshot: git add agent-memory/ && git commit -m 'chore: sync agent-memory snapshot'"
echo "  3. Push to GitHub:      git push"
