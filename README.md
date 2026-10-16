# No Caves - Continued

**No Caves - Continued** prevents RimWorld's vanilla cave tunnels from being carved into newly generated maps and makes its vanilla cave-map query return false. Use it for mountainous or impassable starts without the normal cave networks. There are no settings.

## Links

- [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3735336239)
- [GitHub](https://github.com/sayhiben/NoCavesContinued)
- [Original NoCaves by kd8lvt](https://steamcommunity.com/sharedfiles/filedetails/?id=2898003040)

## Requirements

- RimWorld 1.6 with Harmony 2, or RimWorld 1.7 with Harmony 3.
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) loaded before this mod.

The 1.7 payload is an alpha candidate. Automated checks and controlled in-game cave-suppression tests pass on 1.6 and 1.7. Broader compatibility checks remain pending; this candidate has not been published to the Workshop.

## Steam Workshop Users

Subscribe, enable the mod, and load Harmony before Core/official DLC and No Caves - Continued.

## Safe To Add Or Remove

Enabling or removing the mod does not alter existing terrain. Only newly generated maps are affected; removal restores normal vanilla cave generation for future maps.

## Compatibility

The mod patches the vanilla cave utility and cave-map query. It leaves independent generators and existing cave grids alone. Mods that use vanilla cave data can still be affected, and other patches can override the query result. The shared utility also serves asteroid generation: controlled tests on 1.6 and 1.7 found that this mod fills some asteroid passages with rock or ore.

[Geological Landforms](https://github.com/m00nl1ght-dev/GeologicalLandforms) Surface Cave and all four directional Cave Entrances retained visible cave networks and matching natural roofs in controlled comparisons on 1.6 and 1.7. Floors and some objects differed. A representative Surface Cave pawn walk passed on each version, and selected outdoor-to-interior reachability checks passed for all four 1.7 candidate entrances. This is limited coverage, not a guarantee of every passage or unchanged cave shape. The tested GL mod version, 1.7.13.1, advertises RimWorld support only through 1.6; its use on 1.7 remains experimental.

## In-Game Check

Generate a new map on a tile known to contain caves and inspect the terrain. The game log should contain `Registered SkipCaveGeneration` and `Registered ReportNoCaves`. First use logs `SkipCaveGeneration executed` or `ReportNoCaves executed` once per session. Registration alone does not verify terrain; a missing execution message can mean that map never called the target.

## Credits And Continuation

This continuation is inspired by **NoCaves**, created and maintained by **kd8lvt**. Credit for the original idea, player-facing design, and stewardship belongs to kd8lvt, who stepped away from active development and supported others carrying the work forward.

## AI Assistance

OpenAI Codex was used to write and revise code, tests, documentation, and deployment automation, and to operate the game for controlled tests during this continuation's RimWorld 1.7 uplift. Completed checks and remaining gameplay validation are described above and in the development documentation.

## Manual Install

Extract the complete `NoCavesContinued` folder from a release ZIP into RimWorld's `Mods` directory. Keep only one copy enabled and retain both version folders; RimWorld selects the matching payload through `LoadFolders.xml`.

## Development Prerequisites

Use .NET SDK 10, Python 3.10+, Git, and access to NuGet. The repository's `NuGet.Config` supplies the public feed. Builds use reference assemblies and require no game installation or Windows host. The `net472` smoke executable needs .NET Framework on Windows or a suitable Mono runtime; CI uses Windows for that suite.

Install the description converter in a Python environment:

```bash
python3 -m pip install md2steam==1.0.1
```

## Build

From the repository root:

```bash
dotnet build Source/NoCavesContinued.csproj -c Release
```

One source implementation produces `NoCavesContinued.dll` and `.pdb` under `1.6/Assemblies/` (`net472`) and `1.7/Assemblies/` (`net10.0`). Dependencies are compile-only; RimWorld and the installed Harmony mod provide their runtimes. The project file owns dependency pins; [the migration checkpoint](MIGRATION_STATUS.md) records the versions actually verified.

## Development Test

After building:

```bash
dotnet run --project Tests/Smoke -c Release -f net472
dotnet run --project Tests/Smoke -c Release -f net10.0
dotnet run --project Tests/Contracts -c Release
python3 -m unittest discover -s Tests -p 'test_*.py' -v
```

Smoke tests use real Harmony with game doubles. Contract checks inspect reference signatures, framework/Harmony isolation, and agreement between compile and smoke-test dependencies. Append `-- <path-to-Assembly-CSharp.dll>` to the contract command to inspect an installed game and its direct callers without executing it. Neither suite replaces in-game tests; the checkpoint contains the controlled map-comparison procedure and pending results.

Create a local package:

```bash
python3 scripts/generate-steam-workshop-description.py README.md artifacts/SteamWorkshopDescription.txt
python3 scripts/package_mod.py --description artifacts/SteamWorkshopDescription.txt --output artifacts/NoCavesContinued-candidate.zip
```

Packaging validates its output automatically. To inspect an existing ZIP, use `python3 scripts/package_mod.py --validate <zip>`. That script owns the exact payload allowlist and load-routing checks. It excludes sources, tests, handoff files, and game/Harmony/Unity dependencies.

The README is the source for the Steam description. Keep developer material under the excluded headings listed in `scripts/generate-steam-workshop-description.py`; use `--include-dev-sections` only for an intentional full conversion.

## Steam Workshop Publishing

The existing Workshop item is **3735336239**. Packaging copies root `PublishedFileId.txt` to `About/PublishedFileId.txt` and retains the original artwork. Uploads update this item; title and visibility remain unchanged unless explicitly overridden.

Pull requests and manual CI runs build and test candidates. Main pushes also create a GitHub release. Automated Workshop uploads use the same validated artifact and require the hosted-runner setup in [the deployment guide](docs/STEAM_DEPLOYMENT.md).

For a local preview using the latest GitHub release:

```bash
STEAM_USERNAME=your_steam_username \
bash scripts/publish-steam-workshop.sh --dry-run --changenote "Update from tested release"
```

This downloads and stages content but does not upload. The helper's `--help` lists options; use `--zip <path>` to stage a specific package. Local publishing needs Python 3, Bash, `unzip`, SteamCMD, and `gh` when downloading a release. Remove `--dry-run` only when ready to publish. Use the package script for build checks.
