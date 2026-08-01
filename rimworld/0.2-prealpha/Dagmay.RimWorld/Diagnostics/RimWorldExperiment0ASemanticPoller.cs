using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Dagmay.RimWorld.Diagnostics
{
    internal sealed class RimWorldExperiment0ASemanticPoller
    {
        private const int MaximumThoughtsPerPoll = 1024;
        private const int MaximumPlayLogEntriesPerPoll = 512;
        private const int MaximumTalesPerPoll = 512;

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T first, T second) => ReferenceEquals(first, second);
            public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
        }

        private readonly Experiment0ATelemetry _telemetry;
        private Dictionary<Thought_Memory, long> _thoughtAges =
            new Dictionary<Thought_Memory, long>(new ReferenceComparer<Thought_Memory>());

        public RimWorldExperiment0ASemanticPoller(Experiment0ATelemetry telemetry)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public void Poll(
            IReadOnlyDictionary<string, string> enrolledIndividualIds,
            long currentGameTick)
        {
            if (enrolledIndividualIds is null)
                throw new ArgumentNullException(nameof(enrolledIndividualIds));
            if (currentGameTick < 0) throw new ArgumentOutOfRangeException(nameof(currentGameTick));

            IReadOnlyDictionary<string, Pawn> pawns;
            try
            {
                pawns = EnrolledPawns(enrolledIndividualIds);
            }
            catch (Exception)
            {
                _telemetry.RecordSourceFailure(Experiment0ASource.Thought);
                _telemetry.RecordSourceFailure(Experiment0ASource.PlayLog);
                _telemetry.RecordSourceFailure(Experiment0ASource.Tale);
                return;
            }

            PollThoughts(enrolledIndividualIds, pawns, currentGameTick);
            PollPlayLog(enrolledIndividualIds, currentGameTick);
            PollTales(enrolledIndividualIds, pawns, currentGameTick);
        }

        private void PollThoughts(
            IReadOnlyDictionary<string, string> enrolled,
            IReadOnlyDictionary<string, Pawn> pawns,
            long currentGameTick)
        {
            var current = new Dictionary<Thought_Memory, long>(
                new ReferenceComparer<Thought_Memory>());
            try
            {
                var considered = 0;
                foreach (var ownerPair in pawns.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    var memories = ownerPair.Value.needs?.mood?.thoughts?.memories?.Memories;
                    if (memories is null) continue;
                    var ordinal = 0;
                    foreach (var memory in memories.ToList())
                    {
                        if (considered >= MaximumThoughtsPerPoll) break;
                        considered++;
                        ordinal++;
                        if (!(memory is Thought_MemorySocial social)) continue;
                        var other = social.OtherPawn();
                        if (other is null ||
                            string.IsNullOrWhiteSpace(other.ThingID) ||
                            !enrolled.TryGetValue(other.ThingID, out var otherIndividualId) ||
                            string.Equals(ownerPair.Key, other.ThingID, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        if (!Experiment0AReflectedMemberReader.TryReadInt64(
                                memory,
                                out var age,
                                "age"))
                        {
                            _telemetry.RecordSourceFailure(Experiment0ASource.Thought);
                            continue;
                        }

                        current[memory] = age;
                        var isNew = !_thoughtAges.TryGetValue(memory, out var priorAge);
                        var wasRenewed = !isNew && age < priorAge;
                        if (!isNew && !wasRenewed) continue;

                        var tick = age > currentGameTick ? 0 : currentGameTick - age;
                        var key = "thought:"
                            + RuntimeHelpers.GetHashCode(memory).ToString("x8", CultureInfo.InvariantCulture)
                            + ":" + tick.ToString(CultureInfo.InvariantCulture)
                            + ":" + ordinal.ToString(CultureInfo.InvariantCulture)
                            + ":" + ownerPair.Value.ThingID;
                        _telemetry.RecordObservation(new Experiment0AObservation(
                            Experiment0ASource.Thought,
                            key,
                            tick,
                            tick,
                            new[] { enrolled[ownerPair.Key], otherIndividualId }));
                    }

                    if (considered >= MaximumThoughtsPerPoll) break;
                }

                _thoughtAges = current;
            }
            catch (Exception)
            {
                _thoughtAges = current;
                _telemetry.RecordSourceFailure(Experiment0ASource.Thought);
            }
        }

        private void PollPlayLog(
            IReadOnlyDictionary<string, string> enrolled,
            long currentGameTick)
        {
            try
            {
                var entries = Find.PlayLog?.AllEntries;
                if (entries is null)
                {
                    _telemetry.RecordSourceFailure(Experiment0ASource.PlayLog);
                    return;
                }

                foreach (var entry in entries
                    .OfType<PlayLogEntry_Interaction>()
                    .OrderByDescending(value => value.LogID)
                    .Take(MaximumPlayLogEntriesPerPoll)
                    .ToList())
                {
                    try
                    {
                        var participants = entry.GetConcerns()
                            .OfType<Pawn>()
                            .Where(pawn =>
                                pawn is not null &&
                                !string.IsNullOrWhiteSpace(pawn.ThingID) &&
                                enrolled.ContainsKey(pawn.ThingID))
                            .Select(pawn => enrolled[pawn.ThingID])
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray();
                        if (participants.Length != 2) continue;
                        var correlationTick = entry.Age > currentGameTick
                            ? 0
                            : currentGameTick - entry.Age;
                        _telemetry.RecordObservation(new Experiment0AObservation(
                            Experiment0ASource.PlayLog,
                            "playlog:" + entry.LogID.ToString(CultureInfo.InvariantCulture),
                            Math.Max(0, entry.Tick),
                            correlationTick,
                            participants));
                    }
                    catch (Exception)
                    {
                        _telemetry.RecordSourceFailure(Experiment0ASource.PlayLog);
                    }
                }
            }
            catch (Exception)
            {
                _telemetry.RecordSourceFailure(Experiment0ASource.PlayLog);
            }
        }

        private void PollTales(
            IReadOnlyDictionary<string, string> enrolled,
            IReadOnlyDictionary<string, Pawn> pawns,
            long currentGameTick)
        {
            try
            {
                var tales = Find.TaleManager?.AllTalesListForReading;
                if (tales is null)
                {
                    _telemetry.RecordSourceFailure(Experiment0ASource.Tale);
                    return;
                }

                foreach (var tale in tales
                    .OrderBy(value => value.AgeTicks)
                    .Take(MaximumTalesPerPoll)
                    .ToList())
                {
                    try
                    {
                        if (!Experiment0AReflectedMemberReader.TryReadInt64(
                                tale,
                                out var rawTick,
                                "date"))
                        {
                            _telemetry.RecordSourceFailure(Experiment0ASource.Tale);
                            continue;
                        }

                        var participants = pawns
                            .Where(pair => tale.Concerns(pair.Value))
                            .Select(pair => enrolled[pair.Key])
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .Take(8)
                            .ToArray();
                        if (participants.Length == 0) continue;
                        var correlationTick = tale.AgeTicks > currentGameTick
                            ? 0
                            : currentGameTick - tale.AgeTicks;
                        _telemetry.RecordObservation(new Experiment0AObservation(
                            Experiment0ASource.Tale,
                            "tale:" + tale.GetUniqueLoadID(),
                            rawTick,
                            correlationTick,
                            participants));
                    }
                    catch (Exception)
                    {
                        _telemetry.RecordSourceFailure(Experiment0ASource.Tale);
                    }
                }
            }
            catch (Exception)
            {
                _telemetry.RecordSourceFailure(Experiment0ASource.Tale);
            }
        }

        private static IReadOnlyDictionary<string, Pawn> EnrolledPawns(
            IReadOnlyDictionary<string, string> enrolled)
        {
            var result = new Dictionary<string, Pawn>(StringComparer.Ordinal);
            foreach (var map in Find.Maps.ToList())
            {
                if (map is null) continue;
                foreach (var pawn in map.mapPawns.AllPawns.ToList())
                    AddIfEnrolled(result, enrolled, pawn);
            }

            var worldPawns = Find.WorldPawns;
            if (worldPawns is not null)
            {
                foreach (var pawn in worldPawns.AllPawnsAliveOrDead.ToList())
                    AddIfEnrolled(result, enrolled, pawn);
            }

            return result;
        }

        private static void AddIfEnrolled(
            IDictionary<string, Pawn> result,
            IReadOnlyDictionary<string, string> enrolled,
            Pawn pawn)
        {
            if (pawn is null ||
                string.IsNullOrWhiteSpace(pawn.ThingID) ||
                !enrolled.ContainsKey(pawn.ThingID))
            {
                return;
            }

            result[pawn.ThingID] = pawn;
        }
    }
}
