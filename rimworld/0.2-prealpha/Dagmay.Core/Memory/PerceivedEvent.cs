using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Memory
{
    public enum PerceptionChannel
    {
        Experienced,
        Witnessed,
        Told,
        Inferred,
        PrivilegedTestInput
    }

    public sealed class PerceivedEvent
    {
        public PerceivedEvent(
            PerceptionId id,
            EventId sourceEventId,
            IndividualId perceiverId,
            PerceptionChannel channel,
            double confidence,
            IDictionary<string, string> availableDetails,
            IEnumerable<string> intentionallyOmittedFields,
            long stateVersionAtPerception)
        {
            if (stateVersionAtPerception < 0) throw new ArgumentOutOfRangeException(nameof(stateVersionAtPerception));

            Id = id;
            SchemaVersion = SchemaVersions.PerceivedEvent;
            SourceEventId = sourceEventId;
            PerceiverId = perceiverId;
            Channel = channel;
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            AvailableDetails = ContractGuard.Dictionary(availableDetails, nameof(availableDetails));
            IntentionallyOmittedFields = ContractGuard.List(intentionallyOmittedFields, nameof(intentionallyOmittedFields));
            StateVersionAtPerception = stateVersionAtPerception;
        }

        public PerceptionId Id { get; }
        public int SchemaVersion { get; }
        public EventId SourceEventId { get; }
        public IndividualId PerceiverId { get; }
        public PerceptionChannel Channel { get; }
        public double Confidence { get; }
        public IReadOnlyDictionary<string, string> AvailableDetails { get; }
        public IReadOnlyList<string> IntentionallyOmittedFields { get; }
        public long StateVersionAtPerception { get; }
    }
}

