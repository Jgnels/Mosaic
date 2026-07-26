using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        ValidationRejected = 5
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
    /// Fake-provider-only composition root for the first grounded RimWorld relationship-dialogue path.
    /// It accepts plain captured data and has no access to Pawn, Map, Def, jobs,
    /// persistence paths, or canonical mutation.
    /// </summary>
    public sealed class OfflineRimWorldDialoguePipeline
    {
        public const long RequestLifetimeTicks = 600;
        private readonly object _gate = new object();
        private readonly HashSet<UtteranceId> _preparedUtterances = new HashSet<UtteranceId>();

        public async Task<OfflineRimWorldDialoguePreparationResult> PrepareAsync(
            RimWorldSocialDialogueTrigger trigger,
            long currentTick,
            CancellationToken cancellationToken)
        {
            if (trigger is null) throw new ArgumentNullException(nameof(trigger));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            var expiresAtTick = checked(trigger.ObservedAtTick + RequestLifetimeTicks);
            if (currentTick >= expiresAtTick)
                return Result(OfflineRimWorldDialoguePreparationStatus.Expired, "The social trigger expired before dispatch.");

            var conversationId = new ConversationId(DeriveGuid(trigger.SourceEventId.Value, "conversation"));
            var requestId = new DialogueRequestId(DeriveGuid(trigger.SourceEventId.Value, "request"));
            var utteranceId = new UtteranceId(DeriveGuid(trigger.SourceEventId.Value, "utterance"));
            lock (_gate)
            {
                if (_preparedUtterances.Contains(utteranceId))
                    return Result(OfflineRimWorldDialoguePreparationStatus.Duplicate, "The social trigger was already prepared.");
            }

            var currentEvidence = new GroundedRelationshipEvidence(
                trigger.SourceEventId,
                trigger.EventKind,
                trigger.ObservedAtTick,
                trigger.FactualPayload,
                PerceptionChannel.Experienced,
                PrivacyClassification.RelationshipSensitive,
                1.0);
            var grounded = new GroundedRelationshipDialogueComposer().Compose(
                trigger.Recipient.DisplayLabel,
                currentEvidence,
                trigger.PriorRelationshipEvidence);

            var scene = new DialogueSceneSnapshot(
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
            var request = new DialogueRequest(
                requestId,
                conversationId,
                DialogueTriggerKind.Social,
                DialoguePriority.Normal,
                trigger.Speaker.IndividualId,
                trigger.Speaker.IndividualId,
                trigger.Recipient.IndividualId,
                scene,
                grounded.EvidenceIds,
                trigger.ObservedAtTick,
                expiresAtTick,
                "rimworld-social:" + trigger.SourceEventId);
            var scheduler = new DialogueScheduler(1);
            var enqueue = scheduler.Enqueue(request, currentTick);
            if (enqueue.Disposition != DialogueEnqueueDisposition.Accepted)
                return Result(OfflineRimWorldDialoguePreparationStatus.Duplicate, "The request scheduler rejected the trigger.");
            request = scheduler.TryDequeue(currentTick)!;

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
                    Math.Min(0.95, 0.55 + (Math.Abs(prior.Valence) * prior.Confidence * 0.4)),
                    trigger.Speaker.IndividualId,
                    new[] { trigger.Recipient.IndividualId }));
            }

            var context = new DialogueContextAssembler().Build(
                trigger.Speaker.IndividualId,
                trigger.Recipient.IndividualId,
                currentTick,
                contextCandidates,
                new DialogueContextBudget(4, 4096));
            var prompt = new DialoguePromptPlanner().Build(request, context);
            var expected = new UtteranceProposal(
                utteranceId,
                request.Id,
                request.ConversationId,
                request.ExpectedSpeakerId,
                request.RecipientId,
                grounded.Text,
                request.SourceEventIds,
                currentTick);
            var modelRequest = new ModelRequest(
                new Dagmay.Core.Contracts.RequestId(request.Id.Value),
                trigger.Speaker.IndividualId,
                trigger.Speaker.LineageId,
                trigger.Speaker.StateVersion,
                ModelTaskKind.GenerateDialogueUtterance,
                "mosaic-rimworld-grounded-relationship-dialogue-v1",
                prompt.Segments[0].Content,
                prompt.Segments[1].Content,
                UtteranceProposalJson.ProviderCompatibleSchema,
                request.SourceEventIds,
                trigger.Speaker.Affect,
                trigger.ObservedAtUtc.AddSeconds(10),
                512);
            var provider = new DeterministicFakeProvider(
                FakeProviderBehavior.Success,
                UtteranceProposalJson.Serialize(expected));
            var modelResult = await provider.GenerateStructuredAsync(
                modelRequest,
                cancellationToken).ConfigureAwait(false);
            if (modelResult.Status != ModelResultStatus.Success)
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.ProviderFailure,
                    "The deterministic fake provider did not return a successful payload.");

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
                return Result(OfflineRimWorldDialoguePreparationStatus.InvalidPayload, exception.Message);
            }

            var validation = new UtteranceValidator().Validate(
                request,
                proposal,
                currentTick,
                new HashSet<UtteranceId>(),
                new UtteranceValidationPolicy(true, 600));
            if (!validation.IsAccepted || validation.Utterance is null)
                return Result(
                    OfflineRimWorldDialoguePreparationStatus.ValidationRejected,
                    "The strict utterance validator rejected the fake-provider payload.");

            lock (_gate)
            {
                if (!_preparedUtterances.Add(utteranceId))
                    return Result(OfflineRimWorldDialoguePreparationStatus.Duplicate, "The utterance was prepared concurrently.");
            }

            var row = new DialoguePresentationRow(
                new Dagmay.Core.Contracts.EventId(utteranceId.Value),
                utteranceId,
                conversationId,
                trigger.Speaker.IndividualId,
                trigger.Recipient.IndividualId,
                trigger.Speaker.DisplayLabel,
                proposal.Text,
                DialoguePriority.Normal);
            return new OfflineRimWorldDialoguePreparationResult(
                OfflineRimWorldDialoguePreparationStatus.Prepared,
                new OfflineRimWorldPreparedDialogue(
                    request,
                    validation.Utterance,
                    row,
                    new[] { trigger.Speaker.IndividualId, trigger.Recipient.IndividualId }),
                "The deterministic grounded relationship utterance passed strict offline preparation with "
                + request.SourceEventIds.Count
                + " exact evidence ID(s).");
        }

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
