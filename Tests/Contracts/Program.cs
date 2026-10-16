using Mono.Cecil;
using System.Xml.Linq;

// Reads metadata/IL only. Does not load or execute game or reference assemblies.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
var project = XDocument.Load(Path.Combine(root, "Source/NoCavesContinued.csproj"));
var smokeProject = XDocument.Load(Path.Combine(root, "Tests/Smoke/Smoke.csproj"));
foreach (var (gameVersion, framework, expectedFramework, harmonyMajor) in new[]
    { ("1.6", "net472", ".NETFramework,Version=v4.7.2", 2), ("1.7", "net10.0", ".NETCoreApp,Version=v10.0", 3) })
{
    Require(PackageVersion(project, framework, "Lib.Harmony.Ref") == PackageVersion(smokeProject, framework, "Lib.Harmony"),
        $"{gameVersion} smoke runtime matches compiled Harmony reference");
    string version = PackageVersion(project, framework, "Krafs.Rimworld.Ref");
    Inspect(Path.Combine(cache, "krafs.rimworld.ref", version, "ref", framework, "Assembly-CSharp.dll"), true);
    using var payload = AssemblyDefinition.ReadAssembly(Path.Combine(root, gameVersion, "Assemblies/NoCavesContinued.dll"));
    Require(payload.Name.Name == "NoCavesContinued", "assembly identity");
    Require(payload.CustomAttributes.Single(attribute => attribute.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
        .ConstructorArguments[0].Value.ToString() == expectedFramework, "framework isolation");
    Require(payload.MainModule.AssemblyReferences.Single(reference => reference.Name == "0Harmony").Version.Major == harmonyMajor, "Harmony major isolation");
    Require(!AllTypes(payload.MainModule.Types).Any(type => type.Namespace == "Verse" || type.Namespace.StartsWith("RimWorld")), "no game doubles in payload");
    Console.WriteLine($"PASS payload {gameVersion}: {expectedFramework}, Harmony {harmonyMajor}");
}
foreach (string path in args) Inspect(path);

static string PackageVersion(XDocument project, string framework, string packageName) => project.Descendants("ItemGroup")
    .Single(group => (string)group.Attribute("Condition") == $"'$(TargetFramework)' == '{framework}'")
    .Elements("PackageReference").Single(package => (string)package.Attribute("Include") == packageName).Attribute("Version").Value;

static void Inspect(string path, bool referenceOnly = false)
{
    using var assembly = AssemblyDefinition.ReadAssembly(path);
    var types = AllTypes(assembly.MainModule.Types).ToArray();
    var generator = types.Single(type => type.FullName == "RimWorld.MapGenCavesUtility").Methods.Where(method => method.Name == "GenerateCaves").ToArray();
    var query = types.Single(type => type.FullName == "RimWorld.Planet.World").Methods.Where(method => method.Name == "HasCaves").ToArray();
    Require(generator.Length == 1 && query.Length == 1, "exactly one of each target");
    // Independent expectations: changing runtime signature guards must not silently change this check.
    Require(generator[0].IsStatic && !generator[0].HasGenericParameters && generator[0].ReturnType.FullName == "System.Void" &&
        generator[0].Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] {
            "Verse.Map", "Verse.BoolGrid", "System.Collections.Generic.List`1<Verse.IntVec3>", "Verse.Noise.ModuleBase",
            "RimWorld.MapGenCavesUtility/CaveGenParms", "System.Predicate`1<Verse.IntVec3>" }), "generator signature");
    Require(!query[0].IsStatic && !query[0].HasGenericParameters && query[0].ReturnType.FullName == "System.Boolean" &&
        query[0].Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] { "RimWorld.Planet.PlanetTile" }), "query signature");
    Console.WriteLine($"PASS contracts: {path}");
    foreach (var target in new[] { generator[0], query[0] })
    {
        Console.WriteLine("  " + target.FullName + (referenceOnly ? " [reference only; no game behavior]" : " [installed assembly]"));
        if (referenceOnly) continue;
        foreach (var method in types.SelectMany(type => type.Methods).Where(method => method.HasBody))
            if (method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference called && called.FullName == target.FullName))
                Console.WriteLine("    caller: " + method.FullName);
    }
}
static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types) => types.SelectMany(type => new[] { type }.Concat(AllTypes(type.NestedTypes)));
static void Require(bool value, string description) { if (!value) throw new Exception("FAIL: " + description); }
