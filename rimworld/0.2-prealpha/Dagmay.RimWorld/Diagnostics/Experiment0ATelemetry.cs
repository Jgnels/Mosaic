using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Dagmay.RimWorld.Diagnostics
{
    public enum Experiment0ASource
    {
        CurrentTrigger = 0,
        Thought = 1,
        PlayLog = 2,
        Tale = 3
    }

    public sealed class Experiment0AObservation
    {
        public Experiment0AObservation(
            Experiment0ASource source,
            string stableSourceKey,
            long rawTick,
            long correlationTick,
            IEnumerable<string> participantIds,
            int? priorHistoryDepth = null)
        {
            if (!Enum.IsDefined(typeof(Experiment0ASource), source))
                throw new ArgumentOutOfRangeException(nameof(source));
            if (string.IsNullOrWhiteSpace(stableSourceKey) || stableSourceKey.Length > 256)
                throw new ArgumentException("A bounded stable source key is required.", nameof(stableSourceKey));
            if (rawTick < 0) throw new ArgumentOutOfRangeException(nameof(rawTick));
            if (correlationTick < 0) throw new ArgumentOutOfRangeException(nameof(correlationTick));
            var participants = (participantIds ?? throw new ArgumentNullException(nameof(participantIds)))
                .Select(value => Required(value, nameof(participantIds), 128))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (participants.Length == 0 || participants.Length > 8)
                throw new ArgumentOutOfRangeException(nameof(participantIds));
            if (source != Experiment0ASource.Tale && participants.Length != 2)
                throw new ArgumentException("Pair-linked sources require exactly two participants.", nameof(participantIds));
            if (priorHistoryDepth.HasValue &&
                (source != Experiment0ASource.CurrentTrigger || priorHistoryDepth.Value < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(priorHistoryDepth));
            }

            Source = source;
            StableSourceKey = stableSourceKey;
            RawTick = rawTick;
            CorrelationTick = correlationTick;
            ParticipantIds = new ReadOnlyCollection<string>(participants);
            PriorHistoryDepth = priorHistoryDepth;
        }

        public Experiment0ASource Source { get; }
        public string StableSourceKey { get; }
        public long RawTick { get; }
        public long CorrelationTick { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public int? PriorHistoryDepth { get; }

        internal IReadOnlyList<string> PairKeys()
        {
            var pairs = new List<string>();
            for (var first = 0; first < ParticipantIds.Count; first++)
            {
                for (var second = first + 1; second < ParticipantIds.Count; second++)
                {
                    pairs.Add(ParticipantIds[first] + "|" + ParticipantIds[second]);
                }
            }

            return pairs;
        }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("A bounded participant ID is required.", parameterName);
            return value.Trim();
        }
    }

    public sealed class Experiment0ATelemetrySnapshot
    {
        internal Experiment0ATelemetrySnapshot(
            long thoughtEvents,
            long playLogEvents,
            long taleEvents,
            long triggerEvents,
            long dialoguePrepareOk,
            IReadOnlyDictionary<string, long> dialoguePrepareNotOk,
            long presentationBubble,
            long presentationPlayLog,
            long presentationNone,
            IReadOnlyDictionary<string, long> uniquePairs,
            IReadOnlyDictionary<string, long> repeatedPairs,
            IReadOnlyDictionary<string, long> triggerDepth,
            IReadOnlyDictionary<string, long> semanticDepth,
            IReadOnlyDictionary<string, long> overlap300,
            IReadOnlyDictionary<string, long> overlap600,
            IReadOnlyDictionary<string, long> overlap1200,
            IReadOnlyDictionary<string, long> sourceFailures,
            long diagnosticEpisodeOpportunities,
            long trackingSaturations,
            int recentObservationCount,
            int dedupeKeyCount,
            int trackedPairCount)
        {
            ObservedThoughtPairEvents = thoughtEvents;
            ObservedPlayLogPairEvents = playLogEvents;
            ObservedTaleEnrolledEvents = taleEvents;
            CurrentSocialTriggerEvents = triggerEvents;
            DialoguePrepareOk = dialoguePrepareOk;
            DialoguePrepareSuppressedOrFailed = dialoguePrepareNotOk;
            PresentationReceiptBubble = presentationBubble;
            PresentationReceiptPlayLog = presentationPlayLog;
            PresentationNone = presentationNone;
            UniquePairsBySource = uniquePairs;
            RepeatedPairEventsBySource = repeatedPairs;
            PriorSamePairHistoryDepthCurrentTrigger = triggerDepth;
            PriorObservedPairEpisodeDepthSemanticSource = semanticDepth;
            OverlapCounts300 = overlap300;
            OverlapCounts600 = overlap600;
            OverlapCounts1200 = overlap1200;
            SourceReadParseFailuresBySource = sourceFailures;
            SourceReadParseFailures = sourceFailures.Values.Sum();
            DiagnosticEpisodeOpportunities = diagnosticEpisodeOpportunities;
            TrackingSaturations = trackingSaturations;
            RecentObservationCount = recentObservationCount;
            DedupeKeyCount = dedupeKeyCount;
            TrackedPairCount = trackedPairCount;
        }

        public long ObservedThoughtPairEvents { get; }
        public long ObservedPlayLogPairEvents { get; }
        public long ObservedTaleEnrolledEvents { get; }
        public long CurrentSocialTriggerEvents { get; }
        public long DialoguePrepareOk { get; }
        public IReadOnlyDictionary<string, long> DialoguePrepareSuppressedOrFailed { get; }
        public long PresentationReceiptBubble { get; }
        public long PresentationReceiptPlayLog { get; }
        public long PresentationNone { get; }
        public IReadOnlyDictionary<string, long> UniquePairsBySource { get; }
        public IReadOnlyDictionary<string, long> RepeatedPairEventsBySource { get; }
        public IReadOnlyDictionary<string, long> PriorSamePairHistoryDepthCurrentTrigger { get; }
        public IReadOnlyDictionary<string, long> PriorObservedPairEpisodeDepthSemanticSource { get; }
        public IReadOnlyDictionary<string, long> OverlapCounts300 { get; }
        public IReadOnlyDictionary<string, long> OverlapCounts600 { get; }
        public IReadOnlyDictionary<string, long> OverlapCounts1200 { get; }
        public long SourceReadParseFailures { get; }
        public IReadOnlyDictionary<string, long> SourceReadParseFailuresBySource { get; }
        public long DiagnosticEpisodeOpportunities { get; }
        public long TrackingSaturations { get; }
        public int RecentObservationCount { get; }
        public int DedupeKeyCount { get; }
        public int TrackedPairCount { get; }

        public string ToLogLine(string phase, long tick)
        {
            if (string.IsNullOrWhiteSpace(phase) || phase.Length > 32)
                throw new ArgumentException("A bounded phase is required.", nameof(phase));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            return "experiment=0A"
                + ";phase=" + phase.Trim()
                + ";tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + ";observed_thought_pair_events=" + ObservedThoughtPairEvents
                + ";observed_playlog_pair_events=" + ObservedPlayLogPairEvents
                + ";observed_tale_enrolled_events=" + ObservedTaleEnrolledEvents
                + ";current_social_trigger_events=" + CurrentSocialTriggerEvents
                + ";dialogue_prepare_ok=" + DialoguePrepareOk
                + ";dialogue_prepare_suppressed_failed=" + Map(DialoguePrepareSuppressedOrFailed)
                + ";presentation_receipt_bubble=" + PresentationReceiptBubble
                + ";presentation_receipt_playlog=" + PresentationReceiptPlayLog
                + ";presentation_none=" + PresentationNone
                + ";unique_pairs_by_source=" + Map(UniquePairsBySource)
                + ";repeated_pair_events_by_source=" + Map(RepeatedPairEventsBySource)
                + ";prior_same_pair_history_depth_current_trigger=" + Map(PriorSamePairHistoryDepthCurrentTrigger)
                + ";prior_observed_pair_episode_depth_semantic_source=" + Map(PriorObservedPairEpisodeDepthSemanticSource)
                + ";overlap_counts_300=" + Map(OverlapCounts300)
                + ";overlap_counts_600=" + Map(OverlapCounts600)
                + ";overlap_counts_1200=" + Map(OverlapCounts1200)
                + ";source_read_parse_failures=" + SourceReadParseFailures
                + ";source_read_parse_failures_by_source=" + Map(SourceReadParseFailuresBySource)
                + ";diagnostic_episode_opportunities=" + DiagnosticEpisodeOpportunities
                + ";tracking_saturations=" + TrackingSaturations
                + ";recent_observations=" + RecentObservationCount
                + ";dedupe_keys=" + DedupeKeyCount
                + ";tracked_pairs=" + TrackedPairCount;
        }

        private static string Map(IReadOnlyDictionary<string, long> values) =>
            "{" + string.Join(",", values
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => value.Key + ":" + value.Value.ToString(CultureInfo.InvariantCulture))) + "}";
    }

    public sealed class Experiment0ATelemetry
    {
        public const int MaximumRecentObservations = 512;
        public const int MaximumDedupeKeysPerSource = 4096;
        public const int MaximumTrackedPairs = 4096;

        private sealed class RecentObservation
        {
            public RecentObservation(
                Experiment0ASource source,
                long rawTick,
                long correlationTick,
                IReadOnlyList<string> participantIds,
                IReadOnlyList<string> pairs)
            {
                Source = source;
                RawTick = rawTick;
                CorrelationTick = correlationTick;
                ParticipantIds = participantIds;
                Pairs = pairs;
            }

            public Experiment0ASource Source { get; }
            public long RawTick { get; }
            public long CorrelationTick { get; }
            public IReadOnlyList<string> ParticipantIds { get; }
            public IReadOnlyList<string> Pairs { get; }
        }

        private sealed class BoundedKeySet
        {
            private readonly int _capacity;
            private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
            private readonly Queue<string> _order = new Queue<string>();

            public BoundedKeySet(int capacity) => _capacity = capacity;
            public int Count => _keys.Count;

            public bool Add(string key)
            {
                if (!_keys.Add(key)) return false;
                _order.Enqueue(key);
                while (_order.Count > _capacity)
                {
                    _keys.Remove(_order.Dequeue());
                }
                return true;
            }
        }

        private readonly Dictionary<Experiment0ASource, BoundedKeySet> _dedupe;
        private readonly Dictionary<Experiment0ASource, BoundedKeySet> _uniquePairs;
        private readonly Dictionary<Experiment0ASource, long> _events;
        private readonly Dictionary<Experiment0ASource, long> _repeatedPairs;
        private readonly Dictionary<Experiment0ASource, long> _sourceFailures;
        private readonly Dictionary<string, long> _pairHistory =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Queue<RecentObservation> _recent = new Queue<RecentObservation>();
        private readonly Dictionary<string, long> _prepareNotOk =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _triggerDepth = Buckets();
        private readonly Dictionary<string, long> _semanticDepth = Buckets();
        private readonly Dictionary<string, long> _overlap300 = OverlapCounts();
        private readonly Dictionary<string, long> _overlap600 = OverlapCounts();
        private readonly Dictionary<string, long> _overlap1200 = OverlapCounts();
        private long _dialoguePrepareOk;
        private long _presentationBubble;
        private long _presentationPlayLog;
        private long _presentationNone;
        private long _diagnosticEpisodeOpportunities;
        private long _trackingSaturations;

        public Experiment0ATelemetry()
        {
            _dedupe = Sources().ToDictionary(
                source => source,
                source => new BoundedKeySet(MaximumDedupeKeysPerSource));
            _uniquePairs = Sources().ToDictionary(
                source => source,
                source => new BoundedKeySet(MaximumTrackedPairs));
            _events = Sources().ToDictionary(source => source, source => 0L);
            _repeatedPairs = Sources().ToDictionary(source => source, source => 0L);
            _sourceFailures = Sources().ToDictionary(source => source, source => 0L);
        }

        public bool RecordObservation(Experiment0AObservation observation)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            if (!_dedupe[observation.Source].Add(observation.StableSourceKey)) return false;

            Increment(_events, observation.Source);
            var pairs = observation.PairKeys();
            var overlappingPairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var previous in _recent)
            {
                if (previous.Source == observation.Source) continue;
                var shared = pairs.Intersect(previous.Pairs, StringComparer.Ordinal).ToArray();
                if (shared.Length == 0) continue;
                var distance = Distance(observation.CorrelationTick, previous.CorrelationTick);
                var sourcePair = SourcePair(observation.Source, previous.Source);
                if (distance <= 300) Increment(_overlap300, sourcePair);
                if (distance <= 600)
                {
                    Increment(_overlap600, sourcePair);
                    foreach (var pair in shared) overlappingPairs.Add(pair);
                }
                if (distance <= 1200) Increment(_overlap1200, sourcePair);
            }

            if (pairs.Count == 0 || pairs.Any(pair => !overlappingPairs.Contains(pair)))
                _diagnosticEpisodeOpportunities = Increment(_diagnosticEpisodeOpportunities);

            foreach (var pair in pairs)
            {
                var prior = _pairHistory.TryGetValue(pair, out var count) ? count : 0;
                if (observation.Source == Experiment0ASource.CurrentTrigger &&
                    observation.PriorHistoryDepth.HasValue)
                {
                    Increment(_triggerDepth, Bucket(observation.PriorHistoryDepth.Value));
                }
                else if (observation.Source != Experiment0ASource.CurrentTrigger)
                {
                    Increment(_semanticDepth, Bucket(prior));
                }

                if (!_uniquePairs[observation.Source].Add(pair))
                    Increment(_repeatedPairs, observation.Source);

                if (overlappingPairs.Contains(pair)) continue;
                if (_pairHistory.ContainsKey(pair))
                {
                    _pairHistory[pair] = Increment(_pairHistory[pair]);
                }
                else if (_pairHistory.Count < MaximumTrackedPairs)
                {
                    _pairHistory.Add(pair, 1);
                }
                else
                {
                    _trackingSaturations = Increment(_trackingSaturations);
                }
            }

            _recent.Enqueue(new RecentObservation(
                observation.Source,
                observation.RawTick,
                observation.CorrelationTick,
                new ReadOnlyCollection<string>(observation.ParticipantIds.ToArray()),
                new ReadOnlyCollection<string>(pairs.ToArray())));
            while (_recent.Count > MaximumRecentObservations) _recent.Dequeue();
            return true;
        }

        public void RecordDialoguePreparation(bool prepared, string status)
        {
            if (prepared)
            {
                _dialoguePrepareOk = Increment(_dialoguePrepareOk);
                return;
            }

            Increment(_prepareNotOk, Label(status));
        }

        public void RecordPresentation(string channel)
        {
            if (string.Equals(channel, "Bubble", StringComparison.OrdinalIgnoreCase))
                _presentationBubble = Increment(_presentationBubble);
            else if (string.Equals(channel, "PlayLog", StringComparison.OrdinalIgnoreCase))
                _presentationPlayLog = Increment(_presentationPlayLog);
            else
                _presentationNone = Increment(_presentationNone);
        }

        public void RecordSourceFailure(Experiment0ASource source)
        {
            if (!Enum.IsDefined(typeof(Experiment0ASource), source))
                throw new ArgumentOutOfRangeException(nameof(source));
            Increment(_sourceFailures, source);
        }

        public Experiment0ATelemetrySnapshot Snapshot() =>
            new Experiment0ATelemetrySnapshot(
                _events[Experiment0ASource.Thought],
                _events[Experiment0ASource.PlayLog],
                _events[Experiment0ASource.Tale],
                _events[Experiment0ASource.CurrentTrigger],
                _dialoguePrepareOk,
                Copy(_prepareNotOk),
                _presentationBubble,
                _presentationPlayLog,
                _presentationNone,
                BySource(_uniquePairs.ToDictionary(pair => pair.Key, pair => (long)pair.Value.Count)),
                BySource(_repeatedPairs),
                Copy(_triggerDepth),
                Copy(_semanticDepth),
                Copy(_overlap300),
                Copy(_overlap600),
                Copy(_overlap1200),
                BySource(_sourceFailures),
                _diagnosticEpisodeOpportunities,
                _trackingSaturations,
                _recent.Count,
                _dedupe.Values.Sum(value => value.Count),
                _pairHistory.Count);

        private static IEnumerable<Experiment0ASource> Sources() =>
            (Experiment0ASource[])Enum.GetValues(typeof(Experiment0ASource));

        private static IReadOnlyDictionary<string, long> BySource(
            IDictionary<Experiment0ASource, long> values) =>
            new ReadOnlyDictionary<string, long>(Sources().ToDictionary(
                SourceName,
                source => values.TryGetValue(source, out var value) ? value : 0L,
                StringComparer.Ordinal));

        private static IReadOnlyDictionary<string, long> Copy(IDictionary<string, long> values) =>
            new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>(values, StringComparer.Ordinal));

        private static Dictionary<string, long> Buckets() =>
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["0"] = 0,
                ["1"] = 0,
                ["2_3"] = 0,
                ["4_7"] = 0,
                ["8_plus"] = 0
            };

        private static Dictionary<string, long> OverlapCounts()
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            var sources = Sources().ToArray();
            for (var first = 0; first < sources.Length; first++)
            {
                for (var second = first + 1; second < sources.Length; second++)
                    result[SourcePair(sources[first], sources[second])] = 0;
            }
            return result;
        }

        private static string Bucket(long value) =>
            value <= 0 ? "0" :
            value == 1 ? "1" :
            value <= 3 ? "2_3" :
            value <= 7 ? "4_7" : "8_plus";

        private static string SourcePair(Experiment0ASource first, Experiment0ASource second)
        {
            var left = SourceName(first);
            var right = SourceName(second);
            return string.CompareOrdinal(left, right) <= 0
                ? left + "|" + right
                : right + "|" + left;
        }

        private static string SourceName(Experiment0ASource source)
        {
            switch (source)
            {
                case Experiment0ASource.CurrentTrigger: return "current_trigger";
                case Experiment0ASource.Thought: return "thought";
                case Experiment0ASource.PlayLog: return "playlog";
                case Experiment0ASource.Tale: return "tale";
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        private static string Label(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var result = new string(value.Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .Take(64)
                .ToArray());
            return result.Length == 0 ? "unknown" : result;
        }

        private static long Distance(long first, long second) =>
            first >= second ? first - second : second - first;

        private static long Increment(long value) =>
            value == long.MaxValue ? value : value + 1;

        private static void Increment<TKey>(IDictionary<TKey, long> values, TKey key)
        {
            values[key] = values.TryGetValue(key, out var value) ? Increment(value) : 1;
        }
    }

    public static class Experiment0AReflectedMemberReader
    {
        public static bool TryReadInt64(
            object source,
            out long value,
            params string[] memberNames)
        {
            value = 0;
            if (source is null || memberNames is null || memberNames.Length == 0) return false;
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in memberNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                try
                {
                    var type = source.GetType();
                    var property = type.GetProperty(name, flags);
                    object? raw = null;
                    if (property is not null && property.GetIndexParameters().Length == 0)
                        raw = property.GetValue(source, null);
                    else
                        raw = type.GetField(name, flags)?.GetValue(source);
                    if (raw is null) continue;
                    value = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                    return value >= 0;
                }
                catch (Exception)
                {
                    // Diagnostic reflection is fail-closed and never affects gameplay.
                }
            }

            value = 0;
            return false;
        }
    }
}
