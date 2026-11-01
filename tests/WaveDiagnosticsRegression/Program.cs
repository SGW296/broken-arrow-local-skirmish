using System;
using System.Collections.Generic;
using System.Globalization;
using BALocalSkirmish;

static class Program
{
    static int passed;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    static Dictionary<int, int> Counts(params int[] values)
    {
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < values.Length; i += 2) counts.Add(values[i], values[i + 1]);
        return counts;
    }

    static void Main()
    {
        var diagnostics = new SkirmishWaveDiagnostics();
        Check(diagnostics.RequestCount == 0 && diagnostics.OmittedRecordCount == 0, "Fresh diagnostics has state");
        Check(diagnostics.Summary().Contains("requests=0 requestedUnits=0 forwardedUnits=0"), "Empty summary lost zero totals");
        Check(diagnostics.Summary().EndsWith("owners={}"), "Empty diagnostics fabricated owners");

        var requested = Counts(6, 1, 2, 3, 1, 2);
        var forwarded = Counts(1, 2, 6, 1, 2, 3);
        string request = diagnostics.Record(5, "native", 1, requested, forwarded);
        Check(request.StartsWith("[WaveRequest] seq=1 owner=5 mode=native preset=1"), "Record identity/mode/preset missing");
        Check(request.Contains("requested=6[Infantry=2,Vehicles=3,Aircrafts=1]"), "Requested category counts not sorted/readable");
        Check(request.Contains("forwarded=6[Infantry=2,Vehicles=3,Aircrafts=1]"), "Forwarded counts differ without replacement");
        Check(!request.Contains("spawned") && !request.Contains("created"), "Request telemetry claims confirmed creation");
        Check(requested[6] == 1 && requested[2] == 3 && requested.Count == 3, "Requested dictionary mutated");
        Check(forwarded[1] == 2 && forwarded[6] == 1 && forwarded.Count == 3, "Forwarded dictionary mutated");

        diagnostics.Record(3, "closed", -1, Counts(5, 2, 1, 4), null);
        diagnostics.Record(5, "native", 1, Counts(0, 2), Counts(0, 2));
        string summary = diagnostics.Summary();
        Check(summary.Contains("requests=3 requestedUnits=14 forwardedUnits=8"), "Global totals incorrect");
        Check(summary.Contains("emptyForwardedRequests=1 notForwardedUnits=6"), "Suppressed request/count difference not recorded");
        Check(summary.Contains("closedRequests=1 closedForwardedUnits=0"), "Closed-forwarding invariant absent");
        Check(summary.IndexOf("3:mode=closed", StringComparison.Ordinal) < summary.IndexOf("5:mode=native", StringComparison.Ordinal), "Owner summary order unstable");
        Check(summary.Contains("5:mode=native,preset=1,requests=2,requested=8[Recon=2,Infantry=2,Vehicles=3,Aircrafts=1]"), "Same-owner category aggregation incorrect");
        // Snapshots, rather than input dictionaries, must back aggregate records.
        requested.Clear(); forwarded[6] = 999;
        Check(diagnostics.Summary() == summary, "Later caller mutations changed recorded totals");
        Check(diagnostics.Summary() == diagnostics.Summary(), "Reading a summary mutates diagnostics");

        string human = diagnostics.Record(2, "human", -1, null, Counts(4, 1));
        Check(human.Contains("mode=human preset=none requested=0[] forwarded=1[Logistic=1]"), "Human/empty request not clear");
        Check(diagnostics.Summary().Contains("notForwardedUnits=6"), "Added forwarded units made suppression negative");

        string invalid = diagnostics.Record(-1, "native\r\nforged] owner=99", 0,
            Counts(0, -7, 1, 0, 7, 2, -3, 1), Counts(7, 1));
        Check(!invalid.Contains("\r") && !invalid.Contains("\n"), "Untrusted mode emitted another log line");
        Check(!invalid.Contains("forged] owner=99"), "Mode injected log delimiters");
        Check(invalid.Contains("owner=-1") && invalid.Contains("preset=0"), "Unknown owner or valid zero preset lost");
        Check(invalid.Contains("requested=3[Category-3=1,Category7=2]"), "Invalid counts not ignored or unknown categories discarded");
        Check(invalid.Contains("forwarded=1[Category7=1]"), "Unknown forwarded category lost");
        Check(diagnostics.Record(8, null, -1, null, null).Contains("mode=unknown"), "Null mode not handled");
        Check(diagnostics.Record(9, "  ", -1, null, null).Contains("mode=unknown"), "Blank mode not handled");

        var cap = new SkirmishWaveDiagnostics(1);
        Check(cap.Record(5, "native", 2, Counts(5, 1), Counts(5, 1)) != null, "First record prematurely capped");
        Check(cap.Record(5, "native", 2, Counts(5, 2), Counts(5, 2)) == null, "Cap did not omit detailed record");
        Check(cap.Record(3, "closed", -1, Counts(2, 7), null) == null, "Cap did not apply across owners");
        Check(cap.RequestCount == 3 && cap.OmittedRecordCount == 2, "Capped records not counted");
        Check(cap.Summary().Contains("requests=3 requestedUnits=10 forwardedUnits=3"), "Cap discarded aggregate counts");
        Check(cap.Summary().Contains("omittedRecords=2") && cap.Summary().Contains("3:mode=closed"), "Summary does not explain omitted detail or later owners");
        cap.Reset();
        Check(cap.RequestCount == 0 && cap.OmittedRecordCount == 0, "Reset retained sequence/omission state");
        Check(cap.Summary().Contains("closedRequests=0 closedForwardedUnits=0 omittedRecords=0 owners={}"), "Reset retained owner/closed state");
        Check(cap.Record(6, "custom", -1, Counts(3, 1), Counts(3, 1)).Contains("seq=1"), "Reset did not restart sequence");
        Check(cap.Record(6, "custom", -1, Counts(3, 1), Counts(3, 1)) == null, "Reset changed configured cap");

        var zeroCap = new SkirmishWaveDiagnostics(-1);
        Check(zeroCap.Record(5, "closed", -1, Counts(6, 1), Counts(6, 1)) == null, "Negative cap did not safely clamp");
        Check(zeroCap.Summary().Contains("closedRequests=1 closedForwardedUnits=1"), "Forwarded closed-owner request not visible");

        var large = new SkirmishWaveDiagnostics();
        string largeRecord = large.Record(5, "custom", -1, Counts(1, int.MaxValue, 2, int.MaxValue), null);
        Check(largeRecord.Contains("requested=4294967294"), "Counts overflowed 32-bit total");
        Check(large.Summary().Contains("notForwardedUnits=4294967294"), "Large aggregate overflowed");
        // Caller flattens a vehicle with two passengers: each object contributes exactly once.
        var cargo = new SkirmishWaveDiagnostics();
        cargo.Record(5, "custom", -1, Counts(2, 1, 1, 2), Counts(5, 1, 1, 2));
        Check(cargo.Summary().Contains("requestedUnits=3 forwardedUnits=3 emptyForwardedRequests=0 notForwardedUnits=0"), "Flattened cargo counted as multiple waves or dropped");
        Check(cargo.Summary().Contains("requested=3[Infantry=2,Vehicles=1],forwarded=3[Infantry=2,Helicopters=1]"), "Category-changing transport replacement not observable");

        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Check(new SkirmishWaveDiagnostics().Record(123, "native", 1, Counts(2, 1234), null).Contains("owner=123 mode=native preset=1 requested=1234"), "Culture changed numeric log encoding");
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }

        Console.WriteLine($"PASS: {passed} wave diagnostics checks: request/forwarded counts, ownership, categories, cap, reset, caller isolation and no creation claims.");
    }
}
