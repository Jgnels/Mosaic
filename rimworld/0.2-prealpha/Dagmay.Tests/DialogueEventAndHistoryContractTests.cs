using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class DialogueEventAndHistoryContractTests
    {
        public static void DisplayReceiptRequiresActualChronologyAndAudience()
        {
            var fixture = CreateFixture(10, "hello");

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new DisplayedUtteranceReceipt(
                    fixture.Validated,
                    9,
                    fixture.DisplayedAtUtc,
                    DialogueDisclosure.WitnessesOnly,
                    DialoguePresentationChannel.Overlay,
                    new[] { fixture.Speaker, fixture.Recipient }),
                "A displayed receipt must not precede generation.");

            TestAssert.Throws<ArgumentException>(
                () => new DisplayedUtteranceReceipt(
                    fixture.Validated,
                    11,
                    fixture.DisplayedAtUtc,
                    DialogueDisclosure.WitnessesOnly,
                    DialoguePresentationChannel.Overlay,
                    new[] { fixture.Speaker }),
                "The recipient must be part of the observed audience.");
        }

        public static void DialogueHistoryBoundariesRejectUndefinedEnumsAndIncompletePayloads()
        {
            var fixture = CreateFixture(10, "strict payload");

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new DisplayedUtteranceReceipt(
                    fixture.Validated,
                    11,
                    fixture.DisplayedAtUtc,
                    (DialogueDisclosure)999,
                    DialoguePresentationChannel.Overlay,
                    new[] { fixture.Speaker, fixture.Recipient }),
                "Undefined disclosure values must fail before factual admission.");
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    (DialogueHistoryView)999),
                "Undefined history views must not fall through to observer access.");

            var legitimate = new DialogueEventAdmissionService("rimworld")
                .Prepare(fixture.Request, fixture.CreateReceipt(12))
                .Plan!
                .FactualEvent;
            var incompletePayload = legitimate.FactualPayload.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            incompletePayload.Remove(DialogueEventAdmissionService.RecipientIdKey);
            var incomplete = new EnvironmentEvent(
                legitimate.Id,
                legitimate.DeduplicationKey,
                legitimate.Kind,
                legitimate.Environment,
                legitimate.OccurredAtUtc,
                legitimate.ObservedAtUtc,
                legitimate.GameTick,
                legitimate.Source,
                incompletePayload,
                legitimate.Subjects);
            var result = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, incomplete) },
                new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    DialogueHistoryView.Observer));

            TestAssert.Equal(0, result.Entries.Count,
                "A dialogue event missing the required recipient field must not enter history.");
            TestAssert.Equal(1, result.MalformedEventCount,
                "Incomplete dialogue payloads should remain visible to diagnostics.");
        }

        public static void AdmissionCreatesFactualOnlyReplayStableEvent()
        {
            var fixture = CreateFixture(10, "I saw the storm coming.");
            var receipt = fixture.CreateReceipt(12);
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");
            var result = service.Prepare(fixture.Request, receipt);

            TestAssert.True(result.IsPrepared, "A valid displayed utterance should prepare for canonical admission.");
            var plan = result.Plan!;
            TestAssert.Equal(
                new EventId(fixture.Proposal.Id.Value),
                plan.FactualEvent.Id,
                "The factual EventId must be deterministically derived from UtteranceId.");
            TestAssert.Equal(
                DialogueEventAdmissionService.DeduplicationKey(fixture.Proposal.Id),
                plan.FactualEvent.DeduplicationKey,
                "The dialogue deduplication key must be stable.");
            TestAssert.Equal(
                fixture.Proposal.Text,
                plan.FactualEvent.FactualPayload[DialogueEventAdmissionService.TextKey],
                "The factual event should preserve the exact displayed text.");
            TestAssert.True(plan.JournalRecord.Perception is null,
                "Displaying speech must not automatically create a perception interpretation.");
            TestAssert.True(plan.JournalRecord.Memory is null,
                "Displaying speech must not automatically create autobiographical memory.");
            TestAssert.False(
                plan.FactualEvent.FactualPayload.ContainsKey("relationship_delta"),
                "Dialogue admission must not directly assign relationship changes.");
            TestAssert.False(
                plan.FactualEvent.FactualPayload.ContainsKey("action"),
                "Dialogue admission must not carry generated game actions.");

            var ledger = new InMemoryEventLedger();
            TestAssert.Equal(
                EventAppendStatus.Appended,
                ledger.Append(plan.FactualEvent).Status,
                "The first admission should append.");
            TestAssert.Equal(
                EventAppendStatus.DuplicateEventId,
                ledger.Append(plan.FactualEvent).Status,
                "Replaying the same displayed utterance must not append twice.");
        }

        public static void AdmissionRejectsRequestOrAudienceMismatch()
        {
            var fixture = CreateFixture(10, "hello");
            var otherRequest = CreateRequest(
                fixture.Speaker,
                fixture.Recipient,
                fixture.Witness,
                fixture.Source,
                fixture.Request.ConversationId,
                DialogueRequestId.New(),
                10,
                100);
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");

            TestAssert.Equal(
                DialogueAdmissionDisposition.RequestMismatch,
                service.Prepare(otherRequest, fixture.CreateReceipt(12)).Disposition,
                "A validated utterance cannot be admitted against a different request.");

            var outsider = IndividualId.New();
            var receipt = new DisplayedUtteranceReceipt(
                fixture.Validated,
                12,
                fixture.DisplayedAtUtc,
                DialogueDisclosure.WitnessesOnly,
                DialoguePresentationChannel.Overlay,
                new[] { fixture.Speaker, fixture.Recipient, outsider });
            TestAssert.Equal(
                DialogueAdmissionDisposition.AudienceOutsideScene,
                service.Prepare(fixture.Request, receipt).Disposition,
                "An audience member absent from the captured scene must fail closed.");
        }

        public static void AdmissionRejectsDisplayAfterRequestExpiry()
        {
            var fixture = CreateFixture(10, "too late");
            var service = new DialogueEventAdmissionService("rimworld");
            var receipt = fixture.CreateReceipt(fixture.Request.ExpiresAtTick);

            TestAssert.Equal(
                DialogueAdmissionDisposition.DisplayedAfterExpiry,
                service.Prepare(fixture.Request, receipt).Disposition,
                "A line displayed at or after request expiry must not become canonical dialogue evidence.");
        }

        public static void HistoryRejectsSpoofedDialogueSource()
        {
            var fixture = CreateFixture(10, "spoofed source");
            var legitimate = new DialogueEventAdmissionService("rimworld").Prepare(
                fixture.Request,
                fixture.CreateReceipt(12)).Plan!.FactualEvent;
            var spoofed = new EnvironmentEvent(
                legitimate.Id,
                legitimate.DeduplicationKey,
                legitimate.Kind,
                legitimate.Environment,
                legitimate.OccurredAtUtc,
                legitimate.ObservedAtUtc,
                legitimate.GameTick,
                "untrusted.mod.dialogue",
                legitimate.FactualPayload.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal),
                legitimate.Subjects);

            var result = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, spoofed) },
                new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    DialogueHistoryView.Observer));

            TestAssert.Equal(0, result.Entries.Count,
                "A correctly shaped event from an unexpected source must not enter dialogue history.");
            TestAssert.Equal(1, result.MalformedEventCount,
                "Source-spoofed dialogue should remain visible to diagnostics.");
        }

        public static void ParticipantHistoryContainsOnlyWitnessedSpeech()
        {
            var conversation = ConversationId.New();
            var speaker = IndividualId.New();
            var recipient = IndividualId.New();
            var witnessA = IndividualId.New();
            var witnessB = IndividualId.New();
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");

            var first = CreateFixture(10, "first", conversation, speaker, recipient, witnessA)
                .CreatePlan(service, 12, new[] { speaker, recipient, witnessA });
            var second = CreateFixture(20, "second", conversation, speaker, recipient, witnessB)
                .CreatePlan(service, 22, new[] { speaker, recipient, witnessB });
            var entries = new[]
            {
                new EventLedgerEntry(1, first.FactualEvent),
                new EventLedgerEntry(2, second.FactualEvent)
            };

            var projection = new DialogueHistoryProjection();
            var participant = projection.Build(
                entries,
                new DialogueHistoryQuery(
                    conversation,
                    100,
                    20,
                    DialogueHistoryView.Participant,
                    witnessA));
            var observer = projection.Build(
                entries,
                new DialogueHistoryQuery(
                    conversation,
                    100,
                    20,
                    DialogueHistoryView.Observer));

            TestAssert.Equal(1, participant.Entries.Count,
                "A participant history must include only speech the viewer witnessed.");
            TestAssert.Equal("first", participant.Entries[0].Text,
                "The witnessed line should be preserved exactly.");
            TestAssert.Equal(1, participant.PrivacyFilteredCount,
                "Unwitnessed matching speech should be counted as privacy-filtered.");
            TestAssert.Equal(2, observer.Entries.Count,
                "Observer history may inspect both canonical factual events.");
        }

        public static void HistoryRejectsMalformedDialogueEvents()
        {
            var conversation = ConversationId.New();
            var malformed = new EnvironmentEvent(
                EventId.New(),
                "malformed-dialogue",
                DialogueEventAdmissionService.EventKind,
                "rimworld",
                new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
                10,
                "mosaic.dialogue.presentation",
                new Dictionary<string, string>
                {
                    [DialogueEventAdmissionService.ConversationIdKey] = conversation.ToString(),
                    [DialogueEventAdmissionService.TextKey] = "missing required identifiers"
                },
                new[] { IndividualId.New() });

            var result = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, malformed) },
                new DialogueHistoryQuery(
                    conversation,
                    100,
                    10,
                    DialogueHistoryView.Observer));

            TestAssert.Equal(0, result.Entries.Count,
                "Malformed dialogue events must never enter history.");
            TestAssert.Equal(1, result.MalformedEventCount,
                "Malformed dialogue events should remain visible to diagnostics.");
        }

        public static void HistoryOrderingAndTrimmingAreDeterministic()
        {
            var conversation = ConversationId.New();
            var speaker = IndividualId.New();
            var recipient = IndividualId.New();
            var witness = IndividualId.New();
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");
            var plans = new[]
            {
                CreateFixture(30, "third", conversation, speaker, recipient, witness).CreatePlan(service, 31),
                CreateFixture(10, "first", conversation, speaker, recipient, witness).CreatePlan(service, 11),
                CreateFixture(20, "second", conversation, speaker, recipient, witness).CreatePlan(service, 21)
            };
            var reversed = plans.Reverse()
                .Select((plan, index) => new EventLedgerEntry(index + 1, plan.FactualEvent))
                .ToArray();

            var result = new DialogueHistoryProjection().Build(
                reversed,
                new DialogueHistoryQuery(
                    conversation,
                    100,
                    2,
                    DialogueHistoryView.Participant,
                    witness));

            TestAssert.Equal(2, result.Entries.Count,
                "The history cap should retain exactly the newest entries.");
            TestAssert.Equal("second", result.Entries[0].Text,
                "Selected history should remain chronological after trimming.");
            TestAssert.Equal("third", result.Entries[1].Text,
                "The newest line should remain last.");
            TestAssert.Equal(1, result.TrimmedCount,
                "Trimming should be explicit for diagnostics.");
        }

        public static void HistoryContextUsesCanonicalEventsWithoutOwningMemory()
        {
            var fixture = CreateFixture(10, "We should keep watch tonight.");
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");
            var plan = fixture.CreatePlan(service, 12);
            var packet = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, plan.FactualEvent) },
                new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    DialogueHistoryView.Participant,
                    fixture.Speaker));

            var context = new DialogueHistoryContextProjector().Build(
                packet,
                fixture.Speaker,
                fixture.Recipient,
                5);

            TestAssert.Equal(1, context.Count,
                "Witnessed canonical dialogue should project into recent-conversation context.");
            TestAssert.Equal(
                plan.FactualEvent.Id,
                context[0].EvidenceIds[0],
                "Projected history must remain grounded in the canonical dialogue event.");
            TestAssert.Equal(
                DialogueContextAudience.RelationshipSensitive,
                context[0].Audience,
                "Conversation history shared with the same recipient should remain relationship-sensitive.");
        }

        public static void HistoryContextDoesNotLeakToUnwitnessedRecipient()
        {
            var fixture = CreateFixture(10, "This stays between us.");
            var service = new DialogueEventAdmissionService("rimworld", "mosaic.dialogue.presentation");
            var plan = fixture.CreatePlan(service, 12, new[] { fixture.Speaker, fixture.Recipient });
            var packet = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, plan.FactualEvent) },
                new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    DialogueHistoryView.Participant,
                    fixture.Speaker));

            var outsider = IndividualId.New();
            var context = new DialogueHistoryContextProjector().Build(
                packet,
                fixture.Speaker,
                outsider,
                5);

            TestAssert.Equal(0, context.Count,
                "Exact conversation history must not be projected to an unwitnessed recipient.");
        }

        public static void HistoryContextBoundsLongSpeechWithoutChangingCanonicalEvent()
        {
            var longText = new string('x', 1800);
            var fixture = CreateFixture(10, longText);
            var service = new DialogueEventAdmissionService("rimworld");
            var plan = fixture.CreatePlan(service, 12);
            var packet = new DialogueHistoryProjection().Build(
                new[] { new EventLedgerEntry(1, plan.FactualEvent) },
                new DialogueHistoryQuery(
                    fixture.Request.ConversationId,
                    100,
                    10,
                    DialogueHistoryView.Participant,
                    fixture.Speaker));

            var context = new DialogueHistoryContextProjector().Build(
                packet,
                fixture.Speaker,
                fixture.Recipient,
                5);

            TestAssert.Equal(longText, plan.FactualEvent.FactualPayload[DialogueEventAdmissionService.TextKey],
                "The canonical factual event must retain the admitted spoken text.");
            TestAssert.True(context[0].Text.Length <= 2048,
                "Ephemeral prompt context must obey the existing DialogueContextItem text bound.");
            TestAssert.True(context[0].Text.Contains("[truncated]", StringComparison.Ordinal),
                "A bounded projection should mark that a long canonical line was truncated for context.");
        }

        public static void CheckpointAdmissionPolicyFailsClosedBeforeCommit()
        {
            var fixture = CreateFixture(10, "checkpoint aligned");
            var plan = fixture.CreatePlan(
                new DialogueEventAdmissionService("rimworld"),
                12);
            var policy = new CheckpointAlignedDialogueAdmissionPolicy();
            var admitted = new HashSet<EventId>();

            TestAssert.Equal(
                CheckpointAdmissionDisposition.StaleCheckpoint,
                policy.Evaluate(plan, 4, 3, true, ExperienceJournalLoadStatus.Loaded, admitted),
                "A stale save checkpoint must block dialogue admission.");
            TestAssert.Equal(
                CheckpointAdmissionDisposition.ReadOnlyStorage,
                policy.Evaluate(plan, 4, 4, false, ExperienceJournalLoadStatus.Loaded, admitted),
                "Read-only storage must block dialogue admission.");
            TestAssert.Equal(
                CheckpointAdmissionDisposition.InvalidJournal,
                policy.Evaluate(plan, 4, 4, true, ExperienceJournalLoadStatus.Invalid, admitted),
                "A malformed existing journal must block dialogue admission.");

            admitted.Add(plan.FactualEvent.Id);
            TestAssert.Equal(
                CheckpointAdmissionDisposition.AlreadyAdmitted,
                policy.Evaluate(plan, 4, 4, true, ExperienceJournalLoadStatus.Loaded, admitted),
                "Recovery must recognize an already admitted deterministic EventId.");
            admitted.Clear();
            TestAssert.Equal(
                CheckpointAdmissionDisposition.Ready,
                policy.Evaluate(plan, 4, 4, true, ExperienceJournalLoadStatus.Loaded, admitted),
                "Only an exact writable healthy checkpoint may proceed to a future atomic coordinator.");
        }

        public static void CurrentAdmissionSurfacesCannotProvideAtomicDualWrite()
        {
            var fixture = CreateFixture(10, "dual write limiting result");
            var plan = fixture.CreatePlan(
                new DialogueEventAdmissionService("rimworld"),
                12);
            var directory = Path.Combine(
                Path.GetTempPath(),
                "mosaic-dialogue-atomicity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var journal = new DurableExperienceJournal();
                var journalFirstPath = Path.Combine(directory, "journal-first.journal");
                journal.Append(journalFirstPath, plan.JournalRecord, 0, string.Empty);

                var failingLedger = new ThrowingEventLedger();
                TestAssert.Throws<IOException>(
                    () => failingLedger.Append(plan.FactualEvent),
                    "A ledger failure can occur after the journal has already flushed.");
                var journalFirst = journal.Load(journalFirstPath);
                TestAssert.Equal(1, journalFirst.Records.Count,
                    "Journal-first failure leaves a durable one-sided admission.");

                var ledgerFirst = new InMemoryEventLedger();
                TestAssert.Equal(
                    EventAppendStatus.Appended,
                    ledgerFirst.Append(plan.FactualEvent).Status,
                    "Ledger-first ordering can commit the factual event.");
                TestAssert.Throws<ArgumentOutOfRangeException>(
                    () => journal.Append(
                        Path.Combine(directory, "ledger-first.journal"),
                        plan.JournalRecord,
                        DurableExperienceJournal.MaximumRecordCount,
                        string.Empty),
                    "A journal failure can occur after the event ledger has changed.");
                TestAssert.Equal(1, ledgerFirst.Snapshot().Count,
                    "Ledger-first failure also leaves a one-sided admission.");

                var restarted = journal.Load(journalFirstPath);
                journal.Append(
                    journalFirstPath,
                    plan.JournalRecord,
                    restarted.Records.Count,
                    restarted.LastHash);
                var replayedJournal = journal.Load(journalFirstPath);
                TestAssert.Equal(2, replayedJournal.Records.Count,
                    "Restart replay can duplicate the same factual event in the append-only journal.");
                TestAssert.Equal(
                    replayedJournal.Records[0].FactualEvent.Id,
                    replayedJournal.Records[1].FactualEvent.Id,
                    "The journal has no admission-level EventId deduplication.");

                TestAssert.Equal(
                    EventAppendStatus.DuplicateEventId,
                    ledgerFirst.Append(plan.FactualEvent).Status,
                    "The event ledger independently prevents a second canonical event.");
                TestAssert.Equal(1, ledgerFirst.Snapshot().Count,
                    "Recovery must never create two canonical dialogue events.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static Fixture CreateFixture(
            long generatedAtTick,
            string text,
            ConversationId? conversation = null,
            IndividualId? speaker = null,
            IndividualId? recipient = null,
            IndividualId? witness = null)
        {
            var actualSpeaker = speaker ?? IndividualId.New();
            var actualRecipient = recipient ?? IndividualId.New();
            var actualWitness = witness ?? IndividualId.New();
            var source = EventId.New();
            var request = CreateRequest(
                actualSpeaker,
                actualRecipient,
                actualWitness,
                source,
                conversation ?? ConversationId.New(),
                DialogueRequestId.New(),
                generatedAtTick,
                generatedAtTick + 1000);
            var proposal = new UtteranceProposal(
                UtteranceId.New(),
                request.Id,
                request.ConversationId,
                actualSpeaker,
                actualRecipient,
                text,
                new[] { source },
                generatedAtTick);
            var validation = new UtteranceValidator().Validate(
                request,
                proposal,
                generatedAtTick,
                new HashSet<UtteranceId>(),
                new UtteranceValidationPolicy(true, 4000));
            TestAssert.True(validation.IsAccepted, "The test fixture utterance should validate.");

            return new Fixture(
                actualSpeaker,
                actualRecipient,
                actualWitness,
                source,
                request,
                proposal,
                validation.Utterance!,
                new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero).AddTicks(generatedAtTick));
        }

        private static DialogueRequest CreateRequest(
            IndividualId speaker,
            IndividualId recipient,
            IndividualId witness,
            EventId source,
            ConversationId conversation,
            DialogueRequestId requestId,
            long createdAtTick,
            long expiresAtTick)
        {
            var scene = new DialogueSceneSnapshot(
                source,
                createdAtTick,
                "A quiet colony room.",
                new[]
                {
                    new DialogueParticipantSnapshot(speaker, "Speaker", "colonist", true, true),
                    new DialogueParticipantSnapshot(recipient, "Recipient", "colonist", true, true),
                    new DialogueParticipantSnapshot(witness, "Witness", "colonist", true, true)
                });
            return new DialogueRequest(
                requestId,
                conversation,
                DialogueTriggerKind.Social,
                DialoguePriority.Normal,
                speaker,
                speaker,
                recipient,
                scene,
                new[] { source },
                createdAtTick,
                expiresAtTick,
                "dialogue-history-test");
        }

        private sealed class Fixture
        {
            public Fixture(
                IndividualId speaker,
                IndividualId recipient,
                IndividualId witness,
                EventId source,
                DialogueRequest request,
                UtteranceProposal proposal,
                ValidatedUtterance validated,
                DateTimeOffset displayedAtUtc)
            {
                Speaker = speaker;
                Recipient = recipient;
                Witness = witness;
                Source = source;
                Request = request;
                Proposal = proposal;
                Validated = validated;
                DisplayedAtUtc = displayedAtUtc;
            }

            public IndividualId Speaker { get; }
            public IndividualId Recipient { get; }
            public IndividualId Witness { get; }
            public EventId Source { get; }
            public DialogueRequest Request { get; }
            public UtteranceProposal Proposal { get; }
            public ValidatedUtterance Validated { get; }
            public DateTimeOffset DisplayedAtUtc { get; }

            public DisplayedUtteranceReceipt CreateReceipt(
                long displayedAtTick,
                IEnumerable<IndividualId>? audience = null)
            {
                return new DisplayedUtteranceReceipt(
                    Validated,
                    displayedAtTick,
                    DisplayedAtUtc,
                    DialogueDisclosure.WitnessesOnly,
                    DialoguePresentationChannel.Overlay,
                    audience ?? new[] { Speaker, Recipient, Witness });
            }

            public DialogueEventAdmissionPlan CreatePlan(
                DialogueEventAdmissionService service,
                long displayedAtTick,
                IEnumerable<IndividualId>? audience = null)
            {
                var result = service.Prepare(Request, CreateReceipt(displayedAtTick, audience));
                TestAssert.True(result.IsPrepared, "The test fixture should prepare for admission.");
                return result.Plan!;
            }
        }

        private sealed class ThrowingEventLedger : IEventLedger
        {
            public EventAppendResult Append(EnvironmentEvent value)
            {
                throw new IOException("Injected event-ledger failure.");
            }

            public bool Contains(EventId id) => false;

            public IReadOnlyList<EventLedgerEntry> Snapshot()
            {
                return Array.Empty<EventLedgerEntry>();
            }
        }
    }
}
