using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public enum DialogueDisclosure
    {
        WitnessesOnly = 0,
        Shareable = 1
    }

    public enum DialoguePresentationChannel
    {
        Overlay = 0,
        Bubble = 1,
        PlayLog = 2,
        PlayerConversation = 3
    }

    /// <summary>
    /// Plain-data evidence that a previously validated utterance was actually displayed.
    /// Creating a proposal or receiving a provider response is not enough to create a
    /// canonical dialogue event.
    /// </summary>
    public sealed class DisplayedUtteranceReceipt
    {
        public DisplayedUtteranceReceipt(
            ValidatedUtterance utterance,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            DialogueDisclosure disclosure,
            DialoguePresentationChannel channel,
            IEnumerable<IndividualId> audienceIds)
        {
            Utterance = utterance ?? throw new ArgumentNullException(nameof(utterance));
            if (displayedAtTick < utterance.Proposal.GeneratedAtTick)
                throw new ArgumentOutOfRangeException(
                    nameof(displayedAtTick),
                    "An utterance cannot be displayed before it was generated.");
            if (!Enum.IsDefined(typeof(DialogueDisclosure), disclosure))
                throw new ArgumentOutOfRangeException(nameof(disclosure));
            if (!Enum.IsDefined(typeof(DialoguePresentationChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));

            DisplayedAtTick = displayedAtTick;
            DisplayedAtUtc = displayedAtUtc;
            Disclosure = disclosure;
            Channel = channel;
            AudienceIds = ContractGuard.List(audienceIds, nameof(audienceIds));

            if (AudienceIds.Count == 0 || AudienceIds.Count > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(audienceIds),
                    "A displayed utterance requires between 1 and 32 audience members.");

            if (AudienceIds.Any(id => id.Value == Guid.Empty))
                throw new ArgumentException("Audience IDs cannot contain an empty ID.", nameof(audienceIds));

            if (AudienceIds.Distinct().Count() != AudienceIds.Count)
                throw new ArgumentException("Audience IDs must be unique.", nameof(audienceIds));

            if (!AudienceIds.Contains(Utterance.Proposal.SpeakerId))
                throw new ArgumentException("The speaker must be included in the observed audience.", nameof(audienceIds));

            if (Utterance.Proposal.RecipientId.HasValue &&
                !AudienceIds.Contains(Utterance.Proposal.RecipientId.Value))
            {
                throw new ArgumentException("The recipient must be included in the observed audience.", nameof(audienceIds));
            }
        }

        public ValidatedUtterance Utterance { get; }
        public long DisplayedAtTick { get; }
        public DateTimeOffset DisplayedAtUtc { get; }
        public DialogueDisclosure Disclosure { get; }
        public DialoguePresentationChannel Channel { get; }
        public IReadOnlyList<IndividualId> AudienceIds { get; }
    }
}
