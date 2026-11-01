using System;
using System.Collections.Generic;
using MelonLoader;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.ScriptEngine.Nodes.Spawn;
using Il2CppBrokenArrow.ScriptEngine.Editor.Properties;
using Il2CppBrokenArrow.Shared.Ecs.MissionEditor;

namespace BALocalSkirmish
{
    public partial class Core
    {
        readonly SkirmishWaveDiagnostics _waveDiagnostics = new SkirmishWaveDiagnostics();
        bool _diagnosticFailureLogged;

        internal void TraceSpawnDecision(NodeSpawnMultiUnits node, MultiUnits original)
        {
            if (!_deckOverridesActive) return;
            try
            {
                if (node?.PlayerOwner == null) return;
                if (!MatchesBattleScenario()) return;
                int uid = node.PlayerOwner.DataUID;
                string mode = _battleClosedSlots.Contains(uid) ? "closed"
                    : uid == _battleHumanUid ? "human-scenario"
                    : _battleAiProfiles.ContainsKey(uid) ? "native"
                    : _battleAiDecks.ContainsKey(uid) ? "custom-deck" : "unplanned";
                int preset = _battleAiProfiles.TryGetValue(uid, out int selected) ? selected : -1;
                string line = _waveDiagnostics.Record(uid, mode, preset, CountSpawnPayload(original), CountSpawnPayload(node.UnitInfo));
                if (line != null) MelonLogger.Msg(line);
            }
            catch (Exception e) { DiagnosticFailure(e); }
        }

        static Dictionary<int, int> CountSpawnPayload(MultiUnits payload)
        {
            var counts = new Dictionary<int, int>();
            void Count(UnitModData unit, int depth)
            {
                if (unit == null || depth > 8) return;
                int category = (int)unit.Category;
                counts.TryGetValue(category, out int count); counts[category] = count + 1;
                var cargo = unit.Cargo?.Slots;
                if (cargo != null) foreach (var passenger in cargo) Count(passenger, depth + 1);
            }
            if (payload?.Units != null) foreach (var unit in payload.Units) Count(unit, 0);
            return counts;
        }

        void LogRuntimeSlotSnapshot(GameSessionContext session)
        {
            try
            {
                int humanUid = session.CurrentPlayer != null ? session.CurrentPlayer.UID : -1;
                int humans = 0, aiFlags = 0, closed = 0, others = 0;
                // The generated IReadOnlyDictionary wrapper lacks enumeration on this build;
                // read the same underlying native dictionary without changing it.
                var players = session._players;
                if (players == null) return;
                foreach (var pair in players)
                {
                    var player = pair.Value;
                    if (player == null) continue;
                    bool human = player.UID == humanUid || player.NetID > 0;
                    bool ai = !human && player.IsBot;
                    bool plannedClosed = _battleClosedSlots.Contains(player.UID);
                    if (human) humans++; else if (ai) aiFlags++; else if (plannedClosed) closed++; else others++;
                    MelonLogger.Msg($"[LocalSkirmish] Runtime slot UID={player.UID} side={(int)player.TeamSide}"
                        + $" human={human} netIdPositive={player.NetID > 0} isBot={player.IsBot}"
                        + $" status={(int)player.SlotStatus} plannedClosed={plannedClosed}.");
                }
                MelonLogger.Msg($"[LocalSkirmish] Runtime snapshot: human={humans}, AI-flagged={aiFlags}, planned-closed-inactive={closed}, other={others}."
                    + " These are observed slot flags, not the scenario's resolved Auto map-size calculation.");
            }
            catch (Exception e) { DiagnosticFailure(e); }
        }

        void DiagnosticFailure(Exception error)
        {
            if (_diagnosticFailureLogged) return;
            _diagnosticFailureLogged = true;
            try { MelonLogger.Warning("[LocalSkirmish] Diagnostic read skipped; gameplay was not changed: " + error.Message); } catch { }
        }

        void FlushWaveDiagnostics()
        {
            try { if (_waveDiagnostics.RequestCount > 0) MelonLogger.Msg(_waveDiagnostics.Summary()); }
            catch (Exception e) { DiagnosticFailure(e); }
        }
    }
}
