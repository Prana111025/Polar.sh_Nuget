#!/usr/bin/env bash
# Install agent-memory vendored snapshots into Claude Code's per-project memory dir.
#
# Claude Code stores per-project persistent memory at:
#   $HOME/.claude/projects/{project-hash}/memory/
#
# where {project-hash} is the project's absolute path with `/`, `.`, and `_`
# characters replaced by `-`. Example:
#   /Users/mollsandhersh/Repos/Polar.sh_Nuget
#     -> -Users-mollsandhersh-Repos-Polar-sh-Nuget
#
# That directory is machine-local and not in git. This script copies the
# vendored snapshots from agent-memory/ into the correct project-hash
# subdirectory on the current machine, so a fresh-clone gets full memory
# context.
#
# Behavior:
#   - Computes the project-hash from the script's location (works regardless
#     of where the project was cloned on the fresh box)
#   - Creates the destination directory if missing
#   - If the destination contains existing memory files, backs them up to a
#     timestamped directory ($HOME/.agent-memory-backup-YYYYMMDD-HHMMSS/)
#     before overwriting
#   - Copies all .md files from agent-memory/ except this script + sync script + README
#   - Preserves file timestamps + modes (cp -p)

set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SOURCE_DIR}/.." && pwd)"

# Compute Claude Code's project-hash: replace /, ., _ with -
# (Observed format: /Users/.../Polar.sh_Nuget -> -Users-...-Polar-sh-Nuget)
PROJECT_HASH=$(echo "${PROJECT_ROOT}" | tr '/._' '---')

DEST_DIR="${HOME}/.claude/projects/${PROJECT_HASH}/memory"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="${HOME}/.agent-memory-backup-${TIMESTAMP}"

echo "Installing agent-memory vendored snapshots"
echo "  Project root:   ${PROJECT_ROOT}"
echo "  Project hash:   ${PROJECT_HASH}"
echo "  Destination:    ${DEST_DIR}"
echo ""

# If destination exists and has content, back it up first
if [ -d "${DEST_DIR}" ] && [ -n "$(ls -A "${DEST_DIR}" 2>/dev/null || true)" ]; then
    echo "Destination has existing memory files; backing up to ${BACKUP_DIR}"
    mkdir -p "${BACKUP_DIR}"
    cp -pR "${DEST_DIR}/." "${BACKUP_DIR}/"
    echo ""
fi

mkdir -p "${DEST_DIR}"

# Copy all .md files from SOURCE_DIR to DEST_DIR, skipping this script's own README
COPIED=0
for f in "${SOURCE_DIR}"/*.md; do
    [ -f "${f}" ] || continue
    basename=$(basename "${f}")
    if [ "${basename}" = "README.md" ]; then
        continue
    fi
    cp -p "${f}" "${DEST_DIR}/${basename}"
    echo "  Installed ${basename}"
    COPIED=$((COPIED + 1))
done

echo ""
echo "Done. Installed ${COPIED} memory file(s)."
echo "Verify by listing the destination:"
echo "  ls -la ${DEST_DIR}"
