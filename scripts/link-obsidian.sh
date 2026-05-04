#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET_SUBDIR="ARTradingTools"

SYMLINKS=(
  "docs:$REPO_ROOT/docs"
  "planning:$REPO_ROOT/planning"
  "todo:$REPO_ROOT/todo"
)

# Find all Obsidian vaults under $HOME
mapfile -t VAULTS < <(find "$HOME" -maxdepth 5 -name ".obsidian" -type d 2>/dev/null | sed 's|/.obsidian$||' | sort)

if [[ ${#VAULTS[@]} -eq 0 ]]; then
  echo "No Obsidian vault found under $HOME."
  exit 1
fi

# Pick vault
if [[ ${#VAULTS[@]} -eq 1 ]]; then
  VAULT="${VAULTS[0]}"
else
  echo "Multiple Obsidian vaults found:"
  for i in "${!VAULTS[@]}"; do
    echo "  [$((i+1))] ${VAULTS[$i]}"
  done
  read -rp "Pick a vault [1-${#VAULTS[@]}]: " PICK
  VAULT="${VAULTS[$((PICK-1))]}"
fi

echo "Using vault: $VAULT"

# Ensure the ARTradingTools directory exists inside the vault
VAULT_DIR="$VAULT/$TARGET_SUBDIR"
mkdir -p "$VAULT_DIR"

for ENTRY in "${SYMLINKS[@]}"; do
  LINK_NAME="${ENTRY%%:*}"
  TARGET="${ENTRY#*:}"
  LINK_PATH="$VAULT_DIR/$LINK_NAME"

  if [[ -L "$LINK_PATH" ]]; then
    CURRENT="$(readlink "$LINK_PATH")"
    if [[ "$CURRENT" == "$TARGET" ]]; then
      echo "Symlink already up to date: $LINK_PATH -> $TARGET"
      continue
    fi
    echo "Replacing existing symlink (was -> $CURRENT)"
    rm "$LINK_PATH"
  elif [[ -e "$LINK_PATH" ]]; then
    echo "Error: $LINK_PATH exists and is not a symlink. Remove it manually and re-run."
    exit 1
  fi

  ln -s "$TARGET" "$LINK_PATH"
  echo "Created: $LINK_PATH -> $TARGET"
done
