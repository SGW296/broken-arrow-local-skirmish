using HarmonyLib;
using Il2CppBrokenArrow.Client.Ecs.Controllers;
using Il2CppBrokenArrow.Client.Ecs.Configs;

namespace BALocalSkirmish
{
    // Native constructor initializes session players here, before Load() initializes script nodes.
    [HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.LoadTeamsAndPlayers))]
    static class ClosedSlotInitializationPatch
    {
        static void Postfix(GameSessionContext __0) { Core.Instance?.ApplyClosedSlotPlan(__0); }
    }
}
