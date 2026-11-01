using System;
using System.Collections.Generic;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.ScriptEngine.Nodes.Functions;

namespace BALocalSkirmish
{
    // A caller-scoped InitSolo postfix invokes this after native UID resolution. The
    // built-in v1.2.0 graphs consume AiPresetId directly to select their existing armies.
    public static class NativeAiProfileBridge
    {
        public static bool ApplyPreset(NodeGetSlotData node, GameSessionContext session,
            IReadOnlyDictionary<int, int> profiles, int protectedHumanUid)
        {
            if (node == null || session == null || profiles == null || protectedHumanUid < 0) return false;
            try
            {
                var human = session.CurrentPlayer;
                var player = node._slotSoloPlayer;
                if (human == null || human.UID != protectedHumanUid || player == null) return false;
                int uid = player.UID;
                if (uid == protectedHumanUid || player.NetID > 0 || !player.IsBot || !node.IsAi || node.IsPlayer) return false;
                var sessionPlayer = session.GetPlayer(uid);
                if (sessionPlayer == null || sessionPlayer.Pointer != player.Pointer) return false;
                if (!profiles.TryGetValue(uid, out int profileId) || profileId < 0 || profileId > 11) return false;
                node.AiPresetId = profileId;
                return true;
            }
            catch { return false; }
        }
    }
}
