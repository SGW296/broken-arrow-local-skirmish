using System.Collections.Generic;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppNetworkCommon.Enums;

namespace BALocalSkirmish
{
    // Called only from a launch-scoped LoadTeamsAndPlayers postfix. Native online
    // closed slots retain PlayerInfo/UID identity but do not receive active bot status.
    // Solo slot-data nodes instead inspect the cached IsBot flag, so mirror both states.
    public static class SlotRuntimeBridge
    {
        public static int ApplyClosedSlots(GameSessionContext session, IEnumerable<int> closedUids, int expectedHumanUid)
        {
            if (session == null || closedUids == null) return 0;
            int applied = 0;
            var seen = new HashSet<int>();
            var human = session.CurrentPlayer;
            foreach (int uid in closedUids)
            {
                if (uid < 0 || !seen.Add(uid) || uid == expectedHumanUid || (human != null && uid == human.UID)) continue;
                var player = session.GetPlayer(uid);
                // Read-only/mandatory/human slots cannot be silently closed by stale configuration.
                if (player == null || player.NetID > 0 || !player.IsBot || player.IsSlotMandatory) continue;
                player._isBotSlot = false;
                player._SlotStatus_k__BackingField = LobbySlotStatus.None;
                applied++;
            }
            return applied;
        }
    }
}
