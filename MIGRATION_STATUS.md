# RimWorld 1.7 migration checkpoint

Updated October 14, 2026 (America/Los_Angeles). The 1.7 payload remains a candidate pending gameplay validation.

## Scope and repository state

The starting folder contained a task brief and handoff ZIP, not a checkout. Handoff integrity checks passed for all 23 files, six baseline Git blobs, and both embedded ZIPs. The maintained repository was fetched at `612630d16799ef326d25f93e55c8b58cf111b294`; the original handoff remains unchanged and excluded from the PR.

Work is on `codex/rimworld-1.7-uplift`. The user authorized a full GitHub PR and automated Workshop deployment on a hosted runner. No merge, live upload, game installation, or listing visibility change has been performed. Existing identity, Workshop item `3735336239`, both patches, kd8lvt credit, and artwork remain intact.

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

Both reference versions and installed RimWorld **1.6.4871 rev590** have exactly one declared overload of each target:

```text
static void RimWorld.MapGenCavesUtility.GenerateCaves(
    Verse.Map, Verse.BoolGrid, List<Verse.IntVec3>, Verse.Noise.ModuleBase,
    RimWorld.MapGenCavesUtility.CaveGenParms, Predicate<Verse.IntVec3>)
instance bool RimWorld.Planet.World.HasCaves(RimWorld.Planet.PlanetTile)
```

The installed 1.6 utility creates open/closed tunnels from rock groups. Its direct callers include `TileMutatorWorker_Caves.GenerateCaves` and `GenStep_Asteroid.GenerateCaveElevation`. `World.HasCaves` checks mutator `IsCave`; direct callers include the debug cave-map selector, ancient-turret generation, and cave-debris generation. This establishes broader-than-colony scope, not an exhaustive dynamic call graph. No 1.7 method bodies/callers were available; reference signatures alone do not establish semantics.

Installed Harmony DLL versions are 2.4.3.0 for 1.6 and 3.0.0.0 for 1.7; smoke tests use the pinned NuGet versions above. Installed Geological Landforms reports **mod version 1.7.13.1**, but advertises game support only through **RimWorld 1.6**. Its cave input reads `MapGenerator.Caves`, so independent landforms can still consume vanilla cave data.

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

## Pending gameplay checks

All new gameplay checks remain **NOT RUN**: ordinary cave suppression and the query on 1.7; fresh 1.6 regression; GL Surface Cave and all four directional Cave Entrances on both versions; existing-map behavior; and the shared asteroid path. There is no installed 1.7 game, and no game deployment was performed. The earlier 1.6 GL report was a visual observation only.

Next action: after approving game deployment, quit RimWorld, back up the old mod outside active Mods folders, and record game/Harmony/GL versions plus seed, tile, map size, scenario, and settings. Generate a disabled control that visibly contains vanilla caves, then restart with the matching candidate and regenerate from the same pre-generation baseline. Check terrain and actual query behavior separately from registration/UI labels. Compare GL interiors, roofs, floors, entrances, exits, and pathing; repeat on 1.6 and observe the asteroid path. Choose a GL build that supports the intended 1.7 game before claiming compatibility. Enable automatic publication only after those checks and secret setup are complete.
