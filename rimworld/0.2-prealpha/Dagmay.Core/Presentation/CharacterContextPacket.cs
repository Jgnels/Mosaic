using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Presentation
{
    public sealed class CharacterContextItem
    {
        public CharacterContextItem(
            string kind,
            string text,
            IEnumerable<EventId> evidenceIds,
            double relevance)
        {
            Kind = ContractGuard.Text(kind, nameof(kind), 128);
            Text = ContractGuard.Text(text, nameof(text), 2048);
            EvidenceIds = ContractGuard.List(evidenceIds, nameof(evidenceIds));
            Relevance = ContractGuard.UnitInterval(relevance, nameof(relevance));

            if (EvidenceIds.Count == 0 || EvidenceIds.Count > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceIds),
                    "Grounded context items require between 1 and 100 supporting EvidenceIds.");
            var uniqueEvidence = new HashSet<EventId>();
            foreach (var evidenceId in EvidenceIds)
            {
                if (evidenceId.Value == Guid.Empty)
                    throw new ArgumentException("Evidence IDs cannot be empty.", nameof(evidenceIds));
                if (!uniqueEvidence.Add(evidenceId))
                    throw new ArgumentException("Evidence IDs cannot contain duplicates.", nameof(evidenceIds));
            }
        }

        public string Kind { get; }
        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public double Relevance { get; }
    }

    public sealed class CharacterContextPacket
    {
        public CharacterContextPacket(
            IndividualId individualId,
            long builtAtTick,
            IEnumerable<CharacterContextItem> items)
        {
            if (builtAtTick < 0) throw new ArgumentOutOfRangeException(nameof(builtAtTick));
            if (individualId.Value == Guid.Empty) throw new ArgumentException("Individual ID cannot be empty.", nameof(individualId));

            IndividualId = individualId;
            BuiltAtTick = builtAtTick;
            Items = ContractGuard.List(items, nameof(items));
            if (Items.Count > 100) throw new ArgumentOutOfRangeException(nameof(items));
            for (var index = 0; index < Items.Count; index++)
            {
                if (Items[index] is null)
                    throw new ArgumentException("Context packets cannot contain null items.", nameof(items));
            }
        }

        public IndividualId IndividualId { get; }
        public long BuiltAtTick { get; }
        public IReadOnlyList<CharacterContextItem> Items { get; }
    }

    public interface IReadOnlyCharacterContextProvider
    {
        CharacterContextPacket BuildContext(IndividualId individualId, long currentTick);
    }
}
