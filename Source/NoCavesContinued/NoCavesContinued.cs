using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Verse;

namespace NoCavesContinued
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        internal const string HarmonyId = "sayhiben.nocavescontinued";
        private static int generatorLogged;
        private static int queryLogged;

        internal static readonly string[] GeneratorParameters =
        {
            "Verse.Map", "Verse.BoolGrid", "System.Collections.Generic.List`1[Verse.IntVec3]",
            "Verse.Noise.ModuleBase", "RimWorld.MapGenCavesUtility+CaveGenParms",
            "System.Predicate`1[Verse.IntVec3]"
        };
        internal static readonly string[] QueryParameters = { "RimWorld.Planet.PlanetTile" };

        static Bootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            bool generator = PatchInstaller.Install(harmony,
                "RimWorld.MapGenCavesUtility", "GenerateCaves", true, typeof(void), GeneratorParameters,
                AccessTools.Method(typeof(Bootstrap), nameof(SkipCaveGeneration)));
            bool query = PatchInstaller.Install(harmony,
                "RimWorld.Planet.World", "HasCaves", false, typeof(bool), QueryParameters,
                AccessTools.Method(typeof(Bootstrap), nameof(ReportNoCaves)));
            if (!generator || !query)
                PatchInstaller.LogLoadedCaveTypes();
        }

        public static bool SkipCaveGeneration()
        {
            if (Interlocked.Exchange(ref generatorLogged, 1) == 0)
                Log.Message("[No Caves - Continued] SkipCaveGeneration executed; skipping the vanilla cave utility.");
            return false;
        }

        public static bool ReportNoCaves(ref bool __result)
        {
            __result = false;
            if (Interlocked.Exchange(ref queryLogged, 1) == 0)
                Log.Message("[No Caves - Continued] ReportNoCaves executed; returning false for the vanilla cave query.");
            return false;
        }
    }

    internal static class PatchInstaller
    {
        // Reject new overloads and changed signatures until their behavior has been reviewed.
        internal static bool Install(Harmony harmony, string typeName, string methodName,
            bool isStatic, Type returnType, string[] parameterTypes, MethodInfo prefix)
        {
            string targetName = typeName + "." + methodName;
            MethodInfo target = null;
            bool attempted = false;
            try
            {
                Type targetType = AccessTools.TypeByName(typeName);
                MethodInfo[] methods = targetType == null ? new MethodInfo[0] :
                    AccessTools.GetDeclaredMethods(targetType).Where(method => method.Name == methodName).ToArray();
                if (methods.Length != 1 || !Matches(methods[0], isStatic, returnType, parameterTypes))
                {
                    string found = methods.Length == 0 ? "none (type or method missing)" :
                        string.Join("; ", methods.Select(Describe));
                    Log.Error($"[No Caves - Continued] Cannot patch {targetName}: expected exactly one " +
                        $"{(isStatic ? "static" : "instance")} {returnType} ({string.Join(", ", parameterTypes)}); found {found}. Nothing was patched for this target.");
                    return false;
                }

                target = methods[0];
                var existing = Harmony.GetPatchInfo(target);
                if (existing != null && existing.Prefixes.Any(patch => patch.owner == harmony.Id))
                {
                    bool registered = existing.Prefixes.Count(patch => patch.owner == harmony.Id) == 1 &&
                        existing.Prefixes.Any(patch => patch.owner == harmony.Id && patch.PatchMethod == prefix);
                    if (!registered)
                        Log.Error($"[No Caves - Continued] Unexpected existing prefix owned by {harmony.Id} on {targetName}; left unchanged.");
                    return registered;
                }

                attempted = true;
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));

                var info = Harmony.GetPatchInfo(target);
                if (info == null || !info.Prefixes.Any(patch => patch.owner == harmony.Id && patch.PatchMethod == prefix))
                    throw new InvalidOperationException("Harmony did not register the requested prefix.");
                Log.Message($"[No Caves - Continued] Registered {prefix.Name} on {Describe(target)}. Execution has not yet been observed.");
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"[No Caves - Continued] Failed to patch {targetName}: {exception}");
                if (attempted)
                {
                    try
                    {
                        // Unpatch by owner and category: the same prefix can belong to another owner too.
                        harmony.Unpatch(target, HarmonyPatchType.Prefix, harmony.Id);
                    }
                    catch (Exception cleanupException)
                    {
                        Log.Error($"[No Caves - Continued] Could not roll back our prefix on {targetName}: {cleanupException}");
                    }
                }
                return false;
            }
        }

        private static bool Matches(MethodInfo method, bool isStatic, Type returnType, string[] parameterTypes)
        {
            return method.IsStatic == isStatic && method.ReturnType == returnType &&
                !method.IsGenericMethod && !method.ContainsGenericParameters && !method.IsAbstract &&
                method.GetParameters().Select(parameter => parameter.ParameterType.ToString()).SequenceEqual(parameterTypes);
        }

        private static string Describe(MethodInfo method)
        {
            return $"{(method.IsStatic ? "static" : "instance")} {method.ReturnType} {method.DeclaringType.FullName}.{method.Name}" +
                $"({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.ToString()))})";
        }

        internal static void LogLoadedCaveTypes()
        {
            string[] caveTypeDescriptions = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetTypes)
                .Where(type => type.FullName != null && type.FullName.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(type => type.FullName)
                .Take(50)
                .Select(type =>
                {
                    string[] methodNames = AccessTools.GetDeclaredMethods(type)
                        .Select(method => method.Name)
                        .Distinct()
                        .OrderBy(methodName => methodName)
                        .Take(20)
                        .ToArray();

                    return type.FullName + " methods=[" + string.Join(", ", methodNames) + "]";
                })
                .ToArray();

            Log.Error(caveTypeDescriptions.Length == 0
                ? "[No Caves - Continued] No loaded types with 'Cave' in the name were found."
                : "[No Caves - Continued] Loaded cave-related types: " + string.Join("; ", caveTypeDescriptions));
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                return new Type[0];
            }
        }
    }
}
