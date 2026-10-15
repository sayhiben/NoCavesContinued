using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NoCavesContinued;
using RimWorld;
using RimWorld.Planet;
using Verse;

internal static class Program
{
    private static readonly Harmony Ours = new Harmony(Bootstrap.HarmonyId);
    private static readonly Harmony Other = new Harmony("nocaves.tests.other");
    private static int checks;
    private static int observerCalls;
    private static readonly MethodInfo Generator = AccessTools.Method(typeof(MapGenCavesUtility), "GenerateCaves");
    private static readonly MethodInfo Query = AccessTools.Method(typeof(World), "HasCaves");
    private static readonly MethodInfo Skip = AccessTools.Method(typeof(Bootstrap), "SkipCaveGeneration");
    private static readonly MethodInfo NoCaves = AccessTools.Method(typeof(Bootstrap), "ReportNoCaves");

    private static int Main()
    {
        try
        {
            VerifyStartupAndBehavior();
            VerifySignatureGuards();
            VerifyOtherMods();
            VerifyFailureCleanup();
            Console.WriteLine($"PASS: {checks} smoke assertions using Harmony {typeof(Harmony).Assembly.GetName().Version}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}. Test doubles only; no gameplay tested.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            foreach (var method in new[] { Generator, Query, AccessTools.Method(typeof(FailureTarget), "Run") })
            {
                Ours.Unpatch(method, HarmonyPatchType.All, Ours.Id);
                Other.Unpatch(method, HarmonyPatchType.All, Other.Id);
            }
        }
    }

    private static void VerifyStartupAndBehavior()
    {
        // Calling before startup proves that the originals are executable controls.
        Generate();
        Check(new World().HasCaves(default(PlanetTile)) && World.Calls == 1 && MapGenCavesUtility.Calls == 1,
            "unpatched controls execute");
        RuntimeHelpers.RunClassConstructor(typeof(Bootstrap).TypeHandle);
        Check(Log.Errors.Count == 0, "startup installs both patches without errors");
        Check(Log.Messages.Count(message => message.Contains("Registered")) == 2, "two registration logs");
        Check(Log.Messages.All(message => !message.Contains("executed;")), "registration is not execution");
        Generate(); Generate();
        Check(MapGenCavesUtility.Calls == 1, "generator original skipped");
        Check(!new World().HasCaves(default(PlanetTile)) && !new World().HasCaves(default(PlanetTile)) && World.Calls == 1,
            "query original skipped with false result");
        bool result = true;
        Check(!Bootstrap.ReportNoCaves(ref result) && !result, "query resets an already true result");
        MapGenCavesUtility.IndependentGenerator();
        Check(MapGenCavesUtility.IndependentCalls == 1, "independent generator untouched");
        Check(Log.Messages.Count(message => message.Contains("SkipCaveGeneration executed")) == 1 &&
            Log.Messages.Count(message => message.Contains("ReportNoCaves executed")) == 1, "execution logged once per prefix");

        Check(InstallGenerator() && InstallQuery(), "repeat registration succeeds");
        Check(Harmony.GetPatchInfo(Generator).Prefixes.Count(patch => patch.owner == Ours.Id) == 1 &&
            Harmony.GetPatchInfo(Query).Prefixes.Count(patch => patch.owner == Ours.Id) == 1, "no duplicate registration");
    }

    private static void VerifySignatureGuards()
    {
        Reject(null, "Run", true, typeof(void), new string[0], "missing type");
        Reject(typeof(FailureTarget), "Missing", true, typeof(void), new string[0], "missing method");
        Reject(typeof(Overloads), "Run", true, typeof(void), new string[0], "new overload");
        Reject(typeof(WrongReturn), "Run", true, typeof(void), new string[0], "changed return");
        Reject(typeof(WrongReturn), "Run", true, typeof(bool), Bootstrap.QueryParameters, "changed parameters");
        Reject(typeof(RefArgument), "Run", true, typeof(void), new[] { "System.Int32" }, "ref argument");
        Reject(typeof(OutArgument), "Run", true, typeof(void), new[] { "System.Int32" }, "out argument");
        Reject(typeof(GenericTarget), "Run", true, typeof(void), new string[0], "generic method");
        Reject(typeof(World), "HasCaves", true, typeof(bool), Bootstrap.QueryParameters, "changed static/instance shape");
        Reject(typeof(AbstractTarget), "Run", false, typeof(void), new string[0], "abstract method");
    }

    private static void VerifyOtherMods()
    {
        // Harmony may still run other prefixes and postfixes after a skip.
        Other.Patch(Generator, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(Observe))));
        Other.Patch(Query, postfix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(ForceTrue))));
        Generate();
        Check(observerCalls == 1 && MapGenCavesUtility.Calls == 1, "other side-effect-free prefix still runs");
        Check(new World().HasCaves(default(PlanetTile)) && World.Calls == 1, "another mod's postfix can override false");
        Other.Unpatch(Query, HarmonyPatchType.Postfix, Other.Id);
    }

    private static void VerifyFailureCleanup()
    {
        MethodInfo failure = AccessTools.Method(typeof(FailureTarget), "Run");
        // Deliberately share the exact prefix MethodInfo with another owner.
        Other.Patch(failure, prefix: new HarmonyMethod(Skip));
        Ours.Patch(failure, postfix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(Observe))));
        int errors = Log.Errors.Count;
        int messages = Log.Messages.Count;
        Check(!InstallFailureTarget(AccessTools.Method(typeof(Program), nameof(InvalidPrefix))), "invalid Harmony prefix rejected");
        Check(Log.Errors.Count > errors && Log.Errors.Last().Contains("missingArgument") && Log.Messages.Count == messages,
            "actual Harmony failure is reported without a success log");
        Check(Harmony.GetPatchInfo(failure).Prefixes.All(patch => patch.owner != Ours.Id), "failed Harmony prefix removed");

        // A test-only Verse.Log failure occurs after real Harmony registration; no test hook is shipped.
        errors = Log.Errors.Count;
        Log.ThrowOnNextMessage = true;
        Check(!InstallFailureTarget(Skip), "failure after registration rejected");
        Check(Log.Errors.Count > errors && Log.Errors.Last().Contains("test log failure") && Log.Messages.Count == messages,
            "failure after registration is reported without a success log");
        var info = Harmony.GetPatchInfo(failure);
        Check(info.Prefixes.All(patch => patch.owner != Ours.Id) && info.Prefixes.Any(patch => patch.owner == Other.Id && patch.PatchMethod == Skip),
            "rollback removes only our prefix, including shared-method ownership case");
        Check(info.Postfixes.Any(patch => patch.owner == Ours.Id), "rollback preserves our unrelated postfix");
        Check(Harmony.GetPatchInfo(Generator).Prefixes.Any(patch => patch.owner == Ours.Id), "rollback preserves other target");
        int observations = observerCalls;
        FailureTarget.Run();
        Check(FailureTarget.Calls == 0 && observerCalls == observations + 1, "remaining patches execute after rollback");

        Ours.Patch(failure, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(Observe))));
        Check(!InstallFailureTarget(Skip), "unexpected same-owner prefix rejected");
        Check(Harmony.GetPatchInfo(failure).Prefixes.Any(patch => patch.owner == Ours.Id && patch.PatchMethod.Name == nameof(Observe)),
            "preexisting prefix left unchanged");
    }

    private static bool InstallGenerator() => PatchInstaller.Install(Ours, typeof(MapGenCavesUtility).FullName,
        "GenerateCaves", true, typeof(void), Bootstrap.GeneratorParameters, Skip);
    private static bool InstallQuery() => PatchInstaller.Install(Ours, typeof(World).FullName,
        "HasCaves", false, typeof(bool), Bootstrap.QueryParameters, NoCaves);
    private static bool InstallFailureTarget(MethodInfo prefix) => PatchInstaller.Install(Ours, typeof(FailureTarget).FullName,
        "Run", true, typeof(void), new string[0], prefix);
    private static void Reject(Type type, string name, bool isStatic, Type result, string[] args, string label)
    {
        int errors = Log.Errors.Count;
        int successes = Log.Messages.Count;
        Check(!PatchInstaller.Install(Ours, type == null ? "Missing.Type" : type.FullName, name, isStatic, result, args, Skip), label);
        Check(Log.Errors.Count > errors && Log.Errors.Last().Contains("Nothing was patched") && Log.Messages.Count == successes,
            label + " diagnostic without success");
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label + "\n" + string.Join("\n", Log.Errors));
        checks++;
    }
    private static void Generate() => MapGenCavesUtility.GenerateCaves(null, null, null, null, default(MapGenCavesUtility.CaveGenParms), null);
    public static void Observe() { observerCalls++; }
    public static void ForceTrue(ref bool __result) { __result = true; }
    public static bool InvalidPrefix(int missingArgument) { return false; }
    public static class FailureTarget
    {
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Run() { Calls++; }
    }
    public static class Overloads { public static void Run() { } public static void Run(int value) { } }
    public static class WrongReturn { public static int Run() => 1; }
    public static class RefArgument { public static void Run(ref int value) { } }
    public static class OutArgument { public static void Run(out int value) { value = 1; } }
    public static class GenericTarget { public static void Run<T>() { } }
    public abstract class AbstractTarget { public abstract void Run(); }
}
