using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Memory
{
    public sealed class EnvironmentEvent
    {
        public EnvironmentEvent(
            EventId id,
            string deduplicationKey,
            string kind,
            string environment,
            DateTimeOffset occurredAtUtc,
            DateTimeOffset observedAtUtc,
            long? gameTick,
            string source,
            IDictionary<string, string> factualPayload,
            IEnumerable<IndividualId> subjects)
        {
            Id = id;
            SchemaVersion = SchemaVersions.EnvironmentEvent;
            DeduplicationKey = ContractGuard.Text(deduplicationKey, nameof(deduplicationKey), 512);
            Kind = ContractGuard.Text(kind, nameof(kind), 128);
            Environment = ContractGuard.Text(environment, nameof(environment), 128);
            OccurredAtUtc = occurredAtUtc;
            ObservedAtUtc = observedAtUtc;
            GameTick = gameTick;
            Source = ContractGuard.Text(source, nameof(source), 512);
            FactualPayload = ContractGuard.Dictionary(factualPayload, nameof(factualPayload));
            Subjects = ContractGuard.List(subjects, nameof(subjects));
        }

        public EventId Id { get; }
        public int SchemaVersion { get; }
        public string DeduplicationKey { get; }
        public string Kind { get; }
        public string Environment { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public long? GameTick { get; }
        public string Source { get; }
        public IReadOnlyDictionary<string, string> FactualPayload { get; }
        public IReadOnlyList<IndividualId> Subjects { get; }
    }
}

