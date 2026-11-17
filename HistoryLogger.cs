using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BetrayalHelper
{
    /// <summary>One member as read from game memory.</summary>
    public sealed class HistoryMember
    {
        public string Job = "None";
        public int Rank;
        public bool Leader;
        public int PrisonTurns;
    }

    /// <summary>Whole board at one moment. Relations are "A|B|Status" with A &lt; B (case-insensitive).</summary>
    public sealed class BoardSnapshot
    {
        public Dictionary<string, HistoryMember> Members = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Intel = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Relations = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Append-only history of what happens on the Syndicate board, plus running counters.
    /// Files (folder: ConfigDirectory/history):
    ///   board.jsonl      baseline + every detected change (member job/rank/leader/prison, division intel, relations)
    ///   decisions.jsonl  every encounter decision: event, all outcome groups, recommendation, settings
    ///   plan.jsonl       plan-vs-reality snapshots (Farm placement/ranks, intel, relation groups, prisoners)
    ///   stats.json       running counters (decisions per action, changes per field, ...)
    /// Changes carry the id of the last event (eventId) when they happen within 15 s of it, so the effect
    /// of a choice can be joined to the recommendation that was shown.
    /// </summary>
    public sealed class HistoryLogger
    {
        private readonly string _dir;
        private readonly string _statsPath;
        private readonly object _lock = new();
        private readonly Dictionary<string, long> _counters = new();
        private BoardSnapshot? _prev;
        private long _eventId;
        private string _lastEventKey = "";
        private string _lastDecisionSignature = "";
        private bool _eventOpen;
        private long _lastEventTick;
        private long _lastStatsSave;

        public string Directory => _dir;
        public long EventId => _eventId;
        public IReadOnlyDictionary<string, long> Counters => _counters;

        public HistoryLogger(string baseDir)
        {
            _dir = Path.Combine(baseDir, "history");
            _statsPath = Path.Combine(_dir, "stats.json");
            try
            {
                System.IO.Directory.CreateDirectory(_dir);
                if (File.Exists(_statsPath))
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(_statsPath));
                    if (loaded != null)
                        foreach (var kv in loaded) _counters[kv.Key] = kv.Value;
                }
            }
            catch { }
            Count("sessions");
        }

        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff");

        private void Write(string file, object record)
        {
            try
            {
                string line = JsonSerializer.Serialize(record);
                lock (_lock) File.AppendAllText(Path.Combine(_dir, file), line + Environment.NewLine);
            }
            catch { }
        }

        public void Count(string key, long n = 1)
        {
            lock (_lock) _counters[key] = (_counters.TryGetValue(key, out var v) ? v : 0) + n;
        }

        public void SaveStats(bool force = false)
        {
            long now = Environment.TickCount64;
            if (!force && now - _lastStatsSave < 60_000) return;
            _lastStatsSave = now;
            try
            {
                lock (_lock)
                    File.WriteAllText(_statsPath, JsonSerializer.Serialize(_counters.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value), new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        /// <summary>Called when a choice window is on screen. Returns the event id (new id when the event changed).</summary>
        public long BeginEvent(string key)
        {
            if (key != _lastEventKey)
            {
                _eventId++;
                _lastEventKey = key;
            }
            _eventOpen = true;
            _lastEventTick = Environment.TickCount64;
            return _eventId;
        }

        public void EventClosed()
        {
            if (!_eventOpen) return;
            _eventOpen = false;
            _lastEventTick = Environment.TickCount64;
            Write("decisions.jsonl", new { t = Now(), type = "event_closed", eventId = _eventId });
            Count("events.closed");
        }

        public void RecordDecision(string signature, object record, string recommended, string eventAction)
        {
            if (signature == _lastDecisionSignature) return;
            _lastDecisionSignature = signature;
            Write("decisions.jsonl", record);
            Count("decisions.total");
            Count("decisions.recommended." + recommended);
            Count("decisions.event." + eventAction + ".recommended." + recommended);
        }

        public void RecordPlan(object plan)
        {
            Write("plan.jsonl", new { t = Now(), eventId = _eventId, plan });
        }

        /// <summary>Compares the board with the previous one and logs every difference. Returns the number of changes.</summary>
        public int RecordBoard(BoardSnapshot cur, string area)
        {
            int changes = 0;
            long sinceEvent = (_eventOpen || _eventId > 0) ? (Environment.TickCount64 - _lastEventTick) / 1000 : long.MaxValue;
            long attach = (_eventOpen || sinceEvent <= 15) ? _eventId : 0;

            void Change(string field, string who, object? from, object? to)
            {
                changes++;
                Write("board.jsonl", new { t = Now(), type = "change", field, who, from, to, eventId = attach, area });
                Count("changes." + field);
            }

            if (_prev == null)
            {
                Write("board.jsonl", new
                {
                    t = Now(),
                    type = "baseline",
                    area,
                    members = cur.Members.Select(m => new { name = m.Key, job = m.Value.Job, rank = m.Value.Rank, leader = m.Value.Leader, prison = m.Value.PrisonTurns }),
                    intel = cur.Intel,
                    relations = cur.Relations.Count
                });
                _prev = cur;
                return 0;
            }

            foreach (var kv in cur.Members)
            {
                if (!_prev.Members.TryGetValue(kv.Key, out var old))
                {
                    Change("member_appeared", kv.Key, null, kv.Value.Job);
                    continue;
                }
                var n = kv.Value;
                if (!string.Equals(old.Job, n.Job, StringComparison.OrdinalIgnoreCase)) Change("job", kv.Key, old.Job, n.Job);
                if (old.Rank != n.Rank) Change("rank", kv.Key, old.Rank, n.Rank);
                if (old.Leader != n.Leader) Change("leader", kv.Key, old.Leader, n.Leader);
                if (old.PrisonTurns != n.PrisonTurns) Change("prison", kv.Key, old.PrisonTurns, n.PrisonTurns);
            }
            foreach (var kv in _prev.Members)
                if (!cur.Members.ContainsKey(kv.Key)) Change("member_disappeared", kv.Key, kv.Value.Job, null);

            foreach (var kv in cur.Intel)
                if (_prev.Intel.TryGetValue(kv.Key, out var oi) && oi != kv.Value) Change("intel", kv.Key, oi, kv.Value);

            foreach (var r in cur.Relations.Where(r => !_prev.Relations.Contains(r)))
                Change("relation_added", r, null, null);
            foreach (var r in _prev.Relations.Where(r => !cur.Relations.Contains(r)))
                Change("relation_removed", r, null, null);

            _prev = cur;
            return changes;
        }
    }
}
