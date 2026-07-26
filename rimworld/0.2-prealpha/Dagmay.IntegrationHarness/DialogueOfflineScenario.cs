using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Providers.Fake;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static async Task<ScenarioExecution> DialogueOfflineEndToEndAsync()
        {
            var assertions = new HarnessAssert();
            var first = await ExecuteDialogueOnceAsync(assertions).ConfigureAwait(false);
            var second = await ExecuteDialogueOnceAsync(assertions).ConfigureAwait(false);

            assertions.Equal(first.Fingerprint, second.Fingerprint,
                "The same offline dialogue input produces an identical factual-event fingerprint.");
            assertions.Equal(1, first.LedgerCount,
                "Exactly one admitted factual dialogue event is present after replay.");
            assertions.Equal(0, first.WitnessMemoryCount,
                "Displayed speech does not automatically create subjective memory.");
            assertions.Equal(0, first.UnwitnessedHistoryCount,
                "An individual who did not witness the line receives no participant history.");

            return new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["admittedEvents"] = first.LedgerCount,
                    ["subjectiveMemories"] = first.WitnessMemoryCount,
                    ["unwitnessedHistoryEntries"] = first.UnwitnessedHistoryCount
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["eventFingerprint"] = first.Fingerprint,
                    ["provider"] = "dagmay.fake"
                });
        }

        private static async Task<DialogueRunResult> ExecuteDialogueOnceAsync(HarnessAssert assertions)
        {
            var speaker = IndividualId.Parse("10000000000000000000000000000001");
            var recipient = IndividualId.Parse("10000000000000000000000000000002");
            var witness = IndividualId.Parse("10000000000000000000000000000003");
            var outsider = IndividualId.Parse("10000000000000000000000000000004");
            var conversationId = ConversationId.Parse("20000000000000000000000000000001");
            var requestId = DialogueRequestId.Parse("30000000000000000000000000000001");
            var utteranceId = UtteranceId.Parse("40000000000000000000000000000001");
            var sourceEventId = EventId.Parse("50000000000000000000000000000001");
            var occurredAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

            var scene = new DialogueSceneSnapshot(
                sourceEventId,
                10,
                "A witness hears the speaker address the recipient.",
                new[]
                {
                    new DialogueParticipantSnapshot(speaker, "Speaker", "colonist", true, true),
                    new DialogueParticipantSnapshot(recipient, "Recipient", "colonist", true, true),
                    new DialogueParticipantSnapshot(witness, "Witness", "colonist", true, true)
                });
            var request = new DialogueRequest(
                requestId,
                conversationId,
                DialogueTriggerKind.Social,
                DialoguePriority.Normal,
                speaker,
                speaker,
                recipient,
                scene,
                new[] { sourceEventId },
                10,
                100,
                "offline-dialogue");
            var state = new ConversationStateMachine(
                conversationId,
                new[] { speaker, recipient, witness },
                1,
                10);
            state.QueueTurn(request, 10);
            state.Apply(ConversationTransition.Dispatch, 11);

            var assembler = new DialogueContextAssembler();
            var context = assembler.Build(
                speaker,
                recipient,
                11,
                new[]
                {
                    new DialogueContextItem(
                        "scene-fact",
                        "The recipient is present.",
                        new[] { sourceEventId },
                        DialogueContextAudience.Public,
                        1.0),
                    new DialogueContextItem(
                        "foreign-private",
                        "FOREIGN_PRIVATE_SENTINEL",
                        new[] { sourceEventId },
                        DialogueContextAudience.PrivateSelf,
                        1.0,
                        outsider),
                    new DialogueContextItem(
                        "observer",
                        "OBSERVER_SENTINEL",
                        new[] { sourceEventId },
                        DialogueContextAudience.ObserverOnly,
                        1.0)
                },
                new DialogueContextBudget(8, 5000));
            assertions.Equal(1, context.Items.Count,
                "Context filtering excludes foreign-private and observer-only data.");

            var prompt = new DialoguePromptPlanner().Build(request, context);
            assertions.True(
                !prompt.Segments[0].Content.Contains("FOREIGN_PRIVATE_SENTINEL", StringComparison.Ordinal) &&
                !prompt.Segments[0].Content.Contains("OBSERVER_SENTINEL", StringComparison.Ordinal),
                "Filtered world data never enters the fixed system instruction.");

            var expectedProposal = new UtteranceProposal(
                utteranceId,
                request.Id,
                request.ConversationId,
                speaker,
                recipient,
                "I saw you arrive.",
                new[] { sourceEventId },
                12);
            var modelRequest = new ModelRequest(
                new RequestId(request.Id.Value),
                speaker,
                new LineageId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
                0,
                ModelTaskKind.GenerateDialogueUtterance,
                "mosaic-dialogue-v1",
                prompt.Segments[0].Content,
                prompt.Segments[1].Content,
                UtteranceProposalJson.ProviderCompatibleSchema,
                request.SourceEventIds,
                AffectVector.Neutral,
                occurredAt.AddMinutes(1),
                512);
            var fake = new DeterministicFakeProvider(
                FakeProviderBehavior.Success,
                UtteranceProposalJson.Serialize(expectedProposal));
            var modelResult = await fake.GenerateStructuredAsync(
                modelRequest,
                CancellationToken.None).ConfigureAwait(false);
            assertions.Equal(ModelResultStatus.Success, modelResult.Status,
                "The deterministic fake provider returns the prepared offline payload.");
            state.Apply(ConversationTransition.ReceiveProposal, 12);

            var foreignSpeakerRejected = false;
            try
            {
                UtteranceProposalJson.Parse(
                    modelResult.StructuredPayload.Replace(
                        speaker.ToString(),
                        outsider.ToString()),
                    request,
                    utteranceId,
                    12);
            }
            catch (InvalidDataException)
            {
                foreignSpeakerRejected = true;
            }
            assertions.True(foreignSpeakerRejected,
                "The integrated strict decode rejects a foreign speaker.");

            var ungroundedEvidenceRejected = false;
            try
            {
                UtteranceProposalJson.Parse(
                    modelResult.StructuredPayload.Replace(
                        sourceEventId.ToString(),
                        EventId.Parse("50000000000000000000000000000002").ToString()),
                    request,
                    utteranceId,
                    12);
            }
            catch (InvalidDataException)
            {
                ungroundedEvidenceRejected = true;
            }
            assertions.True(ungroundedEvidenceRejected,
                "The integrated strict decode rejects ungrounded evidence.");

            var decoded = UtteranceProposalJson.Parse(
                modelResult.StructuredPayload,
                request,
                utteranceId,
                12);
            var validation = new UtteranceValidator().Validate(
                request,
                decoded,
                12,
                new HashSet<UtteranceId>(),
                new UtteranceValidationPolicy(true, 600));
            assertions.True(validation.IsAccepted,
                "Strictly decoded grounded speech passes the independent validator.");
            var admittedIds = new HashSet<UtteranceId> { decoded.Id };
            assertions.Equal(
                UtteranceValidationDisposition.DuplicateUtterance,
                new UtteranceValidator().Validate(
                    request,
                    decoded,
                    12,
                    admittedIds,
                    new UtteranceValidationPolicy(true, 600)).Disposition,
                "The integrated validator rejects a replayed utterance ID.");
            assertions.Equal(
                UtteranceValidationDisposition.RequestExpired,
                new UtteranceValidator().Validate(
                    request,
                    decoded,
                    request.ExpiresAtTick,
                    new HashSet<UtteranceId>(),
                    new UtteranceValidationPolicy(true, 600)).Disposition,
                "The integrated validator rejects output after request expiry.");
            state.Apply(ConversationTransition.AcceptValidation, 13);

            var receipt = new DisplayedUtteranceReceipt(
                validation.Utterance!,
                14,
                occurredAt,
                DialogueDisclosure.WitnessesOnly,
                DialoguePresentationChannel.Overlay,
                new[] { speaker, recipient, witness });
            state.Apply(ConversationTransition.RecordDisplay, 14);
            var plan = new DialogueEventAdmissionService("rimworld").Prepare(request, receipt);
            assertions.True(plan.IsPrepared,
                "Actually displayed validated speech prepares one factual admission.");
            assertions.True(plan.Plan!.JournalRecord.Perception is null &&
                            plan.Plan.JournalRecord.Memory is null,
                "Admission prepares no automatic perception or autobiographical memory.");
            state.Apply(ConversationTransition.PrepareAdmission, 15);

            var ledger = new InMemoryEventLedger();
            assertions.Equal(EventAppendStatus.Appended, ledger.Append(plan.Plan.FactualEvent).Status,
                "The first factual dialogue event append succeeds.");
            assertions.Equal(EventAppendStatus.DuplicateEventId, ledger.Append(plan.Plan.FactualEvent).Status,
                "Replaying the same utterance cannot append a second factual event.");
            state.Apply(ConversationTransition.ConfirmAdmission, 16);

            var history = new DialogueHistoryProjection().Build(
                ledger.Snapshot(),
                new DialogueHistoryQuery(
                    conversationId,
                    100,
                    10,
                    DialogueHistoryView.Participant,
                    outsider));
            var factualEvent = plan.Plan.FactualEvent;
            var fingerprint = string.Join(
                "|",
                factualEvent.Id,
                factualEvent.DeduplicationKey,
                factualEvent.Kind,
                factualEvent.GameTick,
                string.Join(";", factualEvent.FactualPayload
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value)),
                string.Join(",", factualEvent.Subjects
                    .OrderBy(id => id.ToString(), StringComparer.Ordinal)));

            return new DialogueRunResult(
                fingerprint,
                ledger.Snapshot().Count,
                plan.Plan.JournalRecord.Memory is null ? 0 : 1,
                history.Entries.Count);
        }

        private sealed class DialogueRunResult
        {
            public DialogueRunResult(
                string fingerprint,
                int ledgerCount,
                int witnessMemoryCount,
                int unwitnessedHistoryCount)
            {
                Fingerprint = fingerprint;
                LedgerCount = ledgerCount;
                WitnessMemoryCount = witnessMemoryCount;
                UnwitnessedHistoryCount = unwitnessedHistoryCount;
            }

            public string Fingerprint { get; }
            public int LedgerCount { get; }
            public int WitnessMemoryCount { get; }
            public int UnwitnessedHistoryCount { get; }
        }
    }
}
