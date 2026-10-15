#!/usr/bin/env python3
"""Build or validate the exact installable payload, without installing or publishing."""
from __future__ import annotations

import argparse
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import xml.etree.ElementTree as ET

MOD = "NoCavesContinued"
VERSIONS = ("1.6", "1.7")
FILES = {
    "About/About.xml", "About/Preview.png", "About/PublishedFileId.txt",
    "LoadFolders.xml", "README.md", "SteamWorkshopDescription.txt",
    *(f"{version}/Assemblies/{MOD}.{extension}" for version in VERSIONS for extension in ("dll", "pdb")),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def validate(path: Path) -> None:
    with ZipFile(path) as archive:
        names = archive.namelist()
        expected = {f"{MOD}/{name}" for name in FILES}
        require(len(names) == len(set(names)), "Duplicate ZIP entries")
        # An exact allowlist also rejects traversal, runtime dependencies, and test/handoff files.
        require(set(names) == expected,
                f"Unexpected payload: missing={sorted(expected - set(names))}, extra={sorted(set(names) - expected)}")
        require(archive.testzip() is None, "ZIP CRC failure")
        data = {name: archive.read(f"{MOD}/{name}") for name in FILES}
        require(all(data.values()), "Empty payload file")
        about = ET.fromstring(data["About/About.xml"])
        require(about.tag == "ModMetaData", "Invalid mod metadata root")
        require(about.findtext("packageId") == "sayhiben.nocavescontinued", "Package identity changed")
        require(about.findtext("name") == "No Caves - Continued" and about.findtext("author") == "sayhiben", "Name/author changed")
        require([entry.text for entry in about.findall("supportedVersions/li")] == list(VERSIONS), "Unsupported version metadata")
        require(about.findtext("modDependencies/li/packageId") == "brrainz.harmony" and
                about.findtext("loadAfter/li") == "brrainz.harmony", "Harmony dependency/load order missing")
        require("kd8lvt" in about.findtext("description", "") and b"kd8lvt" in data["README.md"], "Original credit missing")
        routing = ET.fromstring(data["LoadFolders.xml"])
        require(routing.tag == "loadFolders", "Invalid load routing root")
        require([entry.tag for entry in routing] == [f"v{version}" for version in VERSIONS], "Unexpected load routing")
        for version in VERSIONS:
            folders = [(entry.tag, entry.text) for entry in routing.find(f"v{version}")]
            # Together with the exact file allowlist, these roots load one matching DLL.
            require(folders == [("li", "/"), ("li", version)], f"Incorrect load roots for {version}")
            require(data[f"{version}/Assemblies/{MOD}.dll"].startswith(b"MZ"), f"Invalid DLL for {version}")
        require(data["About/PublishedFileId.txt"].strip() == b"3735336239", "Workshop identity changed")
        require(data["About/Preview.png"].startswith(b"\x89PNG\r\n\x1a\n"), "Preview is not a PNG")


def package(root: Path, description: Path, output: Path) -> None:
    inputs = {name: (root / name) for name in FILES}
    inputs["About/PublishedFileId.txt"] = root / "PublishedFileId.txt"
    inputs["SteamWorkshopDescription.txt"] = description
    # Read everything before writing so a missing build cannot leave a half-written archive.
    data = {name: source.read_bytes() for name, source in inputs.items()}
    output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(output, "w", ZIP_DEFLATED) as archive:
        for name, content in sorted(data.items()):
            archive.writestr(f"{MOD}/{name}", content)
    validate(output)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--validate", type=Path, help="Inspect an existing ZIP only.")
    parser.add_argument("--description", type=Path, help="Generated Steam BBCode.")
    parser.add_argument("--output", type=Path, help="Candidate ZIP to write.")
    args = parser.parse_args()
    if args.validate:
        if args.description or args.output:
            parser.error("--validate cannot be combined with packaging options")
        validate(args.validate)
        print(f"PASS: validated {args.validate}")
    else:
        if not args.description or not args.output:
            parser.error("packaging requires --description and --output")
        package(Path(__file__).resolve().parents[1], args.description, args.output)
        print(f"PASS: packaged and validated {args.output}")


if __name__ == "__main__":
    main()
