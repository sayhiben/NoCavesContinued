#!/usr/bin/env bash
set -euo pipefail

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
dry_run=0
keep_workdir=0

usage() {
  cat <<'EOF'
Usage:
  scripts/publish-steam-workshop.sh --published-file-id ID --steam-user USER --changenote TEXT [options]

Downloads the latest GitHub release zip, stages the installable RimWorld mod
folder, writes a SteamCMD workshop VDF, then optionally runs SteamCMD.

Required for real publish:
  --published-file-id ID    Existing Steam Workshop item ID.
                            May also be set with STEAM_PUBLISHED_FILE_ID or
                            About/PublishedFileId.txt.
  --steam-user USER         Steam username. May also be set with STEAM_USERNAME.
                            SteamCMD will prompt or use its local trusted login.

Common options:
  --changenote TEXT         Workshop changenote. Defaults to the GitHub release tag.
  --preview-file PATH       Copy this image to About/Preview.png and set it in the VDF.
  --visibility VALUE        public, friends, private, unlisted, or numeric 0-3.
  --title TEXT              Update the Workshop title.
  --description-file PATH   Update the Workshop description from this file.
                            Defaults to SteamWorkshopDescription.txt from the
                            latest GitHub release when present.
  --steamcmd PATH           SteamCMD executable. May also be set with STEAMCMD.
  --repo OWNER/REPO         GitHub repository. Defaults to sayhiben/NoCavesContinued.
  --dry-run                 Download, stage, and print the VDF/command only.
  --keep-workdir            Keep the temporary staging directory.
  -h, --help                Show this help.

Authentication is intentionally local. Do not pass a Steam password to this
script; log in through SteamCMD and approve Steam Guard when prompted.
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
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "unknown argument: $1"
      ;;
  esac
done

need_command gh
need_command unzip

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

release_tag="$(gh release view --repo "$repo" --json tagName --jq .tagName)"
[[ -n "$release_tag" ]] || die "could not determine latest GitHub release"

if [[ -z "$change_note" ]]; then
  change_note="Update from GitHub release $release_tag."
fi

workdir="$(mktemp -d "${TMPDIR:-/tmp}/nocaves-workshop.XXXXXX")"
if [[ "$keep_workdir" -eq 0 ]]; then
  trap 'rm -rf "$workdir"' EXIT
else
  echo "Keeping workdir: $workdir"
fi

download_dir="$workdir/download"
extract_dir="$workdir/extract"
mkdir -p "$download_dir" "$extract_dir"

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
echo "Extracting $(basename "$release_zip")..."
unzip -q "$release_zip" -d "$extract_dir"

mod_root="$extract_dir/$mod_dir_name"
[[ -d "$mod_root" ]] || die "release zip did not contain $mod_dir_name/"

required_paths=(
  "$mod_root/About/About.xml"
  "$mod_root/LoadFolders.xml"
  "$mod_root/1.6/Assemblies/NoCavesContinued.dll"
)

for path in "${required_paths[@]}"; do
  [[ -e "$path" ]] || die "release content is missing required file: ${path#$mod_root/}"
done

printf '%s\n' "$published_file_id" > "$mod_root/About/PublishedFileId.txt"

preview_for_vdf=""
if [[ -n "$preview_file" ]]; then
  cp "$preview_file" "$mod_root/About/Preview.png"
  preview_for_vdf="$mod_root/About/Preview.png"
elif [[ -f "$mod_root/About/Preview.png" ]]; then
  preview_for_vdf="$mod_root/About/Preview.png"
else
  echo "warning: no Preview.png found; the VDF will not update the Workshop preview image" >&2
fi

if [[ -z "$description_file" && -f "$mod_root/SteamWorkshopDescription.txt" ]]; then
  description_file="$mod_root/SteamWorkshopDescription.txt"
fi

description=""
if [[ -n "$description_file" ]]; then
  description="$(cat "$description_file")"
fi

vdf_path="$workdir/workshop.vdf"
{
  echo '"workshopitem"'
  echo '{'
  write_vdf_entry "appid" "$app_id"
  write_vdf_entry "publishedfileid" "$published_file_id"
  write_vdf_entry "contentfolder" "$mod_root"
  [[ -n "$preview_for_vdf" ]] && write_vdf_entry "previewfile" "$preview_for_vdf"
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
"$steamcmd" +login "$steam_user" +workshop_build_item "$vdf_path" +quit
echo "SteamCMD completed."
