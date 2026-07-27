using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Reflection;
using Dagmay.Providers.Fake;

namespace Dagmay.RimWorld.Dialogue
{
    public enum OfflineRimWorldDialoguePreparationStatus
    {
        Prepared = 0,
        Expired = 1,
        Duplicate = 2,
        ProviderFailure = 3,
        InvalidPayload = 4,
        ValidationRejected = 5,
        OpeningMismatch = 6
    }

    public sealed class OfflineRimWorldPreparedDialogue
    {
        public OfflineRimWorldPreparedDialogue(
            DialogueRequest request,
            ValidatedUtterance utterance,
            DialoguePresentationRow presentationRow,
            IReadOnlyList<Dagmay.Core.Contracts.IndividualId> audienceIds)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Utterance = utterance ?? throw new ArgumentNullException(nameof(utterance));
            PresentationRow = presentationRow ?? throw new ArgumentNullException(nameof(presentationRow));
            AudienceIds = audienceIds ?? throw new ArgumentNullException(nameof(audienceIds));
        }

        public DialogueRequest Request { get; }
        public ValidatedUtterance Utterance { get; }
        public DialoguePresentationRow PresentationRow { get; }
        public IReadOnlyList<Dagmay.Core.Contracts.IndividualId> AudienceIds { get; }
    }

    public sealed class OfflineRimWorldDialoguePreparationResult
    {
        public OfflineRimWorldDialoguePreparationResult(
            OfflineRimWorldDialoguePreparationStatus status,
            OfflineRimWorldPreparedDialogue? prepared,
            string diagnostic)
        {
            Status = status;
            Prepared = prepared;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public OfflineRimWorldDialoguePreparationStatus Status { get; }
        public OfflineRimWorldPreparedDialogue? Prepared { get; }
        public string Diagnostic { get; }
        public bool IsPrepared => Status == OfflineRimWorldDialoguePreparationStatus.Prepared;
    }

    /// <summary>
    /// Fake-provider-only composition root for grounded opening lines and one
    /// receipt-gated deterministic reply. It accepts plain captured data and
    /// has no access to Pawn, Map, Def, jobs, persistence paths, or canonical
    /// mutation.
    /// </summary>
    public sealed class OfflineRimWorldDialoguePipeline
    {
        public const long RequestLifetimeTicks = 600;
        public const long ReplyRequestLifetimeTicks = 1800;

        private readonly object _gate = new object();
        private readonly HashSet<UtteranceId> _preparedUtterances =
            new HashSet<UtteranceId>();

        public async Task<OfflineRimWorldDialoguePreparationResult> PrepareAsync(
            RimWorldSocialDialogueTrigger trigger,
            long currentTick,
            CancellationToken cancellationToken)
        {
            if (trigger is null) throw new ArgumentNullException(nameof(trigger));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            var expiresAtTick = checked(trigger.ObservedAtTick + RequestLifetimeTicks);
            if (currentTick >= expiresAtTick)
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Expired,
                    "The social trigger expired before dispatch.");
            }

            var conversationId = ConversationIdFor(trigger.SourceEventId);
            var requestId = OpeningRequestIdFor(trigger.SourceEventId);
            var utteranceId = OpeningUtteranceIdFor(trigger.SourceEventId);
            if (AlreadyPrepared(utteranceId))
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Duplicate,
                    "The social trigger was already prepared.");
            }

            var currentEvidence = CurrentEvidence(trigger);
            var grounded = new GroundedRelationshipDialogueComposer().Compose(
                trigger.Recipient.DisplayLabel,
                currentEvidence,
                trigger.PriorRelationshipEvidence);
            var continuity = new GroundedCrossEncounterDialogueComposer().Compose(
                trigger.Speaker.IndividualId,
                trigger.Recipient.IndividualId,
                grounded,
                trigger.PriorConversationContext);
            var scene = Scene(trigger);
            var request = new DialogueRequest(
                requestId,
                conversationId,
                DialogueTriggerKind.Social,
                DialoguePriority.Normal,
                trigger.Speaker.IndividualId,
                trigger.Speaker.IndividualId,
                trigger.Recipient.IndividualId,
                scene,
                continuity.EvidenceIds,
                trigger.ObservedAtTick,
                expiresAtTick,
                "rimworld-social:" + trigger.SourceEventId);

            var contextCandidates = new List<DialogueContextItem>
            {
                new DialogueContextItem(
                    "observed-social-fact",
                    GroundedRelationshipDialogueComposer.DescribeEvidence(currentEvidence),
                    new[] { trigger.SourceEventId },
                    DialogueContextAudience.RelationshipSensitive,
                    1.0,
                    trigger.Speaker.IndividualId,
                    new[] { trigger.Recipient.IndividualId })
            };
            foreach (var prior in grounded.SelectedPriorEvidence)
            {
                contextCandidates.Add(new DialogueContextItem(
                    "same-counterpart-relationship-history",
                    GroundedRelationshipDialogueComposer.DescribeEvidence(prior),
                    new[] { prior.EventId },
                    DialogueContextAudience.RelationshipSensitive,
                    Math.Min(
                        0.95,
                        0.55 + (Math.Abs(prior.Valence) * prior.Confidence * 0.4)),
                    trigger.Speaker.IndividualId,
                    new[] { trigger.Recipient.IndividualId }));
            }
            if (continuity.PriorConversation is not null)
            {
                foreach (var turn in new[]
                {
                    continuity.PriorConversation.FirstTurn,
                    continuity.PriorConversation.SecondTurn
                })
                {
                    var role = turn.SpeakerId == trigger.Speaker.IndividualId
                        ? "The current speaker previously displayed: "
                        : "The current recipient previously displayed: ";
                    contextCandidates.Add(new DialogueContextItem(
                        "actually-displayed-prior-conversation",
                        role + turn.Text,
                        new[] { turn.EventId },
                        DialogueContextAudience.RelationshipSensitive,
                        0.95,
                        trigger.Speaker.IndividualId,
                        new[] { trigger.Recipient.IndividualId }));
                }
            }

            var prepared = await PrepareTurnAsync(
                    request,
                    utteranceId,
                    continuity.Text,
                    trigger.Speaker,
                    trigger.Recipient,
                    contextCandidates,
                    currentTick,
                    "mosaic-rimworld-cross-encounter-continuity-v1",
                    trigger.ObservedAtUtc.AddSeconds(10),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!prepared.IsPrepared) return prepared;
            if (!Reserve(utteranceId))
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Duplicate,
                    "The opening utterance was prepared concurrently.");
            }

            return prepared;
        }

        public async Task<OfflineRimWorldDialoguePreparationResult> PrepareReplyAsync(
            RimWorldSocialDialogueTrigger trigger,
            DisplayedUtteranceReceipt openingReceipt,
            long currentTick,
            CancellationToken cancellationToken)
        {
            if (trigger is null) throw new ArgumentNullException(nameof(trigger));
            if (openingReceipt is null) throw new ArgumentNullException(nameof(openingReceipt));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (!OpeningMatches(trigger, openingReceipt, currentTick))
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.OpeningMismatch,
                    "The reply was rejected because the displayed opening receipt did not match the captured social trigger.");
            }

            var utteranceId = ReplyUtteranceIdFor(trigger.SourceEventId);
            if (AlreadyPrepared(utteranceId))
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Duplicate,
                    "The bounded reply was already prepared.");
            }

            var currentEvidence = CurrentEvidence(trigger);
            var replyPlan = new GroundedConversationReplyComposer().Compose(
                trigger.Speaker.DisplayLabel,
                currentEvidence,
                trigger.RecipientPriorRelationshipEvidence);
            var createdAtTick = currentTick;
            var expiresAtTick = checked(currentTick + ReplyRequestLifetimeTicks);
            var request = new DialogueRequest(
                ReplyRequestIdFor(trigger.SourceEventId),
                ConversationIdFor(trigger.SourceEventId),
                DialogueTriggerKind.Reply,
                DialoguePriority.Normal,
                trigger.Recipient.IndividualId,
                trigger.Recipient.IndividualId,
                trigger.Speaker.IndividualId,
                Scene(trigger),
                replyPlan.EvidenceIds,
                createdAtTick,
                expiresAtTick,
                "rimworld-social-reply:" + trigger.SourceEventId);

            var heardOpening = openingReceipt.Utterance.Proposal.Text.Length <= 1500
                ? openingReceipt.Utterance.Proposal.Text
                : openingReceipt.Utterance.Proposal.Text.Substring(0, 1500) + " [truncated]";
            var contextCandidates = new List<DialogueContextItem>
            {
                new DialogueContextItem(
                    "actually-displayed-opening",
                    "The other participant actually displayed this grounded line to the reply speaker: "
                    + heardOpening,
                    new[] { trigger.SourceEventId },
                    DialogueContextAudience.RelationshipSensitive,
                    1.0,
                    trigger.Recipient.IndividualId,
                    new[] { trigger.Speaker.IndividualId })
            };
            foreach (var prior in replyPlan.SelectedRecipientEvidence)
            {
                contextCandidates.Add(new DialogueContextItem(
                    "reply-speaker-relationship-history",
                    GroundedRelationshipDialogueComposer.DescribeEvidence(prior),
                    new[] { prior.EventId },
                    DialogueContextAudience.RelationshipSensitive,
                    Math.Min(
                        0.95,
                        0.55 + (Math.Abs(prior.Valence) * prior.Confidence * 0.4)),
                    trigger.Recipient.IndividualId,
                    new[] { trigger.Speaker.IndividualId }));
            }

            var prepared = await PrepareTurnAsync(
                    request,
                    utteranceId,
                    replyPlan.Text,
                    trigger.Recipient,
                    trigger.Speaker,
                    contextCandidates,
                    currentTick,
                    "mosaic-rimworld-bounded-reply-v1",
                    openingReceipt.DisplayedAtUtc.AddSeconds(10),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!prepared.IsPrepared) return prepared;
            if (!Reserve(utteranceId))
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Duplicate,
                    "The bounded reply was prepared concurrently.");
            }

            return new OfflineRimWorldDialoguePreparationResult(
                OfflineRimWorldDialoguePreparationStatus.Prepared,
                prepared.Prepared,
                "The receipt-gated deterministic reply passed strict offline preparation with "
                + request.SourceEventIds.Count
                + " exact evidence ID(s).");
        }

        private async Task<OfflineRimWorldDialoguePreparationResult> PrepareTurnAsync(
            DialogueRequest request,
            UtteranceId utteranceId,
            string text,
            RimWorldDialogueIdentitySnapshot speaker,
            RimWorldDialogueIdentitySnapshot recipient,
            IEnumerable<DialogueContextItem> contextCandidates,
            long currentTick,
            string modelName,
            DateTimeOffset deadlineUtc,
            CancellationToken cancellationToken)
        {
            var scheduler = new DialogueScheduler(1);
            var enqueue = scheduler.Enqueue(request, currentTick);
            if (enqueue.Disposition != DialogueEnqueueDisposition.Accepted)
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.Duplicate,
                    "The request scheduler rejected the bounded turn.");
            }

            request = scheduler.TryDequeue(currentTick)!;
            var context = new DialogueContextAssembler().Build(
                speaker.IndividualId,
                recipient.IndividualId,
                currentTick,
                contextCandidates,
                new DialogueContextBudget(6, 6144));
            var prompt = new DialoguePromptPlanner().Build(request, context);
            var expected = new UtteranceProposal(
                utteranceId,
                request.Id,
                request.ConversationId,
                request.ExpectedSpeakerId,
                request.RecipientId,
                text,
                request.SourceEventIds,
                currentTick);
            var modelRequest = new ModelRequest(
                new Dagmay.Core.Contracts.RequestId(request.Id.Value),
                speaker.IndividualId,
                speaker.LineageId,
                speaker.StateVersion,
                ModelTaskKind.GenerateDialogueUtterance,
                modelName,
                prompt.Segments[0].Content,
                prompt.Segments[1].Content,
                UtteranceProposalJson.ProviderCompatibleSchema,
                request.SourceEventIds,
                speaker.Affect,
                deadlineUtc,
                512);
            var provider = new DeterministicFakeProvider(
                FakeProviderBehavior.Success,
                UtteranceProposalJson.Serialize(expected));
            var modelResult = await provider.GenerateStructuredAsync(
                    modelRequest,
                    cancellationToken)
                .ConfigureAwait(false);
            if (modelResult.Status != ModelResultStatus.Success)
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.ProviderFailure,
                    "The deterministic fake provider did not return a successful payload.");
            }

            UtteranceProposal proposal;
            try
            {
                proposal = UtteranceProposalJson.Parse(
                    modelResult.StructuredPayload,
                    request,
                    utteranceId,
                    currentTick);
            }
            catch (InvalidDataException exception)
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.InvalidPayload,
                    exception.Message);
            }

            var validation = new UtteranceValidator().Validate(
                request,
                proposal,
                currentTick,
                new HashSet<UtteranceId>(),
                new UtteranceValidationPolicy(true, 600));
            if (!validation.IsAccepted || validation.Utterance is null)
            {
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.ValidationRejected,
                    "The strict utterance validator rejected the fake-provider payload.");
            }

            var row = new DialoguePresentationRow(
                new Dagmay.Core.Contracts.EventId(utteranceId.Value),
                utteranceId,
                request.ConversationId,
                speaker.IndividualId,
                recipient.IndividualId,
                speaker.DisplayLabel,
                proposal.Text,
                DialoguePriority.Normal);
            return new OfflineRimWorldDialoguePreparationResult(
                OfflineRimWorldDialoguePreparationStatus.Prepared,
                new OfflineRimWorldPreparedDialogue(
                    request,
                    validation.Utterance,
                    row,
                    new[] { speaker.IndividualId, recipient.IndividualId }),
                "The deterministic bounded turn passed strict offline preparation.");
        }

        private static bool OpeningMatches(
            RimWorldSocialDialogueTrigger trigger,
            DisplayedUtteranceReceipt receipt,
            long currentTick)
        {
            var proposal = receipt.Utterance.Proposal;
            return currentTick >= receipt.DisplayedAtTick
                && proposal.Id == OpeningUtteranceIdFor(trigger.SourceEventId)
                && proposal.RequestId == OpeningRequestIdFor(trigger.SourceEventId)
                && proposal.ConversationId == ConversationIdFor(trigger.SourceEventId)
                && proposal.SpeakerId == trigger.Speaker.IndividualId
                && proposal.RecipientId == trigger.Recipient.IndividualId
                && proposal.EvidenceIds.Contains(trigger.SourceEventId)
                && receipt.AudienceIds.Contains(trigger.Speaker.IndividualId)
                && receipt.AudienceIds.Contains(trigger.Recipient.IndividualId);
        }

        private bool AlreadyPrepared(UtteranceId utteranceId)
        {
            lock (_gate) return _preparedUtterances.Contains(utteranceId);
        }

        private bool Reserve(UtteranceId utteranceId)
        {
            lock (_gate) return _preparedUtterances.Add(utteranceId);
        }

        private static GroundedRelationshipEvidence CurrentEvidence(
            RimWorldSocialDialogueTrigger trigger) =>
            new GroundedRelationshipEvidence(
                trigger.SourceEventId,
                trigger.EventKind,
                trigger.ObservedAtTick,
                trigger.FactualPayload,
                PerceptionChannel.Experienced,
                PrivacyClassification.RelationshipSensitive,
                1.0);

        private static DialogueSceneSnapshot Scene(
            RimWorldSocialDialogueTrigger trigger) =>
            new DialogueSceneSnapshot(
                trigger.SourceEventId,
                trigger.ObservedAtTick,
                trigger.FactualSummary,
                new[]
                {
                    new DialogueParticipantSnapshot(
                        trigger.Speaker.IndividualId,
                        trigger.Speaker.DisplayLabel,
                        "colonist",
                        true,
                        true),
                    new DialogueParticipantSnapshot(
                        trigger.Recipient.IndividualId,
                        trigger.Recipient.DisplayLabel,
                        "colonist",
                        true,
                        true)
                });

        private static ConversationId ConversationIdFor(EventId sourceEventId) =>
            new ConversationId(DeriveGuid(sourceEventId.Value, "conversation"));

        private static DialogueRequestId OpeningRequestIdFor(EventId sourceEventId) =>
            new DialogueRequestId(DeriveGuid(sourceEventId.Value, "request"));

        private static UtteranceId OpeningUtteranceIdFor(EventId sourceEventId) =>
            new UtteranceId(DeriveGuid(sourceEventId.Value, "utterance"));

        private static DialogueRequestId ReplyRequestIdFor(EventId sourceEventId) =>
            new DialogueRequestId(DeriveGuid(sourceEventId.Value, "reply-request"));

        private static UtteranceId ReplyUtteranceIdFor(EventId sourceEventId) =>
            new UtteranceId(DeriveGuid(sourceEventId.Value, "reply-utterance"));

        private static OfflineRimWorldDialoguePreparationResult Result(
            OfflineRimWorldDialoguePreparationStatus status,
            string diagnostic) =>
            new OfflineRimWorldDialoguePreparationResult(status, null, diagnostic);

        private static Guid DeriveGuid(Guid source, string discriminator)
        {
            using (var algorithm = SHA256.Create())
            {
                var sourceBytes = source.ToByteArray();
                var labelBytes = Encoding.UTF8.GetBytes(discriminator);
                var input = new byte[sourceBytes.Length + labelBytes.Length];
                Buffer.BlockCopy(sourceBytes, 0, input, 0, sourceBytes.Length);
                Buffer.BlockCopy(labelBytes, 0, input, sourceBytes.Length, labelBytes.Length);
                var hash = algorithm.ComputeHash(input);
                var value = new byte[16];
                Buffer.BlockCopy(hash, 0, value, 0, value.Length);
                return new Guid(value);
            }
        }
    }
}
