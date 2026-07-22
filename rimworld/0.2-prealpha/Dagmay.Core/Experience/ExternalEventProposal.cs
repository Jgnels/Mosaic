using System;
using System.Collections.Generic;
using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Experience
{
    /// <summary>
    /// Portable untrusted event proposed by an environment/mod adapter.
    /// Durable state contains no live object owned by another mod.
    /// </summary>
    public sealed class ExternalEventProposal
    {
        public ExternalEventProposal(
            string sourceModId,
            string eventKind,
            EvidenceDomain domain,
            long observedAtTick,
            IEnumerable<IndividualId> participantIds,
            string summary,
            IReadOnlyDictionary<string, string>? attributes = null)
        {
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));
            SourceModId = ContractGuard.Text(sourceModId, nameof(sourceModId), 256);
            EventKind = ContractGuard.Text(eventKind, nameof(eventKind), 256);
            Domain = domain;
            ObservedAtTick = observedAtTick;
            ParticipantIds = ContractGuard.List(participantIds, nameof(participantIds));
            Summary = ContractGuard.Text(summary, nameof(summary), 2048);
            Attributes = attributes is null ? new Dictionary<string, string>(StringComparer.Ordinal) : Copy(attributes);
        }

        public string SourceModId { get; }
        public string EventKind { get; }
        public EvidenceDomain Domain { get; }
        public long ObservedAtTick { get; }
        public IReadOnlyList<IndividualId> ParticipantIds { get; }
        public string Summary { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }

        private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source)
                result[ContractGuard.Text(pair.Key, nameof(source), 128)] = ContractGuard.Text(pair.Value, nameof(source), 1024);
            return result;
        }
    }

    public interface IExternalEventAdapter
    {
        string SourceModId { get; }
        bool IsAvailable { get; }
    }
}
