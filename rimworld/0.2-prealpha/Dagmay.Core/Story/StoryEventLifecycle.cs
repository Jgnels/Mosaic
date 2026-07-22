using System;
using System.Collections.Generic;
using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Story
{
    public enum StoryEventPhase { Announced=0, Started=1, Progressed=2, Completed=3, Failed=4, Cancelled=5, Expired=6 }
    public enum StorySignificance { Background=0, Notable=1, Major=2, Defining=3 }

    public sealed class StoryEventDescriptor
    {
        public StoryEventDescriptor(string storyEventId, string eventKind, EvidenceDomain domain,
            StoryEventPhase phase, StorySignificance significance, long observedAtTick,
            IEnumerable<IndividualId> participantIds, string? sourceModId=null,
            string? factionKey=null, string? locationKey=null)
        {
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));
            StoryEventId = ContractGuard.Text(storyEventId, nameof(storyEventId), 256);
            EventKind = ContractGuard.Text(eventKind, nameof(eventKind), 256);
            Domain = domain;
            Phase = phase;
            Significance = significance;
            ObservedAtTick = observedAtTick;
            ParticipantIds = ContractGuard.List(participantIds, nameof(participantIds));
            SourceModId = Optional(sourceModId, nameof(sourceModId), 256);
            FactionKey = Optional(factionKey, nameof(factionKey), 256);
            LocationKey = Optional(locationKey, nameof(locationKey), 256);
        }
        public string StoryEventId { get; }
        public string EventKind { get; }
        public EvidenceDomain Domain { get; }
        public StoryEventPhase Phase { get; }
        public StorySignificance Significance { get; }
        public long ObservedAtTick { get; }
        public IReadOnlyList<IndividualId> ParticipantIds { get; }
        public string? SourceModId { get; }
        public string? FactionKey { get; }
        public string? LocationKey { get; }
        public bool IsTerminal => Phase==StoryEventPhase.Completed || Phase==StoryEventPhase.Failed ||
            Phase==StoryEventPhase.Cancelled || Phase==StoryEventPhase.Expired;
        private static string? Optional(string? value, string name, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : ContractGuard.Text(value, name, max);
    }
}
