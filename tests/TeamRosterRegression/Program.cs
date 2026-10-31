using System;
using System.Collections.Generic;
using BALocalSkirmish;

static class Program
{
    static int passed;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    static void Main()
    {
        Check(TeamRoster.Build(null).Count == 0, "Null input fabricated teams");
        Check(TeamRoster.Build(new List<RosterEntry>()).Count == 0, "Empty input fabricated teams");
        Check(TeamRoster.Build(new List<RosterEntry> { null }).Count == 0, "Null slot fabricated a row");
        var input = new List<RosterEntry> {
            new RosterEntry { TeamSide = 7, PlayerUid = 70, Name = "Neutral", AiIndex = -1 },
            new RosterEntry { TeamSide = 1, TeamName = "Bravo", PlayerUid = 5, Name = "Enemy", AiIndex = 4 },
            new RosterEntry { TeamSide = 0, TeamName = "Alpha", PlayerUid = 2, Name = "You", IsHuman = true },
            new RosterEntry { TeamSide = 1, TeamName = "Bravo", PlayerUid = 6, Name = "Enemy", AiIndex = 5 },
            new RosterEntry { TeamSide = 0, TeamName = "Alpha", PlayerUid = 3, Name = "Ally", AiIndex = 0 },
            new RosterEntry { TeamSide = 3, TeamName = "Support", PlayerUid = 30, Name = "Scripted", AiIndex = -1 }
        };
        var teams = TeamRoster.Build(input);
        Check(teams.Count == 4, "Sides were collapsed or invented");
        Check(teams[0].TeamSide == 0 && teams[1].TeamSide == 1 && teams[2].TeamSide == 7 && teams[3].TeamSide == 3,
            "Alpha/Bravo priority or extra-side discovery order changed");
        Check(teams[0].Entries[0].IsHuman && teams[0].Entries[0].PlayerUid == 2, "Human row identity changed");
        Check(teams[0].Entries[0].AiIndex == -1 && teams[0].Entries[1].AiIndex == 0, "Configurable slot mapping changed");
        Check(teams[1].Entries.Count == 2 && teams[1].Entries[0].PlayerUid == 5 && teams[1].Entries[1].PlayerUid == 6,
            "Duplicate names were deduplicated or metadata order changed");
        Check(teams[2].Entries[0].AiIndex == -1 && teams[3].Entries[0].Name == "Scripted", "Read-only rows disappeared");
        Check(teams[0].Title == "Your team · Alpha" && teams[1].Title == "Opposing team · Bravo", "Team roles incorrect");
        Check(teams[2].Title == "Scenario team · Team 7" && teams[3].Title == "Scenario team · Support", "Extra sides mislabeled as opponents");
        Check(input.Count == 6 && input[0].TeamSide == 7 && input[2].Name == "You", "Input list/metadata mutated");
        teams[0].Entries[0].Name = "Presentation copy";
        Check(input[2].Name == "You", "Output entry shares mutable source object");

        var missing = TeamRoster.Build(new List<RosterEntry> {
            new RosterEntry { TeamSide = 0, Name = null },
            new RosterEntry { TeamSide = 0, TeamName = "Later metadata label", Name = "" },
            new RosterEntry { TeamSide = 1, TeamName = " " },
        });
        Check(missing[0].Name == "Later metadata label" && missing[1].Name == "Bravo", "Missing team name fallback incorrect");
        Check(missing[0].Entries[0].Name == null && missing[0].Entries[1].Name == "", "Raw slot names were rewritten");
        Check(!missing[0].Entries.Exists(r => r.IsHuman), "Human row was synthesized");

        var stock = new List<RosterEntry>();
        for (int i = 0; i < 5; i++) stock.Add(new RosterEntry { TeamSide = 0, PlayerUid = i + 2, IsHuman = i == 0, AiIndex = i - 1 });
        for (int i = 0; i < 5; i++) stock.Add(new RosterEntry { TeamSide = 1, PlayerUid = i + 20, AiIndex = i + 4 });
        var stockTeams = TeamRoster.Build(stock);
        Check(TeamRoster.PageCount(stockTeams[0]) == 1 && TeamRoster.PageCount(stockTeams[1]) == 1, "Stock rosters should fit one page");
        Check(TeamRoster.GetPage(stockTeams[0], 0).Count == 5 && TeamRoster.GetPage(stockTeams[1], 0).Count == 5, "Stock slot hidden by pagination");
        stockTeams[1].Entries.Add(new RosterEntry { PlayerUid = 25, AiIndex = 9 });
        Check(TeamRoster.PageCount(stockTeams[1]) == 2, "Sixth row did not create another page");
        Check(TeamRoster.GetPage(stockTeams[1], 1).Count == 1 && TeamRoster.GetPage(stockTeams[1], 1)[0].PlayerUid == 25, "Last page lost row identity");
        Check(TeamRoster.GetPage(stockTeams[1], -10)[0].PlayerUid == 20, "Negative page not clamped");
        Check(TeamRoster.GetPage(stockTeams[1], int.MaxValue)[0].PlayerUid == 25, "Large page not clamped");
        Check(TeamRoster.PageCount(null) == 1 && TeamRoster.GetPage(null, 0).Count == 0, "Null team pagination");
        Check(TeamRoster.PageCount(new RosterTeam()) == 1 && TeamRoster.GetPage(new RosterTeam(), 0).Count == 0, "Empty team fabricated a slot");
        Console.WriteLine($"PASS: {passed} roster checks: side grouping, raw metadata order, read-only slots, identity, copies, and five-row paging.");
    }
}
