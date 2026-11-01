using System;
using System.Collections.Generic;
using BALocalSkirmish;

namespace Il2CppNetworkCommon.Enums
{
    public enum LobbySlotStatus { None = 0, Free = 1, Occupied = 10, Locked = 20, Inaccessible = 30, OccupiedByBot = 40 }
}
namespace Il2CppBrokenArrow.Client.Ecs.Configs
{
    using Il2CppNetworkCommon.Enums;
    public sealed class PlayerData { public bool IsBot = true, Locked; }
    public sealed class PlayerInfo
    {
        public int UID, NetID;
        public bool _isBotSlot = true, IsSlotMandatory;
        public LobbySlotStatus _SlotStatus_k__BackingField = LobbySlotStatus.OccupiedByBot;
        public PlayerData PlayerData = new PlayerData();
        // Matches verified v1.2.0 native getter, not a proposed implementation.
        public bool IsBot => _isBotSlot && NetID == 0;
    }
    public sealed class GameSessionContext
    {
        public PlayerInfo CurrentPlayer;
        public readonly Dictionary<int, PlayerInfo> Players = new Dictionary<int, PlayerInfo>();
        public PlayerInfo GetPlayer(int uid) => Players.TryGetValue(uid, out var player) ? player : null;
    }
}

static class Program
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static void Main()
    {
        var session = new Il2CppBrokenArrow.Client.Ecs.Configs.GameSessionContext();
        var human = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 2, NetID = 99 };
        var ally = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 3 };
        var opponent = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 5 };
        var mandatory = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 6, IsSlotMandatory = true };
        var otherHuman = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 10, NetID = 100 };
        var notBot = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 11, _isBotSlot = false };
        session.CurrentPlayer = human;
        foreach (var player in new[] { human, ally, opponent, mandatory, otherHuman, notBot }) session.Players.Add(player.UID, player);
        var data = ally.PlayerData;
        int count = session.Players.Count;
        int applied = SlotRuntimeBridge.ApplyClosedSlots(session, new[] { 2, 3, 3, 6, 10, 11, 999, -1 }, 2);
        Check(applied == 1, "Only one optional AI should close");
        Check(!ally.IsBot && ally._SlotStatus_k__BackingField == Il2CppNetworkCommon.Enums.LobbySlotStatus.None, "Closed AI native state not mirrored");
        Check(session.Players.Count == count && ReferenceEquals(session.GetPlayer(3), ally), "Closed slot identity removed or replaced");
        Check(ReferenceEquals(ally.PlayerData, data) && data.IsBot && !data.Locked, "Shared scenario PlayerData mutated");
        Check(opponent.IsBot && opponent._SlotStatus_k__BackingField == Il2CppNetworkCommon.Enums.LobbySlotStatus.OccupiedByBot, "Unselected AI changed");
        Check(human.NetID == 99 && human._isBotSlot, "Human changed");
        Check(mandatory.IsBot, "Mandatory slot closed");
        Check(otherHuman.NetID == 100 && otherHuman._isBotSlot, "Another human changed");
        Check(!notBot._isBotSlot && notBot._SlotStatus_k__BackingField == Il2CppNetworkCommon.Enums.LobbySlotStatus.OccupiedByBot, "Non-bot state changed");
        Check(SlotRuntimeBridge.ApplyClosedSlots(session, new[] { 3 }, 2) == 0, "Repeated apply should be idempotent");
        Check(SlotRuntimeBridge.ApplyClosedSlots(null, new[] { 3 }, 2) == 0 && SlotRuntimeBridge.ApplyClosedSlots(session, null, 2) == 0, "Null inputs not handled");
        // Before CurrentPlayer is established, the expected human UID still protects the slot.
        var early = new Il2CppBrokenArrow.Client.Ecs.Configs.GameSessionContext();
        var earlyHuman = new Il2CppBrokenArrow.Client.Ecs.Configs.PlayerInfo { UID = 2 };
        early.Players.Add(2, earlyHuman);
        Check(SlotRuntimeBridge.ApplyClosedSlots(early, new[] { 2 }, 2) == 0 && earlyHuman.IsBot, "Expected human closed before assignment");
        session.CurrentPlayer = opponent;
        Check(SlotRuntimeBridge.ApplyClosedSlots(session, new[] { 5 }, 2) == 0 && opponent.IsBot, "Runtime current-player guard failed");
        // Native InitSolo derives IsPlayer from NetID and IsAi from the cached IsBot getter.
        Check(!(ally.NetID > 0) && !ally.IsBot, "Closed slot still appears active to solo script nodes");
        Console.WriteLine($"PASS: {passed} runtime slot checks: native flags, identity retention, protected slots, no metadata mutation and idempotence.");
    }
}
