#!/usr/bin/env bash
# Install agent-context vendored snapshots into the home directory.
#
# These three files are home-rooted because they apply across ALL Claude projects
# on this machine (not just PolarSharp). They're snapshotted here so that a
# fresh `git clone` of this repo can bootstrap the home-level agent context
# with one command.
#
# Behavior:
#   - Copies AGENTS.md, CLAUDE.home.md (as CLAUDE.md), and ZoranHorvat.md to $HOME
#   - If any of those files already exist at $HOME, backs them up to a
#     timestamped directory ($HOME/.agent-context-backup-YYYYMMDD-HHMMSS/)
#     and prompts for confirmation before overwriting
#   - Preserves file modes + timestamps (cp -p)

set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOME_DIR="${HOME}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="${HOME}/.agent-context-backup-${TIMESTAMP}"

# Source filename in this directory -> destination filename in $HOME
# (CLAUDE.home.md is named that way to avoid colliding with the project's
# own CLAUDE.md when both files coexist in the same repo; on install, it
# lands at $HOME/CLAUDE.md.)

install_file() {
    local src_name="$1"
    local dest_name="$2"
    local src_path="${SOURCE_DIR}/${src_name}"
    local dest_path="${HOME_DIR}/${dest_name}"

    if [ ! -f "${src_path}" ]; then
        echo "  ERROR: source ${src_path} not found; skipping ${dest_name}" >&2
        return 1
    fi

    if [ -f "${dest_path}" ]; then
        mkdir -p "${BACKUP_DIR}"
        cp -p "${dest_path}" "${BACKUP_DIR}/${dest_name}"
        echo "  Backed up ${dest_name} -> ${BACKUP_DIR}/${dest_name}"
    fi

    cp -p "${src_path}" "${dest_path}"
    echo "  Installed ${src_name} -> ${dest_path}"
}

echo "Installing agent-context vendored snapshots to ${HOME_DIR}"
echo ""

# Check what would be overwritten + prompt before proceeding
NEEDS_BACKUP=()
for pair in "AGENTS.md:AGENTS.md" "CLAUDE.home.md:CLAUDE.md" "ZoranHorvat.md:ZoranHorvat.md"; do
    dest_name="${pair##*:}"
    if [ -f "${HOME_DIR}/${dest_name}" ]; then
        NEEDS_BACKUP+=("${dest_name}")
    fi
done

if [ "${#NEEDS_BACKUP[@]}" -gt 0 ]; then
    echo "These existing files at ${HOME_DIR} will be backed up to ${BACKUP_DIR} before overwrite:"
    for f in "${NEEDS_BACKUP[@]}"; do
        echo "  - ${f}"
    done
    echo ""
    read -r -p "Continue? [y/N] " REPLY
    case "${REPLY}" in
        [yY]|[yY][eE][sS]) ;;
        *) echo "Aborted."; exit 1 ;;
    esac
    echo ""
fi

install_file "AGENTS.md" "AGENTS.md"
install_file "CLAUDE.home.md" "CLAUDE.md"
install_file "ZoranHorvat.md" "ZoranHorvat.md"

echo ""
echo "Done. Verify by checking the home-rooted files:"
echo "  head -3 ${HOME_DIR}/CLAUDE.md"
echo "  head -3 ${HOME_DIR}/AGENTS.md"

if [ "${#NEEDS_BACKUP[@]}" -gt 0 ]; then
    echo ""
    echo "Backed-up originals (if you need to recover or merge):"
    echo "  ${BACKUP_DIR}/"
fi
