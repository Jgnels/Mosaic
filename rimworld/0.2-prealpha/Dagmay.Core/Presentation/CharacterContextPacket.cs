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

            if (EvidenceIds.Count == 0)
                throw new ArgumentException("Grounded context items require at least one supporting EvidenceId.", nameof(evidenceIds));
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

            IndividualId = individualId;
            BuiltAtTick = builtAtTick;
            Items = ContractGuard.List(items, nameof(items));
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
