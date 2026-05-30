using System;
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

        static Bootstrap()
        {
            Harmony harmony = new Harmony(HarmonyId);
            Patch(harmony, "RimWorld.MapGenCavesUtility", "GenerateCaves", nameof(SkipCaveGeneration));
            Patch(harmony, "RimWorld.Planet.World", "HasCaves", nameof(ReportNoCaves));
        }

        public static bool SkipCaveGeneration() => false;

        public static bool ReportNoCaves(ref bool __result)
        {
            __result = false;
            return false;
        }

        private static void Patch(Harmony harmony, string targetTypeName, string targetMethodName, string prefixMethodName)
        {
            Type targetType = AccessTools.TypeByName(targetTypeName);
            if (targetType == null)
            {
                Log.Error($"[No Caves - Continued] Could not find {targetTypeName}.{targetMethodName}. Nothing was patched.");
                LogCaveDiagnostics();
                return;
            }

            MethodInfo prefixMethod = AccessTools.Method(typeof(Bootstrap), prefixMethodName);
            MethodInfo[] targetMethods = AccessTools.GetDeclaredMethods(targetType)
                .Where(method => method.Name == targetMethodName)
                .ToArray();
            if (targetMethods.Length == 0)
            {
                Log.Error($"[No Caves - Continued] Could not find {targetTypeName}.{targetMethodName}. Nothing was patched.");
                LogCaveDiagnostics();
                return;
            }

            foreach (MethodInfo targetMethod in targetMethods)
            {
                harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
                Log.Message($"[No Caves - Continued] Patched {DescribeMethod(targetMethod)} with {prefixMethod.Name}.");
            }

            Log.Message($"[No Caves - Continued] Finished patching {targetMethods.Length} {targetTypeName}.{targetMethodName} method(s).");
        }

        private static string DescribeMethod(MethodInfo method)
        {
            string parameterList = string.Join(
                ", ",
                method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));

            return $"{method.DeclaringType.FullName}.{method.Name}({parameterList})";
        }

        private static void LogCaveDiagnostics()
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

            if (caveTypeDescriptions.Length == 0)
            {
                Log.Error("[No Caves - Continued] No loaded types with 'Cave' in the name were found.");
                return;
            }

            Log.Error("[No Caves - Continued] Loaded cave-related types: " + string.Join("; ", caveTypeDescriptions));
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
