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
        private const string TargetTypeName = "RimWorld.MapGenCavesUtility";
        private const string TargetMethodName = "GenerateCaves";

        static Bootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            MethodInfo prefixMethod = AccessTools.Method(typeof(Bootstrap), nameof(SkipCaveGeneration));

            if (prefixMethod == null)
            {
                Log.Error("[No Caves - Continued] Could not find prefix method. Cave generation was not patched.");
                return;
            }

            Type targetType = AccessTools.TypeByName(TargetTypeName);
            if (targetType == null)
            {
                Log.Error(
                    $"[No Caves - Continued] Could not find {TargetTypeName}. " +
                    DescribeDiscoveredCaveTypesAndMethods());
                return;
            }

            List<MethodInfo> targetMethods = AccessTools.GetDeclaredMethods(targetType)
                .Where(method => method.Name == TargetMethodName)
                .ToList();

            if (targetMethods.Count == 0)
            {
                Log.Error(
                    $"[No Caves - Continued] Could not find {TargetTypeName}.{TargetMethodName}. " +
                    DescribeDiscoveredCaveTypesAndMethods());
                return;
            }

            foreach (MethodInfo targetMethod in targetMethods)
            {
                harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
                Log.Message($"[No Caves - Continued] Patched {DescribeMethod(targetMethod)}; cave generation will be skipped.");
            }

            Log.Message($"[No Caves - Continued] Finished patching {targetMethods.Count} cave-generation method(s).");
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
    }
}
