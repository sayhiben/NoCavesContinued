// Test-only signatures. These are not a simulation of RimWorld map generation.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Verse
{
    public class StaticConstructorOnStartup : Attribute { }
    public static class Log
    {
        public static readonly List<string> Messages = new List<string>();
        public static readonly List<string> Errors = new List<string>();
        public static bool ThrowOnNextMessage;
        public static void Message(string message)
        {
            if (ThrowOnNextMessage)
            {
                ThrowOnNextMessage = false;
                throw new InvalidOperationException("test log failure after registration");
            }
            Messages.Add(message);
        }
        public static void Error(string message) { Errors.Add(message); }
    }
    public class Map { }
    public class BoolGrid { }
    public struct IntVec3 { }
}
namespace Verse.Noise { public class ModuleBase { } }
namespace RimWorld
{
    public static class MapGenCavesUtility
    {
        public struct CaveGenParms { }
        public static int Calls;
        public static int IndependentCalls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GenerateCaves(Verse.Map map, Verse.BoolGrid visited, List<Verse.IntVec3> group,
            Verse.Noise.ModuleBase noise, CaveGenParms parms, Predicate<Verse.IntVec3> isRock) { Calls++; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void IndependentGenerator() { IndependentCalls++; }
    }
}
namespace RimWorld.Planet
{
    public struct PlanetTile { }
    public class World
    {
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool HasCaves(PlanetTile tile) { Calls++; return true; }
    }
}
