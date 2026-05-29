using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace NoCavesContinued
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        private const string HarmonyId = "sayhiben.nocavescontinued";

        private static readonly PatchTarget[] PatchTargets =
        {
            // RimWorld 1.6 cave generation converges here. Current public logs show:
            // TileMutatorWorker_Caves.GenerateCaves -> MapGenCavesUtility.GenerateCaves.
            new PatchTarget("RimWorld.MapGenCavesUtility", "GenerateCaves", required: true),

            // Older / alternate names. These are optional because they are absent in current 1.6 builds.
            new PatchTarget("RimWorld.GenStep_Caves", "Generate", required: false),
            new PatchTarget("RimWorld.GenStep_CavesTerrain", "Generate", required: false)
        };

        static Bootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            MethodInfo prefixMethod = AccessTools.Method(typeof(Bootstrap), nameof(SkipCaveGeneration));

            if (prefixMethod == null)
            {
                Log.Error("[No Caves - Continued] Could not find prefix method. Cave generation was not patched.");
                return;
            }

            List<MethodInfo> patchedMethods = new List<MethodInfo>();
            List<string> missingRequiredTargets = new List<string>();

            foreach (PatchTarget patchTarget in PatchTargets)
            {
                Type targetType = AccessTools.TypeByName(patchTarget.TypeName);
                if (targetType == null)
                {
                    if (patchTarget.Required)
                    {
                        missingRequiredTargets.Add(patchTarget.TypeName);
                    }

                    continue;
                }

                List<MethodInfo> targetMethods = AccessTools.GetDeclaredMethods(targetType)
                    .Where(method => method.Name == patchTarget.MethodName)
                    .ToList();

                if (targetMethods.Count == 0)
                {
                    if (patchTarget.Required)
                    {
                        missingRequiredTargets.Add($"{patchTarget.TypeName}.{patchTarget.MethodName}");
                    }

                    continue;
                }

                foreach (MethodInfo targetMethod in targetMethods)
                {
                    harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
                    patchedMethods.Add(targetMethod);
                    Log.Message($"[No Caves - Continued] Patched {DescribeMethod(targetMethod)}; cave generation will be skipped.");
                }
            }

            if (patchedMethods.Count > 0)
            {
                Log.Message($"[No Caves - Continued] Finished patching {patchedMethods.Count} cave-generation method(s).");
                return;
            }

            Log.Error(
                "[No Caves - Continued] Could not patch cave generation. " +
                "Missing required targets: " + string.Join(", ", missingRequiredTargets) + ". " +
                DescribeDiscoveredCaveTypesAndMethods());
        }

        public static bool SkipCaveGeneration()
        {
            // Returning false from a Harmony prefix skips the original method.
            return false;
        }

        private static string DescribeMethod(MethodInfo method)
        {
            string parameterList = string.Join(
                ", ",
                method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));

            return $"{method.DeclaringType.FullName}.{method.Name}({parameterList})";
        }

        private static string DescribeDiscoveredCaveTypesAndMethods()
        {
            List<string> caveTypeDescriptions = GetLoadedTypes()
                .Where(type => type.FullName != null && type.FullName.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(type => type.FullName)
                .Take(50)
                .Select(type =>
                {
                    List<string> methodNames = AccessTools.GetDeclaredMethods(type)
                        .Select(method => method.Name)
                        .Distinct()
                        .OrderBy(methodName => methodName)
                        .Take(20)
                        .ToList();

                    return type.FullName + " methods=[" + string.Join(", ", methodNames) + "]";
                })
                .ToList();

            if (caveTypeDescriptions.Count == 0)
            {
                return "No loaded types with 'Cave' in the name were found.";
            }

            return "Loaded cave-related types: " + string.Join("; ", caveTypeDescriptions);
        }

        private static IEnumerable<Type> GetLoadedTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    yield return type;
                }
            }
        }

        private sealed class PatchTarget
        {
            public PatchTarget(string typeName, string methodName, bool required)
            {
                TypeName = typeName;
                MethodName = methodName;
                Required = required;
            }

            public string TypeName { get; }
            public string MethodName { get; }
            public bool Required { get; }
        }
    }
}
