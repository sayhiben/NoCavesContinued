"""Exercise release staging and unattended deployment with fake Steam/GitHub tools."""
import base64
import importlib.util
import io
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("package_mod", ROOT / "scripts/package_mod.py")
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)
BASH = (str(Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Git/bin/bash.exe")
        if os.name == "nt" else shutil.which("bash"))


def shell_path(path):
    path = Path(path).as_posix()
    return "/" + path[0].lower() + path[2:] if os.name == "nt" else path

STEAM_STUB = """#!/usr/bin/env bash
set -eu
printf '%s\\0' "$@" > "$MOCK_CAPTURE/args"
while [[ $# -gt 0 ]]; do
  if [[ "$1" == '+workshop_build_item' ]]; then
    cp "$2" "$MOCK_CAPTURE/workshop.vdf"
    content="$(sed -n 's/^[[:space:]]*"contentfolder" "\\(.*\\)"$/\\1/p' "$2")"
    cp -R "$content" "$MOCK_CAPTURE/content"
    break
  fi
  shift
done
if IFS= read -r line; then echo input; else echo eof; fi > "$MOCK_CAPTURE/stdin"
printf '%s' "$HOME" > "$MOCK_CAPTURE/home"
if [[ -f "$HOME/Steam/config/config.vdf" ]]; then
  cp "$HOME/Steam/config/config.vdf" "$MOCK_CAPTURE/config_seen"
fi
if [[ -n "${STEAM_CONFIG_VDF:-}" ]]; then touch "$MOCK_CAPTURE/state_in_environment"; fi
printf '%s\\n' "${MOCK_OUTPUT:-Committing update...Success.}"
exit "${MOCK_EXIT:-0}"
"""


@unittest.skipUnless(BASH and Path(BASH).exists(), "Bash is required for publishing tests")
class WorkshopTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="nocaves test ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / "bin"
        self.capture = self.root / "capture"
        self.scratch = self.root / "scratch"
        for path in (self.bin, self.capture, self.scratch):
            path.mkdir()
        self.env = os.environ.copy()
        for key in ("STEAM_USERNAME", "STEAM_PUBLISHED_FILE_ID", "STEAMCMD", "STEAM_CHANGE_NOTE", "STEAM_CONFIG_VDF"):
            self.env.pop(key, None)
        self.env.update({
            "PATH": str(self.bin) + os.pathsep + self.env["PATH"],
            "TMPDIR": shell_path(self.scratch),
            "RUNNER_TEMP": shell_path(self.scratch),
            "MOCK_CAPTURE": shell_path(self.capture),
            "STEAM_USERNAME": "test_user",
        })
        startup = self.root / "mock-path.sh"
        startup.write_text(f'export PATH="{shell_path(self.bin)}:$PATH"\n', encoding="utf-8", newline="\n")
        self.env["BASH_ENV"] = startup.as_posix()
        # Git Bash must use the real interpreter rather than a Windows Store alias.
        if os.name == "nt":
            self.write_command("python3", f'#!/usr/bin/env bash\nexec "{Path(sys.executable).as_posix()}" "$@"\n')
        self.write_command("steamcmd", STEAM_STUB)
        self.write_command("gh", """#!/usr/bin/env bash
set -eu
echo "$*" >> "$MOCK_CAPTURE/github_calls"
case "$2" in
  view) echo build-test ;;
  download)
    while [[ "$1" != --dir ]]; do shift; done
    cp "$MOCK_RELEASE_ZIP" "$2/NoCavesContinued-test.zip"
    ;;
  *) exit 98 ;;
esac
""")
        description = self.root / "description.txt"
        description.write_text('[h1]No Caves[/h1]\nSecond "quoted" line.\n', encoding="utf-8")
        self.zip = self.root / "exact release.zip"
        pack.package(ROOT, description, self.zip)
        self.env["MOCK_RELEASE_ZIP"] = self.zip.as_posix()

    def write_command(self, name, content):
        path = self.bin / name
        path.write_text(content, encoding="utf-8", newline="\n")
        path.chmod(0o755)
        return path

    def run_script(self, script, *args, input="input must not reach SteamCMD\n"):
        return subprocess.run([BASH, str(ROOT / "scripts" / script), *map(str, args)],
                              env=self.env, cwd=ROOT, text=True, input=input,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=30)

    def publish(self, *args):
        return self.run_script("publish-steam-workshop.sh", "--zip", self.zip.as_posix(),
                               "--published-file-id", "3735336239", "--non-interactive", *args)

    def assert_scratch_removed(self):
        self.assertEqual(list(self.scratch.iterdir()), [])

    def test_exact_zip_upload_and_private_output(self):
        self.env["MOCK_OUTPUT"] = "private login detail\nCommitting update...\r\nSuccess."
        self.env["STEAM_USERNAME"] = "one quoted argument"
        result = self.publish("--changenote", 'Two lines\nwith "quotes" and $literal.')
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertNotIn("private login detail", result.stdout)
        self.assertFalse((self.capture / "github_calls").exists())
        args = (self.capture / "args").read_bytes().decode().split("\0")[:-1]
        self.assertEqual(args[args.index("+login") + 1], "one quoted argument")
        self.assertIn("+@NoPromptForPassword", args)
        self.assertEqual((self.capture / "stdin").read_text().strip(), "eof")
        vdf = (self.capture / "workshop.vdf").read_text()
        self.assertIn('"publishedfileid" "3735336239"', vdf)
        self.assertNotIn('"visibility"', vdf)
        self.assertNotIn('"title"', vdf)
        self.assertIn('Two lines\nwith \\"quotes\\" and $literal.', vdf)
        self.assertIn('[h1]No Caves[/h1]\nSecond \\"quoted\\" line.', vdf)
        with ZipFile(self.zip) as archive:
            for name in pack.FILES:
                self.assertEqual((self.capture / "content" / name).read_bytes(), archive.read(f"{pack.MOD}/{name}"))
        self.assert_scratch_removed()

    def test_dry_run_does_not_call_steam_or_github(self):
        result = self.publish("--dry-run")
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("Dry run.", result.stdout)
        self.assertFalse((self.capture / "args").exists())
        self.assertFalse((self.capture / "github_calls").exists())
        self.assert_scratch_removed()

    def test_failed_or_unconfirmed_upload_is_not_success(self):
        for exit_code, output in ((1, "Committing update...Success."), (0, "Login OK"),
                                  (0, "Success. Uploaded item 12345 to Workshop."),
                                  (0, "Committing update...ERROR! Access denied")):
            with self.subTest(exit_code=exit_code, output=output):
                self.env.update(MOCK_EXIT=str(exit_code), MOCK_OUTPUT=output)
                result = self.publish()
                self.assertNotEqual(result.returncode, 0)
                self.assertNotIn("update confirmed", result.stdout)
                self.assert_scratch_removed()

    def test_timeout_is_not_success(self):
        self.write_command("timeout", "#!/usr/bin/env bash\nexit 124\n")
        result = self.publish()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("failed or timed out", result.stdout)
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def test_invalid_payload_rejected_before_steam(self):
        with ZipFile(self.zip, "a") as archive:
            archive.writestr("NoCavesContinued/unexpected.txt", b"extra")
        result = self.publish()
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def test_unattended_logs_cannot_be_retained(self):
        result = self.publish("--keep-workdir")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def test_creation_of_new_item_is_refused(self):
        result = self.publish("--published-file-id", "0")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def test_default_latest_release_still_works(self):
        result = self.run_script("publish-steam-workshop.sh", "--published-file-id", "3735336239", "--dry-run")
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("Update from GitHub release build-test.", result.stdout)
        self.assertTrue((self.capture / "github_calls").exists())
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def setup_fake_download(self):
        tarball = self.root / "steamcmd.tar.gz"
        with tarfile.open(tarball, "w:gz") as archive:
            contents = STEAM_STUB.encode()
            entry = tarfile.TarInfo("steamcmd.sh")
            entry.size = len(contents)
            entry.mode = 0o755
            archive.addfile(entry, io.BytesIO(contents))
        self.env["MOCK_TARBALL"] = shell_path(tarball)
        self.write_command("curl", """#!/usr/bin/env bash
set -eu
touch "$MOCK_CAPTURE/download_called"
while [[ "$1" != --output ]]; do shift; done
cp "$MOCK_TARBALL" "$2"
""")

    def test_hosted_deployment_restores_and_deletes_private_login(self):
        self.setup_fake_download()
        state = b'"InstallConfigStore" { "test_login_token" "private fake token" }'
        self.env["STEAM_CONFIG_VDF"] = base64.b64encode(state).decode()
        self.env["MOCK_OUTPUT"] = "private fake token\nCommitting update...Success."
        result = self.run_script("deploy-steam-workshop.sh", self.zip.as_posix(), "CI test")
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertEqual((self.capture / "config_seen").read_bytes(), state)
        self.assertFalse((self.capture / "state_in_environment").exists())
        self.assertNotIn("private fake token", result.stdout)
        self.assertNotIn(self.env["STEAM_CONFIG_VDF"], result.stdout)
        self.assertFalse((self.capture / "github_calls").exists())
        self.assertEqual((self.capture / "stdin").read_text().strip(), "eof")
        self.assert_scratch_removed()

    def test_invalid_login_state_fails_before_download(self):
        self.setup_fake_download()
        self.env["STEAM_CONFIG_VDF"] = "not-base64!"
        result = self.run_script("deploy-steam-workshop.sh", self.zip.as_posix(), "CI test")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.capture / "download_called").exists())
        self.assertFalse((self.capture / "args").exists())
        self.assert_scratch_removed()

    def test_missing_secrets_fail_before_download(self):
        self.setup_fake_download()
        result = self.run_script("deploy-steam-workshop.sh", self.zip.as_posix(), "CI test")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Missing steam-workshop environment secrets", result.stdout)
        self.assertFalse((self.capture / "download_called").exists())
        self.assert_scratch_removed()


if __name__ == "__main__":
    unittest.main()
