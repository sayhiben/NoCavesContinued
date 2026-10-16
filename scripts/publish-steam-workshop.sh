#!/usr/bin/env bash
set -euo pipefail
umask 077

repo="sayhiben/NoCavesContinued"
app_id="294100"
mod_dir_name="NoCavesContinued"
asset_pattern="NoCavesContinued-*.zip"
published_file_id="${STEAM_PUBLISHED_FILE_ID:-}"
steam_user="${STEAM_USERNAME:-}"
steamcmd="${STEAMCMD:-}"
change_note="${STEAM_CHANGE_NOTE:-}"
visibility=""
title=""
description_file=""
preview_file=""
release_zip=""
dry_run=0
keep_workdir=0
non_interactive=0

usage() {
  cat <<'EOF'
Usage:
  scripts/publish-steam-workshop.sh --published-file-id ID --steam-user USER --changenote TEXT [options]

Stages a validated release zip, writes a SteamCMD workshop VDF, then optionally
runs SteamCMD. Downloads the latest GitHub release when --zip is omitted.

Required for real publish:
  --published-file-id ID    Existing Steam Workshop item ID.
                            May also be set with STEAM_PUBLISHED_FILE_ID or
                            PublishedFileId.txt or About/PublishedFileId.txt.
  --steam-user USER         Steam username. May also be set with STEAM_USERNAME.
                            SteamCMD will prompt or use its local trusted login.

Common options:
  --zip PATH                Use this exact release zip without contacting GitHub.
  --changenote TEXT         Workshop changenote. Defaults to the GitHub release tag.
  --preview-file PATH       Copy this image to About/Preview.png and set it in the VDF.
  --visibility VALUE        public, friends, private, unlisted, or numeric 0-3.
  --title TEXT              Update the Workshop title.
  --description-file PATH   Update the Workshop description from this file.
                            Defaults to SteamWorkshopDescription.txt from the
                            validated GitHub release.
  --steamcmd PATH           SteamCMD executable. May also be set with STEAMCMD.
  --repo OWNER/REPO         GitHub repository. Defaults to sayhiben/NoCavesContinued.
  --dry-run                 Download, stage, and print the VDF/command only.
  --non-interactive         Use cached Steam login, no prompts, and a 10 minute timeout.
                            SteamCMD output stays private and is removed on exit.
  --keep-workdir            Keep the temporary staging directory.
  -h, --help                Show this help.

Never pass a Steam password to this script. For local use, log in through
SteamCMD and approve Steam Guard when prompted. CI restores an authenticated
config/config.vdf and uses --non-interactive (see README).
EOF
}

die() {
  echo "error: $*" >&2
  exit 1
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || die "required command not found: $1"
}

resolve_steamcmd() {
  if [[ -n "$steamcmd" ]]; then
    echo "$steamcmd"
    return
  fi

  if command -v steamcmd >/dev/null 2>&1; then
    command -v steamcmd
    return
  fi

  if command -v steamcmd.sh >/dev/null 2>&1; then
    command -v steamcmd.sh
    return
  fi

  echo "steamcmd"
}

read_published_file_id() {
  if [[ -n "$published_file_id" ]]; then
    echo "$published_file_id"
    return
  fi

  if [[ -f About/PublishedFileId.txt ]]; then
    tr -cd '0-9' < About/PublishedFileId.txt
  elif [[ -f PublishedFileId.txt ]]; then
    tr -cd '0-9' < PublishedFileId.txt
  else
    echo ""
  fi
}

visibility_value() {
  case "$1" in
    public|0) echo "0" ;;
    friends|friends-only|1) echo "1" ;;
    private|hidden|2) echo "2" ;;
    unlisted|3) echo "3" ;;
    *) die "invalid visibility '$1'; use public, friends, private, unlisted, or 0-3" ;;
  esac
}

vdf_escape() {
  local value="$1"
  value=${value//\\/\\\\}
  value=${value//\"/\\\"}
  value=${value//$'\r'/}
  printf '%s' "$value"
}

write_vdf_entry() {
  local key="$1"
  local value="$2"
  # Keep real LF characters in multiline values. Steam Workshop renders
  # backslash-n sequences literally in item descriptions.
  printf '\t"%s" "%s"\n' "$key" "$(vdf_escape "$value")"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --zip)
      [[ $# -ge 2 ]] || die "--zip requires a value"
      release_zip="$2"
      shift 2
      ;;
    --published-file-id)
      [[ $# -ge 2 ]] || die "--published-file-id requires a value"
      published_file_id="$2"
      shift 2
      ;;
    --steam-user)
      [[ $# -ge 2 ]] || die "--steam-user requires a value"
      steam_user="$2"
      shift 2
      ;;
    --changenote)
      [[ $# -ge 2 ]] || die "--changenote requires a value"
      change_note="$2"
      shift 2
      ;;
    --preview-file)
      [[ $# -ge 2 ]] || die "--preview-file requires a value"
      preview_file="$2"
      shift 2
      ;;
    --visibility)
      [[ $# -ge 2 ]] || die "--visibility requires a value"
      visibility="$2"
      shift 2
      ;;
    --title)
      [[ $# -ge 2 ]] || die "--title requires a value"
      title="$2"
      shift 2
      ;;
    --description-file)
      [[ $# -ge 2 ]] || die "--description-file requires a value"
      description_file="$2"
      shift 2
      ;;
    --steamcmd)
      [[ $# -ge 2 ]] || die "--steamcmd requires a value"
      steamcmd="$2"
      shift 2
      ;;
    --repo)
      [[ $# -ge 2 ]] || die "--repo requires a value"
      repo="$2"
      shift 2
      ;;
    --dry-run)
      dry_run=1
      shift
      ;;
    --keep-workdir)
      keep_workdir=1
      shift
      ;;
    --non-interactive)
      non_interactive=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "unknown argument: $1"
      ;;
  esac
done

need_command unzip
need_command python3
if [[ -z "$release_zip" ]]; then
  need_command gh
else
  [[ -f "$release_zip" ]] || die "release zip not found: $release_zip"
fi
if [[ "$non_interactive" -eq 1 ]]; then
  need_command timeout
  [[ "$keep_workdir" -eq 0 ]] || die "--non-interactive cannot retain SteamCMD logs with --keep-workdir"
fi

published_file_id="$(read_published_file_id)"
[[ -n "$published_file_id" ]] || die "missing Workshop item ID; pass --published-file-id or set STEAM_PUBLISHED_FILE_ID"
[[ "$published_file_id" =~ ^[0-9]+$ ]] || die "published file ID must be numeric"
[[ "$published_file_id" != "0" ]] || die "this script updates an existing Workshop item only; do the first upload manually"

if [[ -z "$steam_user" && "$dry_run" -eq 0 ]]; then
  die "missing Steam username; pass --steam-user or set STEAM_USERNAME"
fi

if [[ -n "$description_file" && ! -f "$description_file" ]]; then
  die "description file not found: $description_file"
fi

if [[ -n "$preview_file" && ! -f "$preview_file" ]]; then
  die "preview file not found: $preview_file"
fi

steamcmd="$(resolve_steamcmd)"
if [[ "$dry_run" -eq 0 ]]; then
  if [[ "$steamcmd" == */* ]]; then
    [[ -x "$steamcmd" ]] || die "SteamCMD executable not found or not executable: $steamcmd"
  else
    command -v "$steamcmd" >/dev/null 2>&1 || die "SteamCMD not found; pass --steamcmd or set STEAMCMD"
  fi
fi

workdir="$(mktemp -d "${TMPDIR:-/tmp}/nocaves-workshop.XXXXXX")"
if [[ "$keep_workdir" -eq 0 ]]; then
  trap 'rm -rf "$workdir"' EXIT
else
  echo "Keeping workdir: $workdir"
fi

extract_dir="$workdir/extract"
mkdir -p "$extract_dir"

if [[ -z "$release_zip" ]]; then
  release_tag="$(gh release view --repo "$repo" --json tagName --jq .tagName)"
  [[ -n "$release_tag" ]] || die "could not determine latest GitHub release"
  download_dir="$workdir/download"
  mkdir -p "$download_dir"
  echo "Downloading latest GitHub release $release_tag from $repo..."
  gh release download "$release_tag" --repo "$repo" --pattern "$asset_pattern" --dir "$download_dir"

  assets=()
  while IFS= read -r -d '' asset; do
    assets+=("$asset")
  done < <(find "$download_dir" -maxdepth 1 -type f -name "$asset_pattern" -print0)

  if [[ "${#assets[@]}" -ne 1 ]]; then
    die "expected one release asset matching $asset_pattern, found ${#assets[@]}"
  fi
  release_zip="${assets[0]}"
  change_note="${change_note:-Update from GitHub release $release_tag.}"
else
  change_note="${change_note:-Update from $(basename "$release_zip").}"
fi

python3 "$(dirname "${BASH_SOURCE[0]}")/package_mod.py" --validate "$release_zip"
echo "Extracting $(basename "$release_zip")..."
unzip -q "$release_zip" -d "$extract_dir"

mod_root="$extract_dir/$mod_dir_name"
if [[ "$(tr -d '[:space:]' < "$mod_root/About/PublishedFileId.txt")" != "$published_file_id" ]]; then
  printf '%s\n' "$published_file_id" > "$mod_root/About/PublishedFileId.txt"
fi

if [[ -n "$preview_file" ]]; then
  cp "$preview_file" "$mod_root/About/Preview.png"
fi

if [[ -z "$description_file" ]]; then
  description_file="$mod_root/SteamWorkshopDescription.txt"
fi
description="$(cat "$description_file")"

vdf_path="$workdir/workshop.vdf"
{
  echo '"workshopitem"'
  echo '{'
  write_vdf_entry "appid" "$app_id"
  write_vdf_entry "publishedfileid" "$published_file_id"
  write_vdf_entry "contentfolder" "$mod_root"
  write_vdf_entry "previewfile" "$mod_root/About/Preview.png"
  [[ -n "$visibility" ]] && write_vdf_entry "visibility" "$(visibility_value "$visibility")"
  [[ -n "$title" ]] && write_vdf_entry "title" "$title"
  [[ -n "$description" ]] && write_vdf_entry "description" "$description"
  write_vdf_entry "changenote" "$change_note"
  echo '}'
} > "$vdf_path"

echo "Staged Workshop content: $mod_root"
echo "Generated SteamCMD VDF: $vdf_path"

if [[ "$dry_run" -eq 1 ]]; then
  echo
  echo "Dry run. VDF contents:"
  cat "$vdf_path"
  echo
  echo "SteamCMD command:"
  if [[ -n "$steam_user" ]]; then
    printf '%q +login %q +workshop_build_item %q +quit\n' "$steamcmd" "$steam_user" "$vdf_path"
  else
    printf '%q +login %s +workshop_build_item %q +quit\n' "$steamcmd" "<steam-user>" "$vdf_path"
  fi
  exit 0
fi

echo "Publishing to Steam Workshop item $published_file_id..."
steam_args=(+login "$steam_user" +workshop_build_item "$vdf_path" +quit)
steam_log="$workdir/steamcmd.log"
if [[ "$non_interactive" -eq 1 ]]; then
  if ! timeout --kill-after=30s 10m "$steamcmd" \
    +@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 "${steam_args[@]}" \
    </dev/null >"$steam_log" 2>&1; then
    die "SteamCMD failed or timed out. Check Steam availability and renew STEAM_CONFIG_VDF with an interactive local login if needed. Private logs were discarded."
  fi
else
  if ! "$steamcmd" "${steam_args[@]}" 2>&1 | tee "$steam_log"; then
    die "SteamCMD failed"
  fi
fi
# SteamCMD can exit successfully without publishing; require the Workshop commit message.
if ! python3 - "$steam_log" <<'PY'
from pathlib import Path
import re
import sys

output = Path(sys.argv[1]).read_bytes()
sys.exit(0 if re.search(rb"Committing update\.\.\.[\r\n\t ]*Success\.", output) else 1)
PY
then
  die "SteamCMD did not confirm the Workshop update. Check item permissions and renew cached login if needed."
fi
echo "Steam Workshop update confirmed for item $published_file_id."
