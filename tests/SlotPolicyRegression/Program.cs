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

    static List<SlotPolicyEntry> Standard(int alpha = 5, int bravo = 5)
    {
        var result = new List<SlotPolicyEntry>();
        for (int i = 0; i < alpha; i++) result.Add(new SlotPolicyEntry { PlayerUid = i + 10, TeamSide = 0, IsHuman = i == 0 });
        for (int i = 0; i < bravo; i++) result.Add(new SlotPolicyEntry { PlayerUid = i + 100, TeamSide = 1 });
        return result;
    }

    static string Snapshot(List<SlotPolicyEntry> entries)
    {
        return string.Join(";", entries.ConvertAll(r => r == null ? "null" : $"{r.PlayerUid},{r.TeamSide},{r.IsHuman},{r.Mandatory},{r.Closed},{r.Configurable}"));
    }

    static void FailedWithoutMutation(List<SlotPolicyEntry> entries, int format, string label)
    {
        string before = entries == null ? null : Snapshot(entries);
        bool success = SlotSelectionPolicy.TrySetFormat(entries, format, out var closed, out var error);
        Check(!success && closed == null && !string.IsNullOrWhiteSpace(error), label + " did not fail cleanly");
        Check(entries == null || before == Snapshot(entries), label + " mutated inputs");
    }

    static void Main()
    {
        for (int format = 1; format <= 5; format++)
        {
            var rows = Standard(); string before = Snapshot(rows);
            Check(SlotSelectionPolicy.TrySetFormat(rows, format, out var closed, out var error) && error == null, "Valid format rejected");
            Check(closed.Count == 10 - format * 2 && !closed.Contains(10), "Wrong format count or human closed");
            int alpha = 0, bravo = 0;
            foreach (var row in rows)
            {
                if (!closed.Contains(row.PlayerUid)) { if (row.TeamSide == 0) alpha++; else bravo++; }
            }
            Check(alpha == format && bravo == format, "Unbalanced symmetric format");
            Check(before == Snapshot(rows), "Successful plan mutated input rows");
        }

        var asymmetric = Standard();
        asymmetric[1].Closed = asymmetric[2].Closed = asymmetric[3].Closed = asymmetric[4].Closed = true;
        asymmetric[6].Closed = asymmetric[7].Closed = true;
        Check(SlotSelectionPolicy.Validate(asymmetric) == null, "Valid 1v3 manual plan rejected");
        asymmetric[5].Closed = asymmetric[8].Closed = asymmetric[9].Closed = true;
        Check(SlotSelectionPolicy.Validate(asymmetric) != null, "No active enemy accepted");
        Check(SlotSelectionPolicy.TrySetFormat(asymmetric, 2, out var reopened, out _)
            && !reopened.Contains(100) && !reopened.Contains(101), "Quick format failed to reopen closed opponents");

        var mandatory = Standard(); mandatory[4].Mandatory = true; mandatory[9].Mandatory = true;
        Check(SlotSelectionPolicy.TrySetFormat(mandatory, 2, out var protectedPlan, out _)
            && !protectedPlan.Contains(10) && !protectedPlan.Contains(14) && !protectedPlan.Contains(104), "Protected rows were closed");
        FailedWithoutMutation(mandatory, 1, "Mandatory count exceeds format");
        mandatory[4].Closed = true;
        Check(SlotSelectionPolicy.Validate(mandatory) != null, "Closed mandatory row accepted");
        FailedWithoutMutation(mandatory, 5, "Closed mandatory input");

        var fixedRows = Standard(); fixedRows[1].Configurable = false; fixedRows[6].Configurable = false; fixedRows[6].Closed = true;
        Check(SlotSelectionPolicy.TrySetFormat(fixedRows, 2, out var fixedPlan, out _)
            && !fixedPlan.Contains(11) && fixedPlan.Contains(101), "Nonconfigurable state changed");
        FailedWithoutMutation(fixedRows, 1, "Fixed active rows exceed format");
        FailedWithoutMutation(fixedRows, 5, "Fixed closed slot was illegally reopened");

        var retained = Standard(); retained[1].Closed = true; retained[5].Closed = true;
        Check(SlotSelectionPolicy.TrySetFormat(retained, 2, out var stable, out _)
            && stable.Contains(11) && !stable.Contains(12) && stable.Contains(100) && !stable.Contains(101), "Format needlessly switched active armies");

        var humanClosed = Standard(); humanClosed[0].Closed = true;
        Check(SlotSelectionPolicy.Validate(humanClosed) != null, "Closed human accepted");
        FailedWithoutMutation(humanClosed, 5, "Closed human");
        var secondHuman = Standard(); secondHuman[1].IsHuman = true;
        FailedWithoutMutation(secondHuman, 5, "Multiple humans");
        var noHuman = Standard(); noHuman[0].IsHuman = false;
        FailedWithoutMutation(noHuman, 5, "No human");
        var wrongSide = Standard(); wrongSide[0].IsHuman = false; wrongSide[5].IsHuman = true;
        FailedWithoutMutation(wrongSide, 5, "Human on Bravo");
        var unknown = Standard(); unknown[1].TeamSide = 100;
        FailedWithoutMutation(unknown, 5, "Spectator/unknown side");
        var duplicate = Standard(); duplicate[1].PlayerUid = duplicate[0].PlayerUid;
        FailedWithoutMutation(duplicate, 5, "Duplicate UID");
        var negative = Standard(); negative[1].PlayerUid = -1;
        FailedWithoutMutation(negative, 5, "Invalid UID");
        var unreadable = Standard(); unreadable[1] = null;
        FailedWithoutMutation(unreadable, 5, "Null slot");
        FailedWithoutMutation(null, 5, "Null metadata");
        FailedWithoutMutation(new List<SlotPolicyEntry>(), 5, "Empty metadata");
        FailedWithoutMutation(Standard(), 0, "Zero format");
        FailedWithoutMutation(Standard(), 6, "Overlarge quick format");
        FailedWithoutMutation(Standard(2, 3), 4, "Format beyond metadata capacity");
        FailedWithoutMutation(Standard(1, 0), 1, "No opponent metadata");
        Check(SlotSelectionPolicy.TrySetFormat(Standard(1, 1), 1, out var minimal, out _) && minimal.Count == 0, "Minimal 1v1 rejected");
        Console.WriteLine($"PASS: {passed} slot policy checks covering 1v1–5v5, asymmetric plans, protected rows, identity validation and atomic failure.");
    }
}
