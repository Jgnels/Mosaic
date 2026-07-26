using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Dialogue
{
    public enum DialogueAdmissionDisposition
    {
        Prepared = 0,
        RequestMismatch = 1,
        ConversationMismatch = 2,
        ForeignSpeaker = 3,
        RecipientMismatch = 4,
        AudienceOutsideScene = 5,
        EvidenceMismatch = 6,
        InvalidGenerationWindow = 7,
        DisplayedAfterExpiry = 8
    }

    public sealed class DialogueEventAdmissionPlan
    {
        internal DialogueEventAdmissionPlan(
            EnvironmentEvent factualEvent,
            ExperienceJournalRecord journalRecord)
        {
            FactualEvent = factualEvent ?? throw new ArgumentNullException(nameof(factualEvent));
            JournalRecord = journalRecord ?? throw new ArgumentNullException(nameof(journalRecord));
        }

        public EnvironmentEvent FactualEvent { get; }
        public ExperienceJournalRecord JournalRecord { get; }
    }

    public sealed class DialogueEventAdmissionResult
    {
        private DialogueEventAdmissionResult(
            DialogueAdmissionDisposition disposition,
            DialogueEventAdmissionPlan? plan,
            string diagnostic)
        {
            Disposition = disposition;
            Plan = plan;
            Diagnostic = ContractGuard.Text(diagnostic, nameof(diagnostic), 1024);
        }

        public DialogueAdmissionDisposition Disposition { get; }
        public DialogueEventAdmissionPlan? Plan { get; }
        public string Diagnostic { get; }
        public bool IsPrepared => Disposition == DialogueAdmissionDisposition.Prepared;

        internal static DialogueEventAdmissionResult Prepared(DialogueEventAdmissionPlan plan)
        {
            return new DialogueEventAdmissionResult(
                DialogueAdmissionDisposition.Prepared,
                plan,
                "Displayed utterance passed deterministic dialogue-event admission.");
        }

        internal static DialogueEventAdmissionResult Rejected(
            DialogueAdmissionDisposition disposition,
            string diagnostic)
        {
            if (disposition == DialogueAdmissionDisposition.Prepared)
                throw new ArgumentException("Prepared admission requires a plan.", nameof(disposition));
            return new DialogueEventAdmissionResult(disposition, null, diagnostic);
        }
    }

    /// <summary>
    /// Converts an actually displayed, already validated utterance into a factual
    /// EnvironmentEvent plus a factual-only experience journal record. It does not
    /// create a perception, memory, relationship delta, mood effect, goal, belief,
    /// action, or any other canonical interpretation.
    /// </summary>
    public sealed class DialogueEventAdmissionService
    {
        public const string EventKind = "mosaic.dialogue.utterance.observed";
        public const string DefaultSource = "mosaic.dialogue.presentation";
        public const string ConversationIdKey = "conversation_id";
        public const string RequestIdKey = "request_id";
        public const string UtteranceIdKey = "utterance_id";
        public const string SpeakerIdKey = "speaker_id";
        public const string RecipientIdKey = "recipient_id";
        public const string TextKey = "text";
        public const string DisclosureKey = "disclosure";
        public const string PresentationChannelKey = "presentation_channel";
        public const string SupportingEventIdsKey = "supporting_event_ids";

        private readonly string _environment;
        private readonly string _source;

        public DialogueEventAdmissionService(string environment, string source = DefaultSource)
        {
            _environment = ContractGuard.Text(environment, nameof(environment), 128);
            _source = ContractGuard.Text(source, nameof(source), 512);
        }

        public DialogueEventAdmissionResult Prepare(
            DialogueRequest request,
            DisplayedUtteranceReceipt receipt)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (receipt is null) throw new ArgumentNullException(nameof(receipt));

            var proposal = receipt.Utterance.Proposal;
            if (proposal.RequestId != request.Id)
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.RequestMismatch,
                    "Displayed utterance request ID does not match the admission request.");
            }

            if (proposal.ConversationId != request.ConversationId)
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.ConversationMismatch,
                    "Displayed utterance conversation ID does not match the admission request.");
            }

            if (proposal.SpeakerId != request.ExpectedSpeakerId)
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.ForeignSpeaker,
                    "Displayed utterance speaker does not match the expected speaker.");
            }

            if (proposal.RecipientId != request.RecipientId)
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.RecipientMismatch,
                    "Displayed utterance recipient does not match the request.");
            }

            if (proposal.GeneratedAtTick < request.CreatedAtTick ||
                proposal.GeneratedAtTick >= request.ExpiresAtTick)
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.InvalidGenerationWindow,
                    "Utterance generation tick falls outside the admitted request window.");
            }

            if (request.IsExpired(receipt.DisplayedAtTick))
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.DisplayedAfterExpiry,
                    "Utterance was displayed after the request expired.");
            }

            if (receipt.AudienceIds.Any(id => !request.Scene.Contains(id)))
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.AudienceOutsideScene,
                    "Displayed utterance audience includes an individual absent from the captured scene.");
            }

            var requestEvidence = new HashSet<EventId>(request.SourceEventIds);
            if (proposal.EvidenceIds.Any(id => !requestEvidence.Contains(id)))
            {
                return DialogueEventAdmissionResult.Rejected(
                    DialogueAdmissionDisposition.EvidenceMismatch,
                    "Displayed utterance cites evidence outside the admitted dialogue request.");
            }

            var eventId = new EventId(proposal.Id.Value);
            var factualPayload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ConversationIdKey] = proposal.ConversationId.ToString(),
                [RequestIdKey] = proposal.RequestId.ToString(),
                [UtteranceIdKey] = proposal.Id.ToString(),
                [SpeakerIdKey] = proposal.SpeakerId.ToString(),
                [RecipientIdKey] = proposal.RecipientId?.ToString() ?? string.Empty,
                [TextKey] = proposal.Text,
                [DisclosureKey] = receipt.Disclosure.ToString(),
                [PresentationChannelKey] = receipt.Channel.ToString(),
                [SupportingEventIdsKey] = string.Join(
                    ",",
                    proposal.EvidenceIds
                        .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                        .Select(id => id.ToString()))
            };

            var audience = receipt.AudienceIds
                .OrderBy(id => id.ToString(), StringComparer.Ordinal)
                .ToArray();
            var factualEvent = new EnvironmentEvent(
                eventId,
                DeduplicationKey(proposal.Id),
                EventKind,
                _environment,
                receipt.DisplayedAtUtc,
                receipt.DisplayedAtUtc,
                receipt.DisplayedAtTick,
                _source,
                factualPayload,
                audience);
            var journalRecord = new ExperienceJournalRecord(factualEvent, null, null);

            return DialogueEventAdmissionResult.Prepared(
                new DialogueEventAdmissionPlan(factualEvent, journalRecord));
        }

        public static string DeduplicationKey(UtteranceId utteranceId)
        {
            if (utteranceId.Value == Guid.Empty)
                throw new ArgumentException("Utterance ID cannot be empty.", nameof(utteranceId));
            return "mosaic-dialogue-utterance:" + utteranceId.ToString();
        }
    }
}
