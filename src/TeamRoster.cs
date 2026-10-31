using System;
using System.Collections.Generic;

namespace BALocalSkirmish
{
    // A projection of the scenario's slots, not a new player/team configuration.
    public sealed class RosterEntry
    {
        public string Name, TeamName;
        public int TeamSide, PlayerUid, AiIndex = -1;
        public bool IsHuman;
    }

    public sealed class RosterTeam
    {
        public int TeamSide;
        public string Name, Title;
        public List<RosterEntry> Entries = new List<RosterEntry>();
    }

    public static class TeamRoster
    {
        public const int RowsPerPage = 5;

        public static List<RosterTeam> Build(List<RosterEntry> slots)
        {
            var teams = new List<RosterTeam>();
            var bySide = new Dictionary<int, RosterTeam>();
            if (slots == null) return teams;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (!bySide.TryGetValue(slot.TeamSide, out var team))
                {
                    team = new RosterTeam { TeamSide = slot.TeamSide };
                    bySide.Add(slot.TeamSide, team);
                    teams.Add(team);
                }
                if (string.IsNullOrWhiteSpace(team.Name) && !string.IsNullOrWhiteSpace(slot.TeamName))
                    team.Name = slot.TeamName;
                // Own copies keep presentation changes from altering the source metadata projection.
                // Keep duplicate names/UIDs and non-configurable rows: only the caller knows legality.
                team.Entries.Add(new RosterEntry {
                    Name = slot.Name, TeamName = slot.TeamName, TeamSide = slot.TeamSide,
                    PlayerUid = slot.PlayerUid, AiIndex = slot.AiIndex, IsHuman = slot.IsHuman
                });
            }
            foreach (var team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.Name))
                    team.Name = team.TeamSide == 0 ? "Alpha" : team.TeamSide == 1 ? "Bravo" : "Team " + team.TeamSide;
                string role = team.TeamSide == 0 ? "Your team" : team.TeamSide == 1 ? "Opposing team" : "Scenario team";
                team.Title = role + " · " + team.Name;
            }
            // Prioritize the loader's Alpha/Bravo sides; retain discovery order for other sides.
            var ordered = new List<RosterTeam>();
            if (bySide.TryGetValue(0, out var alpha)) ordered.Add(alpha);
            if (bySide.TryGetValue(1, out var bravo)) ordered.Add(bravo);
            foreach (var team in teams) if (team.TeamSide != 0 && team.TeamSide != 1) ordered.Add(team);
            return ordered;
        }

        public static int PageCount(RosterTeam team)
        {
            int count = team?.Entries?.Count ?? 0;
            return count == 0 ? 1 : 1 + (count - 1) / RowsPerPage;
        }

        public static List<RosterEntry> GetPage(RosterTeam team, int page)
        {
            var rows = new List<RosterEntry>();
            if (team?.Entries == null) return rows;
            int bounded = Math.Max(0, Math.Min(page, PageCount(team) - 1));
            int first = bounded * RowsPerPage;
            for (int i = first; i < Math.Min(first + RowsPerPage, team.Entries.Count); i++)
                rows.Add(team.Entries[i]);
            return rows;
        }
    }
}
