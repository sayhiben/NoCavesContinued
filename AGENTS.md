# AGENTS.md

Guidance for coding agents working in this repository.

## Purpose and scope

No Caves - Continued has two fixed Harmony prefixes, shared by its RimWorld 1.6 and 1.7 builds:

- `RimWorld.MapGenCavesUtility.GenerateCaves`: skip the original void method.
- `RimWorld.Planet.World.HasCaves`: set its Boolean result false and skip the original.

Keep author `sayhiben`, package/Harmony ID `sayhiben.nocavescontinued`, assembly `NoCavesContinued.dll`, Workshop item `3735336239`, existing artwork, and kd8lvt credit stable. Archived Redux sources are not the maintained implementation.

Leave independent generators alone. Never clear `MapGenerator.Caves`, patch arbitrary cave-named types, or imply this blocks every modded cave system. The inspected utilities in both versions have an asteroid caller. Historical Geological Landforms observations are limited; neither its 1.7 compatibility nor full cave access/shape has been established.

## Design and editing

- Keep one shared runtime source and isolated version outputs. The project file owns dependency pins; `LoadFolders.xml` owns game routing.
- Separate startup/behavior from Harmony installation checks. Two fixed patches need no interfaces, inheritance hierarchy, dependency container, or configurable patch registry.
- Preserve exact signature guards, separate void/query handling, useful errors, registration checks, and once-per-session execution logs. Reject changed APIs until reviewed.
- Cleanup must remove only our prefix from the failed target. Preserve other owners, patch categories, and targets; do not force patch priorities without evidence.
- Share actual production rules, such as the ZIP payload allowlist. Keep independent test expectations where they catch accidental changes; do not make tests merely repeat implementation output.
- Preserve the existing block-scoped C# style and concise explanatory comments. Avoid unrelated formatting churn.
- Do not edit generated `bin`/`obj` files. Rebuild both tracked DLL/PDB pairs when runtime source changes. Never ship game, Harmony, Unity, reference, or test-double assemblies.

## Documentation ownership

- `README.md` owns player-facing behavior and build/test/package usage. It also supplies Steam description text through the existing `md2steam` converter; do not hand-roll a replacement. Preserve its excluded developer headings when editing.
- `docs/STEAM_DEPLOYMENT.md` owns hosted upload setup and authentication recovery. CLI help owns individual publishing options.
- `MIGRATION_STATUS.md` owns dependency versions actually checked, evidence, current test results, and pending gameplay checks.
- This file owns durable constraints, not repeated command recipes or session history. `CLAUDE.md` delegates here.
- Preserve input handoffs and their integrity manifests as historical evidence. Exclude them and local inspection artifacts from commits and packages.
- Preserve the AI-assistance disclosure in README, its generated Workshop description, and the in-game About text. Disclose AI contributions in PR descriptions without implying unperformed human review or gameplay validation.

## Verification

Run the build and focused checks documented in the README after relevant changes: both Harmony smoke suites, reference/payload contracts, Python package/publishing tests, and shell/workflow validation when affected. Regenerate and inspect the Steam description after README changes. Package creation validates the ZIP once; the publisher independently validates supplied/downloaded archives.

Mocks, reference signatures, and registration logs do not prove gameplay compatibility. Record missing runtime checks explicitly. Use controlled seed/tile/map-size comparisons for ordinary cave suppression, the query, GL Surface Cave/directional entrances, and the known asteroid caller. Never claim unperformed tests passed.

## Publication

Preserve user changes. Commit/push/open a PR when authorized; do not merge, release, upload, change listing visibility, or install into the game without applicable authorization. Main pushes create releases and can trigger configured Workshop deployment. PR checks must never upload.

Hosted deployment uses encrypted GitHub environment secrets for Steam username and cached login state. Keep credentials, state, and private Steam logs out of source, console output, caches, and artifacts. Local authentication remains interactive; never request passwords or Steam Guard codes in chat. Preserve real newlines in Workshop VDF descriptions.

The publishing helper is not a build test. Test upload paths with fake executables and temporary packages; a live upload needs working credentials and explicit publication scope.
