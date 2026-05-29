# AGENTS.md

Guidance for coding agents working in this repository.

## Project Purpose

No Caves - Continued is a small RimWorld 1.6 Harmony mod. Its purpose is to prevent vanilla cave tunnel generation during map generation.

The current implementation patches RimWorld cave-generation methods with a Harmony prefix that returns `false`, which skips the original method. The main RimWorld 1.6 target is:

```text
RimWorld.MapGenCavesUtility.GenerateCaves
```

Older or alternate cave-generation targets are patched opportunistically when those types exist.

## Repository Layout

```text
About/About.xml
  RimWorld mod metadata, package id, supported game version, Harmony dependency, and load order.

LoadFolders.xml
  RimWorld version folder mapping. For RimWorld 1.6, load the repo root and the 1.6 folder.

1.6/Assemblies/
  Packaged mod assembly output consumed by RimWorld.

Source/NoCavesContinued.csproj
  .NET project file. Builds directly to ../1.6/Assemblies/.

Source/NoCavesContinued/NoCavesContinued.cs
  The full runtime implementation.

Source/obj/
  Generated build intermediates. Do not edit by hand.
```

## Build

Build from the `Source` directory:

```bash
dotnet restore
dotnet build -c Release
```

Expected output:

```text
1.6/Assemblies/NoCavesContinued.dll
1.6/Assemblies/NoCavesContinued.pdb
```

The project targets `net472` for RimWorld/Unity compatibility. Keep the build output path aligned with RimWorld's mod folder layout unless intentionally changing packaging.

## Verification

At minimum, run:

```bash
dotnet build -c Release
```

There is no automated test harness in this repository. Runtime verification requires RimWorld:

1. Install the entire `NoCavesContinued` folder as a RimWorld mod.
2. Load Harmony before this mod.
3. Start a new map on a mountainous or impassable tile that would normally contain caves.
4. Confirm the RimWorld log contains messages like:

```text
[No Caves - Continued] Patched RimWorld.MapGenCavesUtility.GenerateCaves(...); cave generation will be skipped.
[No Caves - Continued] Finished patching 1 cave-generation method(s).
```

If patching fails, inspect the diagnostic log emitted by the mod. It lists loaded cave-related types and declared method names to help identify a changed RimWorld target.

## Implementation Notes

- `Bootstrap` is marked with `Verse.StaticConstructorOnStartup`, so RimWorld runs its static constructor when the mod assembly loads.
- The Harmony ID is `sayhiben.nocavescontinued`; keep it stable unless intentionally creating a distinct mod identity.
- Patch targets are resolved by string name through `HarmonyLib.AccessTools`. This avoids compile-time references to RimWorld internal types that may be absent from public reference assemblies.
- The required modern target is `RimWorld.MapGenCavesUtility.GenerateCaves`.
- `RimWorld.GenStep_Caves.Generate` and `RimWorld.GenStep_CavesTerrain.Generate` are optional legacy targets.
- The patch intentionally applies to every declared overload matching the configured method name.
- The prefix method should stay minimal. Returning `false` is the behavior that skips cave generation.
- Keep the failure diagnostics useful. If targets change, preserve or improve the cave-type discovery output instead of replacing it with a generic error.

## Editing Guidelines

- Keep changes narrowly scoped. This mod has one job: skip vanilla cave generation.
- Prefer compatibility with RimWorld 1.6 and Unity's Mono runtime over newer .NET idioms.
- Do not add runtime copies of Harmony, RimWorld, Unity, or reference assemblies to the mod package. Harmony is a declared mod dependency.
- Do not edit generated files under `Source/obj/`.
- If source code changes affect runtime behavior, rebuild so `1.6/Assemblies/NoCavesContinued.dll` matches the source.
- Preserve the simple C# style already present: block-scoped namespace, explicit helper methods, and clear log messages.
- Avoid unrelated formatting churn in XML or C# files.
- Use concise comments only where they explain RimWorld/Harmony behavior that is not obvious from the code.

## Release Checklist

Before considering a change complete:

1. Confirm `About/About.xml` still declares the correct package id, supported RimWorld version, Harmony dependency, and load order.
2. Confirm `LoadFolders.xml` still maps RimWorld 1.6 to the root and `1.6` folders.
3. Run `dotnet build -c Release` from `Source`.
4. Confirm the built DLL is present under `1.6/Assemblies/`.
5. For behavior changes, verify in RimWorld using a cave-prone map tile and inspect the game log.

