"""Mutate a real built package to exercise payload and version-loading rejection."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("package_mod", ROOT / "scripts/package_mod.py")
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.output = Path(self.temp.name) / "mod.zip"
        description = Path(self.temp.name) / "description.txt"
        description.write_text("[h1]No Caves - Continued[/h1]", encoding="utf-8")
        pack.package(ROOT, description, self.output)
        with ZipFile(self.output) as archive:
            self.contents = {name: archive.read(name) for name in archive.namelist()}

    def assert_rejected(self):
        with ZipFile(self.output, "w") as archive:
            for name, data in self.contents.items():
                archive.writestr(name, data)
        with self.assertRaises(ValueError):
            pack.validate(self.output)

    def test_complete_package(self):
        pack.validate(self.output)
        self.assertEqual(len(self.contents), 10)
        for version in pack.VERSIONS:
            relative = f"{version}/Assemblies/NoCavesContinued.dll"
            self.assertEqual(self.contents[f"{pack.MOD}/{relative}"], (ROOT / relative).read_bytes())

    def test_missing_new_version(self):
        del self.contents[f"{pack.MOD}/1.7/Assemblies/NoCavesContinued.dll"]
        self.assert_rejected()

    def test_cross_version_load(self):
        key = f"{pack.MOD}/LoadFolders.xml"
        self.contents[key] = self.contents[key].replace(b"<li>1.7</li>", b"<li>1.6</li><li>1.7</li>")
        self.assert_rejected()

    def test_duplicate_load(self):
        key = f"{pack.MOD}/LoadFolders.xml"
        self.contents[key] = self.contents[key].replace(b"<li>1.7</li>", b"<li>1.7</li><li>1.7</li>")
        self.assert_rejected()

    def test_wrong_xml_roots(self):
        for path, tag in (("About/About.xml", b"ModMetaData"), ("LoadFolders.xml", b"loadFolders")):
            with self.subTest(path=path):
                key = f"{pack.MOD}/{path}"
                original = self.contents[key]
                self.contents[key] = original.replace(tag, b"incorrectRoot")
                self.assert_rejected()
                self.contents[key] = original

    def test_wrong_load_entry_tag(self):
        key = f"{pack.MOD}/LoadFolders.xml"
        self.contents[key] = self.contents[key].replace(b"<li>1.7</li>", b"<folder>1.7</folder>")
        self.assert_rejected()

    def test_dependencies_and_development_files(self):
        for path in ("1.7/Assemblies/0Harmony.dll", "1.6/Assemblies/Assembly-CSharp.dll",
                     "1.7/Assemblies/UnityEngine.dll", "Assemblies/NoCavesRedux.dll",
                     "Tests/GameDoubles.cs", "codex-handoff/STATUS.md", ".git/config", "../escape"):
            with self.subTest(path=path):
                key = f"{pack.MOD}/{path}"
                self.contents[key] = b"unwanted"
                self.assert_rejected()
                del self.contents[key]

    def test_wrong_workshop_id(self):
        self.contents[f"{pack.MOD}/About/PublishedFileId.txt"] = b"2898003040"
        self.assert_rejected()

    def test_wrong_identity(self):
        key = f"{pack.MOD}/About/About.xml"
        self.contents[key] = self.contents[key].replace(b"sayhiben.nocavescontinued", b"sayhiben.nocavesredux")
        self.assert_rejected()

    def test_duplicate_zip_entry(self):
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with ZipFile(self.output, "a") as archive:
                archive.writestr(f"{pack.MOD}/README.md", b"duplicate")
        with self.assertRaises(ValueError):
            pack.validate(self.output)


if __name__ == "__main__":
    unittest.main()
