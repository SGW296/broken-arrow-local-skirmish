using System;
using System.Collections.Generic;

namespace BALocalSkirmish
{
    // Input projection of existing metadata slots. This policy never adds a UID or edits a row.
    public sealed class SlotPolicyEntry
    {
        public int PlayerUid, TeamSide;
        public bool Closed, Mandatory, IsHuman;
        public bool Configurable = true;
    }

    public static class SlotSelectionPolicy
    {
        public static string Validate(List<SlotPolicyEntry> entries)
        {
            return CheckEntries(entries, true);
        }

        public static bool TrySetFormat(List<SlotPolicyEntry> entries, int playersPerSide,
            out HashSet<int> closedUids, out string error)
        {
            closedUids = null;
            if (playersPerSide < 1 || playersPerSide > 5)
            {
                error = "Choose a format from 1v1 through 5v5.";
                return false;
            }
            // A quick format can recover an otherwise valid plan with every enemy closed.
            // Human/mandatory closures and malformed identities remain errors.
            error = CheckEntries(entries, false);
            if (error != null) return false;

            var proposedClosed = new HashSet<int>();
            for (int side = 0; side <= 1; side++)
            {
                int fixedActive = 0;
                var activeOptional = new List<SlotPolicyEntry>();
                var closedOptional = new List<SlotPolicyEntry>();
                foreach (var row in entries)
                {
                    if (row.TeamSide != side) continue;
                    bool fixedRow = row.IsHuman || row.Mandatory || !row.Configurable;
                    if (fixedRow)
                    {
                        if (row.Closed) proposedClosed.Add(row.PlayerUid);
                        else fixedActive++;
                    }
                    else if (row.Closed) closedOptional.Add(row);
                    else activeOptional.Add(row);
                }
                string team = side == 0 ? "Your team" : "Opposing team";
                if (fixedActive > playersPerSide)
                {
                    error = team + " has more protected active slots than this format allows.";
                    return false;
                }
                if (fixedActive + activeOptional.Count + closedOptional.Count < playersPerSide)
                {
                    error = team + " does not have enough configurable slots for this format.";
                    return false;
                }
                // Keep active optional rows first, then reopen closed rows in metadata order.
                // This avoids switching chosen armies needlessly when a format already fits.
                int remaining = playersPerSide - fixedActive;
                foreach (var row in activeOptional)
                {
                    if (remaining > 0) remaining--;
                    else proposedClosed.Add(row.PlayerUid);
                }
                foreach (var row in closedOptional)
                {
                    if (remaining > 0) remaining--;
                    else proposedClosed.Add(row.PlayerUid);
                }
            }

            // Final validation is against a separate projection, before publishing the plan.
            var proposed = new List<SlotPolicyEntry>();
            foreach (var row in entries)
                proposed.Add(new SlotPolicyEntry {
                    PlayerUid = row.PlayerUid, TeamSide = row.TeamSide,
                    Closed = proposedClosed.Contains(row.PlayerUid), Mandatory = row.Mandatory,
                    IsHuman = row.IsHuman, Configurable = row.Configurable
                });
            error = Validate(proposed);
            if (error != null) return false;
            closedUids = proposedClosed;
            return true;
        }

        static string CheckEntries(List<SlotPolicyEntry> entries, bool requireActiveOpponent)
        {
            if (entries == null || entries.Count == 0) return "No playable slot metadata is available.";
            var seen = new HashSet<int>();
            int humans = 0, opponents = 0;
            foreach (var row in entries)
            {
                if (row == null) return "The scenario contains an unreadable slot.";
                if (row.TeamSide != 0 && row.TeamSide != 1)
                    return "Quick match setup supports Alpha and Bravo slots only.";
                if (row.PlayerUid < 0 || !seen.Add(row.PlayerUid))
                    return "The scenario contains an invalid or duplicate slot UID.";
                if (row.IsHuman)
                {
                    humans++;
                    if (row.TeamSide != 0) return "The existing offline loader requires your slot on Alpha.";
                    if (row.Closed) return "Your human slot cannot be closed.";
                }
                if (row.Mandatory && row.Closed) return "A required scenario slot cannot be closed.";
                if (row.TeamSide == 1 && !row.Closed) opponents++;
            }
            if (humans != 1) return "The offline setup requires exactly one human slot.";
            if (requireActiveOpponent && opponents == 0) return "Keep at least one opposing slot active.";
            return null;
        }
    }
}
