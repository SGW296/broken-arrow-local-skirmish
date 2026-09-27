using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DeckSelectionRegression;
using HarmonyLib;

static class Program
{
    static void Check(bool condition, string failure)
    {
        if (!condition) throw new Exception(failure);
    }

    // Inspect the shipped assembly without loading Unity or starting MelonLoader.
    static bool OptsOutOfAutomaticPatching(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var constructor = metadata.GetCustomAttribute(handle).Constructor;
            if (constructor.Kind != HandleKind.MemberReference) continue;
            var parent = metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent;
            if (parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
            if (metadata.GetString(type.Namespace) == "MelonLoader"
                && metadata.GetString(type.Name) == "HarmonyDontPatchAllAttribute") return true;
        }
        return false;
    }

    static void Register(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(AiPlayerDeckPatch)).Patch();
        harmony.CreateClassProcessor(typeof(AiSpawnPatch)).Patch();
    }

    static int Main(string[] args)
    {
        var harmony = new Harmony("BALocalSkirmish.RegistrationRegression");
        try
        {
            var load = AccessTools.Method(typeof(MissionLoader), nameof(MissionLoader.LoadPlayersDeck));
            var spawn = AccessTools.Method(typeof(NodeSpawnMultiUnits), nameof(NodeSpawnMultiUnits.OnActivated));

            // Reproduce v2.6.2 with the installed Harmony library: our manual pass,
            // then MelonLoader.HarmonyInit's automatic CreateClassProcessor pass.
            Register(harmony);
            Register(harmony);
            Check(Harmony.GetPatchInfo(load).Prefixes.Count == 2, "Double registration was not reproduced.");
            Check(!Checks.RegisteredLoadSurvives(), "Expected the duplicate prefix to discard the pending AI plan.");
            Console.WriteLine("REPRODUCED v2.6.2: two load prefixes clear the AI plan before scripted spawning.");
            harmony.UnpatchSelf();

            bool manualOnly = OptsOutOfAutomaticPatching(args[0]);
            Check(manualOnly, "Built mod lacks HarmonyDontPatchAll; MelonLoader would install duplicate hooks.");
            Register(harmony);
            if (!manualOnly) Register(harmony);
            Check(Harmony.GetPatchInfo(load).Prefixes.Count == 1, "Load prefix must be installed exactly once.");
            Check(Harmony.GetPatchInfo(spawn).Prefixes.Count == 1
                && Harmony.GetPatchInfo(spawn).Postfixes.Count == 1, "Spawn hooks must be installed exactly once.");
            Check(Checks.RegisteredLoadSurvives(), "Selected AI roster did not survive load and reach the spawned request.");
            Console.WriteLine("PASS: compiled opt-out, single hook registration, live Harmony dispatch, AI plan survival, custom spawn, and input restoration.");
            return 0;
        }
        finally { harmony.UnpatchSelf(); }
    }
}
