using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.RimWorld.Dialogue
{
    public sealed class RimWorldDialogueIdentitySnapshot
    {
        public RimWorldDialogueIdentitySnapshot(
            string externalId,
            IndividualId individualId,
            LineageId lineageId,
            long stateVersion,
            string displayLabel,
            AffectVector affect)
        {
            if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > 256)
                throw new ArgumentException("A stable external ID is required.", nameof(externalId));
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("IndividualId cannot be empty.", nameof(individualId));
            if (lineageId.Value == Guid.Empty)
                throw new ArgumentException("LineageId cannot be empty.", nameof(lineageId));
            if (stateVersion < 0) throw new ArgumentOutOfRangeException(nameof(stateVersion));
            if (displayLabel is null || displayLabel.Length > 256)
                throw new ArgumentException("Display label cannot exceed 256 characters.", nameof(displayLabel));

            ExternalId = externalId;
            IndividualId = individualId;
            LineageId = lineageId;
            StateVersion = stateVersion;
            DisplayLabel = displayLabel;
            Affect = affect ?? throw new ArgumentNullException(nameof(affect));
        }

        public string ExternalId { get; }
        public IndividualId IndividualId { get; }
        public LineageId LineageId { get; }
        public long StateVersion { get; }
        public string DisplayLabel { get; }
        public AffectVector Affect { get; }
    }

    public sealed class RimWorldSocialDialogueTrigger
    {
        public RimWorldSocialDialogueTrigger(
            EventId sourceEventId,
            long observedAtTick,
            DateTimeOffset observedAtUtc,
            string eventKind,
            string factualSummary,
            RimWorldDialogueIdentitySnapshot speaker,
            RimWorldDialogueIdentitySnapshot recipient,
            IDictionary<string, string> factualPayload)
        {
            if (sourceEventId.Value == Guid.Empty)
                throw new ArgumentException("Source EventId cannot be empty.", nameof(sourceEventId));
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));
            if (string.IsNullOrWhiteSpace(eventKind) || eventKind.Length > 128)
                throw new ArgumentException("Event kind is required.", nameof(eventKind));
            if (string.IsNullOrWhiteSpace(factualSummary) || factualSummary.Length > 1024)
                throw new ArgumentException("Factual summary is required.", nameof(factualSummary));
            Speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
            Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
            if (Speaker.IndividualId == Recipient.IndividualId)
                throw new ArgumentException("A social trigger requires two distinct individuals.");
            if (factualPayload is null) throw new ArgumentNullException(nameof(factualPayload));
            if (factualPayload.Count == 0 || factualPayload.Count > 16)
                throw new ArgumentOutOfRangeException(nameof(factualPayload));
            var payload = factualPayload
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => ValidateText(pair.Key, nameof(factualPayload), 128),
                    pair => ValidateText(pair.Value, nameof(factualPayload), 512),
                    StringComparer.Ordinal);

            SourceEventId = sourceEventId;
            ObservedAtTick = observedAtTick;
            ObservedAtUtc = observedAtUtc;
            EventKind = eventKind;
            FactualSummary = factualSummary;
            FactualPayload = new ReadOnlyDictionary<string, string>(payload);
        }

        public EventId SourceEventId { get; }
        public long ObservedAtTick { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string EventKind { get; }
        public string FactualSummary { get; }
        public RimWorldDialogueIdentitySnapshot Speaker { get; }
        public RimWorldDialogueIdentitySnapshot Recipient { get; }
        public IReadOnlyDictionary<string, string> FactualPayload { get; }

        private static string ValidateText(string value, string parameter, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("Factual payload keys and values must be bounded.", parameter);
            return value;
        }
    }

    public static class RimWorldSocialDialogueCapture
    {
        public const string OpinionChanged = "rimworld.social.opinion_changed";
        public const string DirectRelationshipChanged = "rimworld.relationship.direct_changed";

        public static RimWorldSocialDialogueTrigger? TryCreate(
            EventId sourceEventId,
            long observedAtTick,
            DateTimeOffset observedAtUtc,
            string eventKind,
            IDictionary<string, string> factualPayload,
            RimWorldDialogueIdentitySnapshot? speaker,
            RimWorldDialogueIdentitySnapshot? recipient)
        {
            if (speaker is null || recipient is null) return null;
            if (!string.Equals(eventKind, OpinionChanged, StringComparison.Ordinal) &&
                !string.Equals(eventKind, DirectRelationshipChanged, StringComparison.Ordinal))
                return null;
            if (factualPayload is null || factualPayload.Count == 0) return null;

            var summary = string.Equals(eventKind, OpinionChanged, StringComparison.Ordinal)
                ? "The speaker's observed opinion of the recipient materially changed."
                : "The speaker's observed direct relationship with the recipient changed.";
            return new RimWorldSocialDialogueTrigger(
                sourceEventId,
                observedAtTick,
                observedAtUtc,
                eventKind,
                summary,
                speaker,
                recipient,
                factualPayload);
        }
    }
}
