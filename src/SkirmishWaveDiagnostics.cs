using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BALocalSkirmish
{
    // Records the input forwarded to the native spawn method, not successful unit creation.
    // Cargo has already been flattened and counted by the caller. This class owns no game objects.
    public sealed class SkirmishWaveDiagnostics
    {
        sealed class OwnerTotals
        {
            public string Mode = "unknown";
            public int ProfileId = -1;
            public long Requests, Requested, Forwarded, EmptyForwarded, NotForwarded;
            public long ClosedRequests, ClosedForwarded;
            public readonly SortedDictionary<int, long> RequestedCategories = new SortedDictionary<int, long>();
            public readonly SortedDictionary<int, long> ForwardedCategories = new SortedDictionary<int, long>();
        }

        readonly int _maxRecords;
        readonly SortedDictionary<int, OwnerTotals> _owners = new SortedDictionary<int, OwnerTotals>();
        long _requested, _forwarded, _emptyForwarded, _notForwarded, _closedRequests, _closedForwarded;

        public long RequestCount { get; private set; }
        public long OmittedRecordCount { get; private set; }

        public SkirmishWaveDiagnostics(int maxRecords = 4000)
        {
            _maxRecords = Math.Max(0, maxRecords);
        }

        public string Record(int ownerUid, string mode, int profileId,
            Dictionary<int, int> requestedByCategory, Dictionary<int, int> forwardedByCategory)
        {
            var requested = Snapshot(requestedByCategory);
            var forwarded = Snapshot(forwardedByCategory);
            long requestTotal = Total(requested), forwardTotal = Total(forwarded);
            long notForwarded = Math.Max(0L, requestTotal - forwardTotal);
            bool emptyForwarded = requestTotal > 0 && forwardTotal == 0;
            string safeMode = SafeMode(mode);
            bool closed = string.Equals(safeMode, "closed", StringComparison.OrdinalIgnoreCase);

            RequestCount++;
            _requested += requestTotal; _forwarded += forwardTotal; _notForwarded += notForwarded;
            if (emptyForwarded) _emptyForwarded++;
            if (closed) { _closedRequests++; _closedForwarded += forwardTotal; }
            if (!_owners.TryGetValue(ownerUid, out var owner))
            {
                owner = new OwnerTotals(); _owners.Add(ownerUid, owner);
            }
            owner.Mode = safeMode; owner.ProfileId = profileId; owner.Requests++;
            owner.Requested += requestTotal; owner.Forwarded += forwardTotal; owner.NotForwarded += notForwarded;
            if (emptyForwarded) owner.EmptyForwarded++;
            if (closed) { owner.ClosedRequests++; owner.ClosedForwarded += forwardTotal; }
            AddCategories(owner.RequestedCategories, requested);
            AddCategories(owner.ForwardedCategories, forwarded);

            if (RequestCount > _maxRecords)
            {
                OmittedRecordCount++;
                return null;
            }
            return "[WaveRequest] seq=" + Number(RequestCount) + " owner=" + Number(ownerUid)
                + " mode=" + safeMode + " preset=" + Preset(profileId)
                + " requested=" + Number(requestTotal) + Categories(requested)
                + " forwarded=" + Number(forwardTotal) + Categories(forwarded);
        }

        public string Summary()
        {
            var text = new StringBuilder("[WaveSummary] requests=");
            text.Append(Number(RequestCount)).Append(" requestedUnits=").Append(Number(_requested))
                .Append(" forwardedUnits=").Append(Number(_forwarded))
                .Append(" emptyForwardedRequests=").Append(Number(_emptyForwarded))
                .Append(" notForwardedUnits=").Append(Number(_notForwarded))
                .Append(" closedRequests=").Append(Number(_closedRequests))
                .Append(" closedForwardedUnits=").Append(Number(_closedForwarded))
                .Append(" omittedRecords=").Append(Number(OmittedRecordCount)).Append(" owners={");
            bool first = true;
            foreach (var pair in _owners)
            {
                if (!first) text.Append(';');
                first = false;
                var owner = pair.Value;
                text.Append(Number(pair.Key)).Append(":mode=").Append(owner.Mode)
                    .Append(",preset=").Append(Preset(owner.ProfileId))
                    .Append(",requests=").Append(Number(owner.Requests))
                    .Append(",requested=").Append(Number(owner.Requested)).Append(Categories(owner.RequestedCategories))
                    .Append(",forwarded=").Append(Number(owner.Forwarded)).Append(Categories(owner.ForwardedCategories))
                    .Append(",emptyForwarded=").Append(Number(owner.EmptyForwarded))
                    .Append(",notForwarded=").Append(Number(owner.NotForwarded))
                    .Append(",closedRequests=").Append(Number(owner.ClosedRequests))
                    .Append(",closedForwarded=").Append(Number(owner.ClosedForwarded));
            }
            return text.Append('}').ToString();
        }

        public void Reset()
        {
            RequestCount = OmittedRecordCount = 0;
            _requested = _forwarded = _emptyForwarded = _notForwarded = _closedRequests = _closedForwarded = 0;
            _owners.Clear();
        }

        static SortedDictionary<int, long> Snapshot(Dictionary<int, int> counts)
        {
            var copy = new SortedDictionary<int, long>();
            if (counts != null)
                foreach (var pair in counts) if (pair.Value > 0) copy.Add(pair.Key, pair.Value);
            return copy;
        }

        static long Total(SortedDictionary<int, long> counts)
        {
            long total = 0;
            foreach (var pair in counts) total += pair.Value;
            return total;
        }

        static void AddCategories(SortedDictionary<int, long> target, SortedDictionary<int, long> source)
        {
            foreach (var pair in source)
            {
                target.TryGetValue(pair.Key, out long count);
                target[pair.Key] = count + pair.Value;
            }
        }

        static string SafeMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "unknown";
            var text = new StringBuilder();
            foreach (char c in mode.Trim())
            {
                if (text.Length == 64) break;
                text.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
            }
            return text.ToString();
        }

        static string Preset(int id) => id < 0 ? "none" : Number(id);
        static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        static string Categories(SortedDictionary<int, long> counts)
        {
            var text = new StringBuilder("[");
            bool first = true;
            foreach (var pair in counts)
            {
                if (!first) text.Append(',');
                first = false;
                text.Append(CategoryName(pair.Key)).Append('=').Append(Number(pair.Value));
            }
            return text.Append(']').ToString();
        }

        static string CategoryName(int value)
        {
            switch (value)
            {
                case 0: return "Recon";
                case 1: return "Infantry";
                case 2: return "Vehicles";
                case 3: return "Support";
                case 4: return "Logistic";
                case 5: return "Helicopters";
                case 6: return "Aircrafts";
                default: return "Category" + Number(value);
            }
        }
    }
}
