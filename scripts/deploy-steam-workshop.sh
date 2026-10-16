#!/usr/bin/env bash
# Hosted-runner entry point: isolate cached Steam login and discard it after use.
set -euo pipefail
set +x
umask 077

[[ $# -eq 2 ]] || { echo "usage: $0 RELEASE_ZIP CHANGE_NOTE" >&2; exit 1; }
[[ -n "${STEAM_USERNAME:-}" && -n "${STEAM_CONFIG_VDF:-}" ]] || {
  echo "Missing steam-workshop environment secrets: STEAM_USERNAME and STEAM_CONFIG_VDF." >&2
  exit 1
}

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
release_zip="$(realpath -e -- "$1")"
change_note="$2"
deploy_dir="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/nocaves-steam.XXXXXX")"
trap 'rm -rf -- "$deploy_dir"' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
steam_dir="$deploy_dir/home/Steam"
mkdir -p "$steam_dir/config" "$deploy_dir/tmp"

# Valve's CI guidance uses config/config.vdf and username-only login. Passing a
# password would request a new Steam Guard token instead of using cached login.
printf '%s' "$STEAM_CONFIG_VDF" | base64 --decode > "$steam_dir/config/config.vdf"
unset STEAM_CONFIG_VDF
[[ -s "$steam_dir/config/config.vdf" ]] || { echo "STEAM_CONFIG_VDF decoded to an empty file." >&2; exit 1; }

curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 \
  --connect-timeout 30 --max-time 180 \
  https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
  --output "$deploy_dir/steamcmd.tar.gz"
tar -xzf "$deploy_dir/steamcmd.tar.gz" -C "$steam_dir"

# HOME and TMPDIR apply only to this process tree; SteamCMD state/logs stay out
# of the checkout, Actions artifacts, and caches.
env HOME="$deploy_dir/home" TMPDIR="$deploy_dir/tmp" \
  bash "$script_dir/publish-steam-workshop.sh" \
  --zip "$release_zip" \
  --published-file-id 3735336239 \
  --steamcmd "$steam_dir/steamcmd.sh" \
  --changenote "$change_note" \
  --non-interactive
