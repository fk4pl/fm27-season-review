#!/usr/bin/env bash
# Season Review for Football Manager 27 - Linux / Steam Proton installer.
#
# Run from the folder you extracted the release zip into:
#   ./install-linux.sh                      install (finds the game in your Steam libraries)
#   ./install-linux.sh --game-dir "<dir>"   install into a specific game folder
#   ./install-linux.sh --uninstall          remove the plugin (keeps BepInEx, config and reports)
#   ./install-linux.sh --yes                answer "yes" to questions (e.g. installing BepInEx)
#
# The script only touches <game>/BepInEx/plugins/SeasonReview and, if you agree, installs
# BepInEx itself. It never touches your saves.

set -euo pipefail

BEPINEX_URL="https://builds.bepinex.dev/projects/bepinex_be/793/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793%2Bdbc82f0.zip"
BEPINEX_SHA256="5b63d7d0e92a2448ccd13b738ff95ff1cc1026c6d718555d4df8e6c824953ac4"
GAME_FOLDER_NAME="Football Manager 27"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_dir=""
uninstall=0
assume_yes=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --game-dir) game_dir="${2:-}"; shift 2 ;;
    --uninstall) uninstall=1; shift ;;
    --yes|-y) assume_yes=1; shift ;;
    -h|--help) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1 (try --help)" >&2; exit 2 ;;
  esac
done

ask() {
  [[ $assume_yes -eq 1 ]] && return 0
  local reply
  read -r -p "$1 [y/N] " reply || return 1
  [[ "$reply" =~ ^[Yy] ]]
}

# --- find the game ------------------------------------------------------------------------
find_game() {
  local roots=(
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
  )
  local libraries=()
  for root in "${roots[@]}"; do
    [[ -d "$root/steamapps" ]] && libraries+=("$root")
    local vdf="$root/steamapps/libraryfolders.vdf"
    if [[ -f "$vdf" ]]; then
      while IFS= read -r path; do libraries+=("$path"); done \
        < <(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf")
    fi
  done
  for library in "${libraries[@]}"; do
    local candidate="$library/steamapps/common/$GAME_FOLDER_NAME"
    if [[ -f "$candidate/fm.exe" ]]; then echo "$candidate"; return 0; fi
  done
  return 1
}

if [[ -z "$game_dir" ]]; then
  if ! game_dir="$(find_game)"; then
    echo "Could not find '$GAME_FOLDER_NAME' in your Steam libraries." >&2
    echo "Run again with --game-dir \"/path/to/$GAME_FOLDER_NAME\"." >&2
    exit 1
  fi
fi
if [[ ! -f "$game_dir/fm.exe" ]]; then
  echo "'$game_dir' does not look like the FM27 folder (no fm.exe)." >&2
  exit 1
fi
echo "Game folder: $game_dir"

# A running game keeps the plugin open; installing over it is not safe. A process counts as the
# game only if one of its arguments is the fm.exe path itself (a shell command that merely
# mentions fm.exe somewhere does not).
game_running() {
  local pid arg
  for pid in $(pgrep -f 'fm[.]exe' 2>/dev/null || true); do
    [[ "$pid" == "$$" || ! -r "/proc/$pid/cmdline" ]] && continue
    while IFS= read -r -d '' arg; do
      if [[ "$arg" =~ (^|[/\\])fm\.exe$ ]]; then return 0; fi
    done < "/proc/$pid/cmdline"
  done
  return 1
}

if game_running; then
  echo "Football Manager 27 is running. Close the game and run this script again." >&2
  exit 1
fi

plugin_dir="$game_dir/BepInEx/plugins/SeasonReview"

# --- uninstall ----------------------------------------------------------------------------
if [[ $uninstall -eq 1 ]]; then
  if [[ -d "$plugin_dir" ]]; then
    rm -rf -- "$plugin_dir"
    echo "Removed $plugin_dir"
  else
    echo "Season Review is not installed."
  fi
  echo "BepInEx, the settings in BepInEx/config and your reports were left in place."
  exit 0
fi

# --- BepInEx ------------------------------------------------------------------------------
if [[ ! -f "$game_dir/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ]]; then
  echo "BepInEx is not installed in this game folder."
  if ask "Download and install BepInEx 6.0.0-be.793 (IL2CPP, win-x64) from builds.bepinex.dev?"; then
    for tool in curl unzip sha256sum; do
      command -v "$tool" >/dev/null || { echo "'$tool' is needed to install BepInEx." >&2; exit 1; }
    done
    tmp="$(mktemp -d)"
    trap 'rm -rf -- "$tmp"' EXIT
    echo "Downloading BepInEx..."
    curl -fL --retry 2 -o "$tmp/bepinex.zip" "$BEPINEX_URL"
    actual="$(sha256sum "$tmp/bepinex.zip" | cut -d' ' -f1)"
    if [[ "$actual" != "$BEPINEX_SHA256" ]]; then
      echo "Checksum mismatch for the BepInEx download; not installing it." >&2
      exit 1
    fi
    unzip -q -n "$tmp/bepinex.zip" -d "$game_dir"   # -n: never overwrite existing files
    echo "BepInEx installed."
    bepinex_new=1
  else
    echo "Install BepInEx first (see README.md), then run this script again." >&2
    exit 1
  fi
fi

# --- plugin -------------------------------------------------------------------------------
source_dir="$here/BepInEx/plugins/SeasonReview"
if [[ ! -f "$source_dir/SeasonReview.dll" ]]; then
  echo "SeasonReview.dll not found next to this script ($source_dir)." >&2
  echo "Run the script from the folder you extracted the release zip into." >&2
  exit 1
fi

mkdir -p "$plugin_dir/lang"
rm -f -- "$plugin_dir/SeasonReview.dll"       # replace as a new file
cp -- "$source_dir/SeasonReview.dll" "$plugin_dir/"
cp -- "$source_dir"/lang/*.txt "$plugin_dir/lang/"
echo "Installed Season Review to $plugin_dir"

# --- launch option reminder ---------------------------------------------------------------
cat <<'EOF'

Steam launch option (needed once, so Proton loads BepInEx):
  Steam > Football Manager 27 > Properties > Launch options:
    WINEDLLOVERRIDES="winhttp=n,b" %command%
  If you already have launch options, put WINEDLLOVERRIDES="winhttp=n,b" in front of them.
EOF
if [[ "${bepinex_new:-0}" -eq 1 ]]; then
  echo
  echo "The first start with BepInEx takes a few minutes while it prepares the game's files."
fi
echo
echo "Reports will be written to the game's Documents folder inside the Proton prefix:"
echo "  <Steam library>/steamapps/compatdata/4530370/pfx/drive_c/users/steamuser/Documents/Sports Interactive/Football Manager 27/SeasonReviews/"
