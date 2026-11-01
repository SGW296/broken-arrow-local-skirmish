using HarmonyLib;
using Il2CppBrokenArrow.ScriptEngine.Nodes.Functions;

namespace BALocalSkirmish
{
    [HarmonyPatch(typeof(NodeGetSlotData), nameof(NodeGetSlotData.InitSolo))]
    static class NativeAiPresetInitializationPatch
    {
        static void Postfix(NodeGetSlotData __instance) { Core.Instance?.ApplyNativeAiProfile(__instance); }
    }
}
