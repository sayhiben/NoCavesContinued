# RimWorld 1.7 migration checkpoint

Updated October 15, 2026 (America/Los_Angeles). Controlled cave-suppression tests pass on 1.6 and 1.7; the 1.7 alpha candidate still needs broader compatibility checks.

## Scope and repository state

The starting folder contained a task brief and handoff ZIP, not a checkout. Handoff integrity checks passed for all 23 files, six baseline Git blobs, and both embedded ZIPs. The maintained repository was fetched at `612630d16799ef326d25f93e55c8b58cf111b294`; the original handoff remains unchanged and excluded from the PR.

Work is on `codex/rimworld-1.7-uplift`. The user authorized a full GitHub PR, automated Workshop deployment on a hosted runner, and computer-use gameplay testing. The candidate was temporarily staged locally for isolated testing, with the installed mod backed up first. No merge, live upload, or listing visibility change has been performed. Existing identity, Workshop item `3735336239`, both patches, kd8lvt credit, and artwork remain intact.

## Design decisions

- **SOLID:** startup and the two prefix behaviors stay separate from the installer, which owns signature checks, registration, rollback, and diagnostics. Packaging owns its file contract; the publisher consumes it. With no interchangeable implementations or inheritance, interfaces and dependency containers would add machinery without solving a current problem.
- **DRY:** one shared implementation serves both builds; one ZIP validator serves CI and publishing. README owns usage, AGENTS owns durable rules, and the deployment guide owns authentication setup. Independent signature expectations remain in tests so accidental runtime edits cannot silently redefine their own acceptance criteria.
- **KISS:** two explicit patch targets, two conditional build groups, and the existing md2steam converter. Type lookup occurs inside the installation error boundary. Tests use named scenarios and verify compile/runtime Harmony pins agree.
- **YAGNI:** removed the production test-injection delegate, duplicate required-file checks, unused outputs/imports/defaults, and repeated documentation. No configurable patch registry, version adapters, generic compatibility layer, forced priorities, or GL patches were added.

Changed areas: runtime/project and both DLL/PDB pairs; About/version routing; NuGet config; smoke/contracts/package/publishing tests; packaging, publishing, and deployment scripts; release workflow; README, AGENTS, deployment guide, and this checkpoint. `CLAUDE.md` continues delegating to AGENTS.

## Verified versions and contracts

| Payload | Framework | RimWorld reference | Harmony reference and smoke runtime |
|---|---|---|---|
| 1.6 | net472 | Krafs.Rimworld.Ref 1.6.4633 | 2.4.2 |
| 1.7 | net10.0 | Krafs.Rimworld.Ref 1.7.4977-alpha.2 | 3.0.0-rc.2 |

Local tools: .NET SDK 10.0.103; smoke hosts .NET Framework 4.8.9363.0 and .NET 10.0.3; Mono.Cecil 0.11.6 for metadata; ILSpyCmd 11.1.0.9782 for local inspection; md2steam 1.0.1 for the description. The project file is authoritative for future pins. [Harmony's build guidance](https://github.com/pardeike/HarmonyRimWorld/blob/master/BUILDING.md) supports separate framework payloads; the reference and Harmony packages were restored successfully from NuGet.

Both reference versions and installed RimWorld **1.6.4871 rev590** and **1.7.4980 rev60** have exactly one declared overload of each target:

```text
static void RimWorld.MapGenCavesUtility.GenerateCaves(
    Verse.Map, Verse.BoolGrid, List<Verse.IntVec3>, Verse.Noise.ModuleBase,
    RimWorld.MapGenCavesUtility.CaveGenParms, Predicate<Verse.IntVec3>)
instance bool RimWorld.Planet.World.HasCaves(RimWorld.Planet.PlanetTile)
```

The installed utilities in both versions create open/closed tunnels from rock groups. Their direct callers include `TileMutatorWorker_Caves.GenerateCaves` and `GenStep_Asteroid.GenerateCaveElevation`. `World.HasCaves` checks mutator `IsCave`; direct callers include the debug cave-map selector, ancient-turret generation, and cave-debris generation. Method-body inspection and metadata checks establish broader-than-colony scope, not an exhaustive dynamic call graph. The inspected 1.7 game assembly SHA-256 is `eff7d97949f919fb2b151ebfb906e9be749b63410b7dbda78eeebb7972b245c3`.

In-game tests used installed Harmony **2.4.3.0** for 1.6 and **3.0.1.0** for 1.7; smoke tests use the pinned NuGet versions above. Installed Geological Landforms reports **mod version 1.7.13.1**, but advertises game support only through **RimWorld 1.6**. Its cave input reads `MapGenerator.Caves`, so independent landforms can still consume vanilla cave data.

## Verification evidence

The original baseline build first failed with NU1101 because the machine had only offline feeds. Restoring from NuGet fixed it before source edits. Repository `NuGet.Config` now supplies that feed without changing machine settings.

Current local results:

| Check | Result |
|---|---|
| `dotnet build Source/NoCavesContinued.csproj -c Release` | PASS: both frameworks, zero warnings/errors |
| `dotnet run --project Tests/Smoke -c Release -f net472` | PASS: 44 assertions |
| `dotnet run --project Tests/Smoke -c Release -f net10.0` | PASS: 44 assertions |
| `dotnet run --project Tests/Contracts -c Release` | PASS: signatures, payloads, matching Harmony pins |
| Contract command with installed 1.6 assembly path | PASS: signatures and direct callers recorded during initial migration |
| Contract command with installed 1.7 assembly path | PASS: signatures and direct callers recorded before restoring 1.6 |
| `python -m unittest discover -s Tests -p 'test_*.py' -v` | PASS: 10 package tests and 11 mock publishing/deployment tests |
| Steam description generation and package creation | PASS: player sections inspected; ZIP validated |
| Bash syntax, workflow YAML, and `git diff --check` | PASS; hosted PR checks recorded in GitHub |

Smoke coverage includes skipped originals, false query results, independent generators, bounded logs, changed/missing targets, repeated registration, actual Harmony installation failure, rollback after registration, and preservation/execution of other owners' patches. Another postfix can override the Boolean result; the mod does not force ordering.

An initial migration snapshot also passed in a fresh local clone without build intermediates. Current refactor logs are `artifacts/refactor-build.txt`, `refactor-smoke-1.6.txt`, `refactor-smoke-1.7.txt`, and `refactor-contracts.txt`. These logs, local inspection output, and the verification clone are ignored and excluded from the PR/package.

Publishing tests use fake SteamCMD, GitHub CLI, and download executables. They cover exact staged bytes, quoting/multiline VDF values, private output, closed stdin, timeout/error/unconfirmed-success rejection, invalid packages, existing-item enforcement, and restoration/deletion of fake login state. No live Steam authentication or upload occurs in these tests.

The candidate is `artifacts/NoCavesContinued-candidate.zip`. `scripts/package_mod.py` owns the exact ten-file payload list; both outputs contain only the mod DLL/PDB, and the ZIP includes no runtime dependencies, test doubles, handoffs, or development files. Original preview SHA-256: `e950aad91ca319249c775d0afc4f8ba138c232c562b26c91836423e334991c66`.

## Deployment readiness

The GitHub environment `steam-workshop` exists and permits deployment from `main` only. Repository variable `STEAM_WORKSHOP_ENABLED` is **false**. No Steam secrets are configured. The pipeline requires encrypted username and cached login-state secrets; see [setup and renewal](docs/STEAM_DEPLOYMENT.md). It consumes the same run's validated artifact, never a moving latest-release download. PR checks do not publish.

Mock execution verifies orchestration only. Authentication, token renewal, and a live Workshop upload remain untested. Static cached login may expire or rotate; failure requires interactive reauthorization and replacement of the encrypted state. No password storage, broad secret-writeback token, or plaintext cache was introduced.

## In-game verification on October 15

### RimWorld 1.6

Computer-use testing exercised candidate commit `db82c9f` in RimWorld **1.6.4871 rev591** (the installed `Version.txt` says rev590), with installed Harmony **2.4.3.0**, Core, and all five official DLC. Other mods were disabled in an isolated save folder. The user's normal mod configuration and preferences retained their original SHA-256 hashes.

The control uses seed `consortium`, 50% coverage, Normal rainfall/temperature/population/landmarks, 5% pollution, and the default factions. Scenario: Crashlanded, Cassandra Classic, Peaceful, reload anytime, ideology inactive. Tile **132336,0**, Doereba's Facade City, is mountainous temperate forest with caves, slate, and sandstone; map size **250 × 250**. Both fresh-generation comparisons use the same saved world and tile through the game's `Regenerate Current Map` developer action. This action replaces only the disposable session map; the original control save remains intact.

| Check | Observed result |
|---|---|
| Mod disabled, initial control | Visible winding cave passages; save contains 9 Glowstool and 1 Agarilux |
| Add candidate and load control save | Existing cave passages remain visible; both prefixes register |
| Generate fresh map with candidate | Cave passages suppressed; both prefixes log execution, including the false cave-query result; no Glowstool or Agarilux in the result save |
| Disable candidate, reload original control, repeat generation | Cave passages return; save contains 8 Glowstool and 2 Agarilux |

The native ruins generator reports `GenStep_ScatterGroupPrefabs ... could not find cell to generate at` in **both** regenerated runs. This is a control-reproduced warning, not evidence of a candidate-only failure. Ruins placement varies between generation contexts; this test establishes cave suppression, not identical placement of unrelated objects. The query execution stack reaches `World.HasCaves` from `GenStep_ScatterCaveDebris`.

Local evidence is under `artifacts/gameplay-20261015/`: the three `nocaves-1.6-*.rws` saves in `profile-1.6/Saves`, screenshots and per-session `Player.log` copies in `evidence`, and restoration notes in `RESTORE.txt`. These are excluded from commits and packages. This is one controlled 1.6 tile, not an exhaustive compatibility result.

### RimWorld 1.7

With the user's authorization, Steam's `unstable` branch installed **1.7.4980 rev60**, build **25972930**. Testing used installed Harmony **3.0.1.0**, Core, and all five official DLC in a separate `profile-1.7` save folder. The tested runtime is unchanged from `db82c9f`; its 1.7 DLL SHA-256 is `ebc81e9233d886d1deb51873a95b754905ff0d7fef1ca0d001a09f6a2ba1522f`.

The game's developer quicktest and `Generate Map With Caves` actions produced the control: seed **`shark`**, **30%** coverage, tile **63263,0**, `Base_Player` map, **250 × 250**. Candidate and disabled-repeat sessions loaded the same control copy and used `Regenerate Current Map`, preserving the saved world, tile, and map size. `Clear All Fog` exposed terrain for inspection. The untouched initial save is `c.rws`; the shared control copy changes only remembered camera position, not terrain or world data.

| Check | Observed result |
|---|---|
| Mod disabled, initial control | Visible winding cave passages; 8 Glowstool and 6 Agarilux |
| Add candidate and load control save | Existing cave passages remain visible; both prefixes register |
| Generate fresh map with candidate | Cave passages suppressed; both prefixes execute once; no Glowstool or Agarilux in the result save |
| Disable candidate, reload control, repeat generation | Cave passages return; 8 Glowstool and 6 Agarilux |

The cave utility execution stack reaches `TileMutatorWorker_Caves.GenerateCaves`; the false query execution reaches `World.HasCaves` from `GenStep_ScatterCaveDebris`. Both load sessions report the same 13 missing funeral-precept warnings. Vulkan registry lookup and native-allocation shutdown warnings also occur without the mod. No candidate-only error was observed in these sessions; the engine warnings remain unresolved. Unrelated ruins placement varies, so this is a cave comparison, not a claim that every generated object is identical.

Local evidence shares the directory above: `profile-1.7/Saves/nocaves-1.7-{control,candidate,control-repeat}.rws`, the untouched `c.rws`, screenshots, three session logs, installed-assembly contract output, and local method-body inspection under `evidence`. These tests establish ordinary cave suppression and exercise the query on one controlled tile per version; they do not establish all modded-map behavior.

### Restoration

Steam is back on the default `public` branch, **1.6.4871 rev590**, build **23969874**, with empty launch options. The restored executable and game assembly match the pre-test backup. Every original Workshop-mod file, normal `ModsConfig.xml`, and normal `Prefs.xml` matches its saved hash. Candidate copies, isolated test saves, and the game backup remain outside the active game installation. No user colony save was loaded, and no Workshop upload was performed.

## Pending gameplay checks

Still **NOT RUN**: GL Surface Cave and all four directional Cave Entrances on both versions, and the shared asteroid path. The earlier 1.6 GL report was a visual observation only; a GL build supporting RimWorld 1.7 has not been established.

For the remaining GL checks, compare interiors, roofs, floors, entrances, exits, and pathing, using a GL build that supports the game version being tested. Observe the shared asteroid path separately. Enable automatic publication only after the remaining checks and secret setup are complete.
