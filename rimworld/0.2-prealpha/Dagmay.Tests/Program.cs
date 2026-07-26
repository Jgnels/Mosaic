using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dagmay.Tests
{
    internal static class Program
    {
        public static async Task<int> Main()
        {
            var tests = new List<(string Name, Func<Task> Run)>
            {
                (nameof(CoreContractTests.StrongIdsRoundTripAndRejectEmpty), Sync(CoreContractTests.StrongIdsRoundTripAndRejectEmpty)),
                (nameof(CoreContractTests.AffectIsBoundedAndComposable), Sync(CoreContractTests.AffectIsBoundedAndComposable)),
                (nameof(CoreContractTests.ContinuityUsesAcceptedWeightedCombination), Sync(CoreContractTests.ContinuityUsesAcceptedWeightedCombination)),
                (nameof(CoreContractTests.RenamePreservesIdentityAndLineage), Sync(CoreContractTests.RenamePreservesIdentityAndLineage)),
                (nameof(CoreContractTests.InWorldRevivalContinuesTheSameLineage), Sync(CoreContractTests.InWorldRevivalContinuesTheSameLineage)),
                (nameof(CoreContractTests.LedgerDeduplicatesEvents), Sync(CoreContractTests.LedgerDeduplicatesEvents)),
                (nameof(CoreContractTests.PerspectivesCanDisagreeWithoutChangingFacts), Sync(CoreContractTests.PerspectivesCanDisagreeWithoutChangingFacts)),
                (nameof(CoreContractTests.ValidatedMutationIsAtomicAndReplaySafe), Sync(CoreContractTests.ValidatedMutationIsAtomicAndReplaySafe)),
                (nameof(CoreContractTests.ExcessiveOrUngroundedMutationDoesNotChangeState), Sync(CoreContractTests.ExcessiveOrUngroundedMutationDoesNotChangeState)),
                (nameof(CoreContractTests.QueueIsBoundedAndProtectsCriticalWork), Sync(CoreContractTests.QueueIsBoundedAndProtectsCriticalWork)),
                (nameof(CoreContractTests.QueueCoalescingKeysAreScopedToIndividual), Sync(CoreContractTests.QueueCoalescingKeysAreScopedToIndividual)),
                (nameof(CoreContractTests.ReflectionTasksRejectInvalidIdentityAndEvidence), Sync(CoreContractTests.ReflectionTasksRejectInvalidIdentityAndEvidence)),
                (nameof(CoreContractTests.SelectiveEnrollmentCannotCreateHalfIndividuals), Sync(CoreContractTests.SelectiveEnrollmentCannotCreateHalfIndividuals)),
                (nameof(CoreContractTests.InteractionOriginPreventsOperatorImpersonation), Sync(CoreContractTests.InteractionOriginPreventsOperatorImpersonation)),
                (nameof(CoreContractTests.RelationshipsAreAsymmetricByConstruction), Sync(CoreContractTests.RelationshipsAreAsymmetricByConstruction)),
                (nameof(PersistenceContractTests.IdentityArchiveRoundTripPreservesContinuity), Sync(PersistenceContractTests.IdentityArchiveRoundTripPreservesContinuity)),
                (nameof(PersistenceContractTests.CorruptedArchiveCannotBecomeCanonicalState), Sync(PersistenceContractTests.CorruptedArchiveCannotBecomeCanonicalState)),
                (nameof(PersistenceContractTests.AtomicArchiveRecoversVerifiedBackup), Sync(PersistenceContractTests.AtomicArchiveRecoversVerifiedBackup)),
                (nameof(PersistenceContractTests.ArchiveCheckpointExpectationRejectsIdentityAndGenerationMismatch), Sync(PersistenceContractTests.ArchiveCheckpointExpectationRejectsIdentityAndGenerationMismatch)),
                (nameof(PersistenceContractTests.BackupRecoveryCannotRollBackPastSaveCheckpoint), Sync(PersistenceContractTests.BackupRecoveryCannotRollBackPastSaveCheckpoint)),
                (nameof(PersistenceContractTests.UnchangedSaveAsDoesNotAdvanceIdentityGeneration), Sync(PersistenceContractTests.UnchangedSaveAsDoesNotAdvanceIdentityGeneration)),
                (nameof(PersistenceContractTests.InterruptedTemporaryWritePreservesLastKnownGoodArchive), Sync(PersistenceContractTests.InterruptedTemporaryWritePreservesLastKnownGoodArchive)),
                (nameof(PersistenceContractTests.TruncatedAndUnsupportedArchivesFailClosed), Sync(PersistenceContractTests.TruncatedAndUnsupportedArchivesFailClosed)),
                (nameof(PersistenceContractTests.SaveManifestStateMachineFailsClosedOnContradictoryEvidence), Sync(PersistenceContractTests.SaveManifestStateMachineFailsClosedOnContradictoryEvidence)),
                (nameof(ExperienceContractTests.DeterministicEncodingSeparatesFactPerceptionAndMemory), Sync(ExperienceContractTests.DeterministicEncodingSeparatesFactPerceptionAndMemory)),
                (nameof(ExperienceContractTests.ExperienceJournalRoundTripPreservesProvenance), Sync(ExperienceContractTests.ExperienceJournalRoundTripPreservesProvenance)),
                (nameof(ExperienceContractTests.ExperienceJournalRejectsTampering), Sync(ExperienceContractTests.ExperienceJournalRejectsTampering)),
                (nameof(ExperienceContractTests.OrdinaryDisclosureExcludesPrivateMemoriesAndDescribesStateCoarsely), Sync(ExperienceContractTests.OrdinaryDisclosureExcludesPrivateMemoriesAndDescribesStateCoarsely)),
                (nameof(ExperienceContractTests.OrdinaryDisclosureEnforcesPrivacyAndAccessibilityBoundary), Sync(ExperienceContractTests.OrdinaryDisclosureEnforcesPrivacyAndAccessibilityBoundary)),
                (nameof(ExperienceContractTests.SocialExperienceLinksOtherIndividualAndRemainsRelationshipSensitive), Sync(ExperienceContractTests.SocialExperienceLinksOtherIndividualAndRemainsRelationshipSensitive)),
                (nameof(ExperienceContractTests.MemoryIndexRetrievesRecentAndSignificantWithoutTranscriptSemantics), Sync(ExperienceContractTests.MemoryIndexRetrievesRecentAndSignificantWithoutTranscriptSemantics)),
                (nameof(ExperienceContractTests.MemoryIndexTieBreaksAreStableAcrossRebuildOrder), Sync(ExperienceContractTests.MemoryIndexTieBreaksAreStableAcrossRebuildOrder)),
                (nameof(SocialPathCertificationContractTests.CertificationRequiresExactCounterpartProvenanceAndOneOwnerMemory), Sync(SocialPathCertificationContractTests.CertificationRequiresExactCounterpartProvenanceAndOneOwnerMemory)),
                (nameof(SocialPathCertificationContractTests.CertificationRequiresOwnerEvidencePostLoadAndHealthyStores), Sync(SocialPathCertificationContractTests.CertificationRequiresOwnerEvidencePostLoadAndHealthyStores)),
                (nameof(ReflectionContractTests.ReflectionProposalRoundTripIsStrict), Sync(ReflectionContractTests.ReflectionProposalRoundTripIsStrict)),
                (nameof(ReflectionContractTests.ReflectionValidationIsAtomicGroundedAndBounded), Sync(ReflectionContractTests.ReflectionValidationIsAtomicGroundedAndBounded)),
                (nameof(ReflectionContractTests.ReflectionContextTreatsWorldTextAsUntrustedData), Sync(ReflectionContractTests.ReflectionContextTreatsWorldTextAsUntrustedData)),
                (nameof(ReflectionContractTests.ReflectionContextExcludesForeignMemoriesAndCapsPrivateContext), Sync(ReflectionContractTests.ReflectionContextExcludesForeignMemoriesAndCapsPrivateContext)),
                (nameof(ReflectionContractTests.ReflectionContextRejectsForeignSourceEvents), Sync(ReflectionContractTests.ReflectionContextRejectsForeignSourceEvents)),
                (nameof(ReflectionContractTests.ReflectionContextOrdersEqualTimeEvidenceDeterministically), Sync(ReflectionContractTests.ReflectionContextOrdersEqualTimeEvidenceDeterministically)),
                (nameof(ReflectionContractTests.PersistentReflectionQueueMergesDefersAndRetries), Sync(ReflectionContractTests.PersistentReflectionQueueMergesDefersAndRetries)),
                (nameof(ReflectionContractTests.PersistentReflectionQueueNeverMergesAcrossIndividuals), Sync(ReflectionContractTests.PersistentReflectionQueueNeverMergesAcrossIndividuals)),
                (nameof(ReflectionContractTests.HigherPriorityMergeRetainsWinningEvidenceAtCapacity), Sync(ReflectionContractTests.HigherPriorityMergeRetainsWinningEvidenceAtCapacity)),
                (nameof(ReflectionContractTests.PersistentReflectionQueueFairTieBreakRotatesIndividuals), Sync(ReflectionContractTests.PersistentReflectionQueueFairTieBreakRotatesIndividuals)),
                (nameof(ReflectionContractTests.ReflectionStoreRoundTripPreservesPendingCommit), Sync(ReflectionContractTests.ReflectionStoreRoundTripPreservesPendingCommit)),
                (nameof(ReflectionContractTests.ReflectionStoreRejectsTamperingAndRecoversBackup), Sync(ReflectionContractTests.ReflectionStoreRejectsTamperingAndRecoversBackup)),
                (nameof(ReflectionContractTests.ReflectionCheckpointExpectationRejectsIdentityGenerationAndStaleBackup), Sync(ReflectionContractTests.ReflectionCheckpointExpectationRejectsIdentityGenerationAndStaleBackup)),
                (nameof(ReflectionContractTests.PostLoadReflectionWritesWaitForRimWorldSaveCheckpoint), Sync(ReflectionContractTests.PostLoadReflectionWritesWaitForRimWorldSaveCheckpoint)),
                (nameof(ReflectionContractTests.ReflectionBudgetEnforcesHourlyDailyAndCircuitLimits), Sync(ReflectionContractTests.ReflectionBudgetEnforcesHourlyDailyAndCircuitLimits)),
                (nameof(ReflectionContractTests.PausedEnrollmentRemovesOnlyTargetedQueuedWork), Sync(ReflectionContractTests.PausedEnrollmentRemovesOnlyTargetedQueuedWork)),
                (nameof(ReflectionContractTests.ObserverUsageDeduplicatesAuditStages), Sync(ReflectionContractTests.ObserverUsageDeduplicatesAuditStages)),
                (nameof(ReflectionContractTests.ObserverMemoryProjectionPreservesProvenance), Sync(ReflectionContractTests.ObserverMemoryProjectionPreservesProvenance)),
                (nameof(ReflectionContractTests.ReadOnlyExperienceStoragePausesReflectionProcessingWithoutQueueMutation), Sync(ReflectionContractTests.ReadOnlyExperienceStoragePausesReflectionProcessingWithoutQueueMutation)),
            (nameof(ReflectionContractTests.EmptyReflectionAuditDoesNotImplyReadOnlyStorage), Sync(ReflectionContractTests.EmptyReflectionAuditDoesNotImplyReadOnlyStorage)),
                (nameof(StorytellingFoundationContractTests.TemporalFactPreservesEvidenceAndSupersedesWithoutErasure), Sync(StorytellingFoundationContractTests.TemporalFactPreservesEvidenceAndSupersedesWithoutErasure)),
                (nameof(StorytellingFoundationContractTests.AppraisalContractsAreBoundedAndSideEffectFreeByConstruction), Sync(StorytellingFoundationContractTests.AppraisalContractsAreBoundedAndSideEffectFreeByConstruction)),
                (nameof(StorytellingFoundationContractTests.DecisionTraceCarriesEvidenceWithoutOwningCanonicalState), Sync(StorytellingFoundationContractTests.DecisionTraceCarriesEvidenceWithoutOwningCanonicalState)),
                (nameof(EnvironmentIntegrationContractTests.ContainmentAndUnresolvedBindingsPreserveIndividualIdentity), Sync(EnvironmentIntegrationContractTests.ContainmentAndUnresolvedBindingsPreserveIndividualIdentity)),
                (nameof(EnvironmentIntegrationContractTests.ConfirmedDestructionCannotSilentlyRebind), Sync(EnvironmentIntegrationContractTests.ConfirmedDestructionCannotSilentlyRebind)),
                (nameof(EnvironmentIntegrationContractTests.ExternalEventProposalStoresPortableDataInsteadOfModObjects), Sync(EnvironmentIntegrationContractTests.ExternalEventProposalStoresPortableDataInsteadOfModObjects)),
                (nameof(ExecutionAndPresentationContractTests.ExternalModExecutionIsNotMosaicAuthorship), Sync(ExecutionAndPresentationContractTests.ExternalModExecutionIsNotMosaicAuthorship)),
                (nameof(ExecutionAndPresentationContractTests.MosaicCommitmentRequiresDecisionProvenance), Sync(ExecutionAndPresentationContractTests.MosaicCommitmentRequiresDecisionProvenance)),
                (nameof(ExecutionAndPresentationContractTests.PresentationContextRequiresEvidenceGrounding), Sync(ExecutionAndPresentationContractTests.PresentationContextRequiresEvidenceGrounding)),
                (nameof(ExecutionAndPresentationContractTests.PresentationContextCollectionsAreBounded), Sync(ExecutionAndPresentationContractTests.PresentationContextCollectionsAreBounded)),
                (nameof(ExecutionAndPresentationContractTests.PresentationContextRejectsUngroundedIdentifiers), Sync(ExecutionAndPresentationContractTests.PresentationContextRejectsUngroundedIdentifiers)),
                (nameof(ExecutionAndPresentationContractTests.ObserverAndPresentationReadsPreserveCanonicalFingerprint), Sync(ExecutionAndPresentationContractTests.ObserverAndPresentationReadsPreserveCanonicalFingerprint)),
                (nameof(DialogueContractTests.DialogueIdentifiersRoundTripAndRejectEmpty), Sync(DialogueContractTests.DialogueIdentifiersRoundTripAndRejectEmpty)),
                (nameof(DialogueContractTests.DialogueContractsRejectDefaultIdsAndUndefinedEnums), Sync(DialogueContractTests.DialogueContractsRejectDefaultIdsAndUndefinedEnums)),
                (nameof(DialogueContractTests.DialogueContextEnforcesAudienceOwnershipAndDeterministicBudget), Sync(DialogueContractTests.DialogueContextEnforcesAudienceOwnershipAndDeterministicBudget)),
                (nameof(DialogueContractTests.DialogueSchedulerNeverCoalescesAcrossIndividualsOrConversations), Sync(DialogueContractTests.DialogueSchedulerNeverCoalescesAcrossIndividualsOrConversations)),
                (nameof(DialogueContractTests.DialogueSchedulerProtectsHigherPriorityWorkAndExpiresStaleWork), Sync(DialogueContractTests.DialogueSchedulerProtectsHigherPriorityWorkAndExpiresStaleWork)),
                (nameof(DialogueContractTests.DialogueSchedulerFairlyRotatesEqualPriorityIndividuals), Sync(DialogueContractTests.DialogueSchedulerFairlyRotatesEqualPriorityIndividuals)),
                (nameof(DialogueContractTests.DialoguePromptKeepsWorldTextOutOfSystemInstruction), Sync(DialogueContractTests.DialoguePromptKeepsWorldTextOutOfSystemInstruction)),
                (nameof(DialogueContractTests.UtteranceValidationRejectsForeignUngroundedAndActionOutput), Sync(DialogueContractTests.UtteranceValidationRejectsForeignUngroundedAndActionOutput)),
                (nameof(DialogueContractTests.UtteranceValidationAcceptsGroundedBoundedSpeechAndDetectsReplay), Sync(DialogueContractTests.UtteranceValidationAcceptsGroundedBoundedSpeechAndDetectsReplay)),
                (nameof(DialogueContractTests.UtteranceProposalCodecRoundTripsDeterministically), Sync(DialogueContractTests.UtteranceProposalCodecRoundTripsDeterministically)),
                (nameof(DialogueContractTests.UtteranceProposalCodecRejectsStructuralAndAuthorityViolations), Sync(DialogueContractTests.UtteranceProposalCodecRejectsStructuralAndAuthorityViolations)),
                (nameof(DialogueContractTests.ConversationStateMachineCompletesBoundedTurns), Sync(DialogueContractTests.ConversationStateMachineCompletesBoundedTurns)),
                (nameof(DialogueContractTests.ConversationStateMachineRejectsInvalidTransitionsAndReplay), Sync(DialogueContractTests.ConversationStateMachineRejectsInvalidTransitionsAndReplay)),
                (nameof(DialogueContractTests.ConversationStateMachineRepresentsFailureTerminals), Sync(DialogueContractTests.ConversationStateMachineRepresentsFailureTerminals)),
                (nameof(DialogueEventAndHistoryContractTests.DisplayReceiptRequiresActualChronologyAndAudience), Sync(DialogueEventAndHistoryContractTests.DisplayReceiptRequiresActualChronologyAndAudience)),
                (nameof(DialogueEventAndHistoryContractTests.DialogueHistoryBoundariesRejectUndefinedEnumsAndIncompletePayloads), Sync(DialogueEventAndHistoryContractTests.DialogueHistoryBoundariesRejectUndefinedEnumsAndIncompletePayloads)),
                (nameof(DialogueEventAndHistoryContractTests.AdmissionCreatesFactualOnlyReplayStableEvent), Sync(DialogueEventAndHistoryContractTests.AdmissionCreatesFactualOnlyReplayStableEvent)),
                (nameof(DialogueEventAndHistoryContractTests.AdmissionRejectsRequestOrAudienceMismatch), Sync(DialogueEventAndHistoryContractTests.AdmissionRejectsRequestOrAudienceMismatch)),
                (nameof(DialogueEventAndHistoryContractTests.AdmissionRejectsDisplayAfterRequestExpiry), Sync(DialogueEventAndHistoryContractTests.AdmissionRejectsDisplayAfterRequestExpiry)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryRejectsSpoofedDialogueSource), Sync(DialogueEventAndHistoryContractTests.HistoryRejectsSpoofedDialogueSource)),
                (nameof(DialogueEventAndHistoryContractTests.ParticipantHistoryContainsOnlyWitnessedSpeech), Sync(DialogueEventAndHistoryContractTests.ParticipantHistoryContainsOnlyWitnessedSpeech)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryRejectsMalformedDialogueEvents), Sync(DialogueEventAndHistoryContractTests.HistoryRejectsMalformedDialogueEvents)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryOrderingAndTrimmingAreDeterministic), Sync(DialogueEventAndHistoryContractTests.HistoryOrderingAndTrimmingAreDeterministic)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryContextUsesCanonicalEventsWithoutOwningMemory), Sync(DialogueEventAndHistoryContractTests.HistoryContextUsesCanonicalEventsWithoutOwningMemory)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryContextDoesNotLeakToUnwitnessedRecipient), Sync(DialogueEventAndHistoryContractTests.HistoryContextDoesNotLeakToUnwitnessedRecipient)),
                (nameof(DialogueEventAndHistoryContractTests.HistoryContextBoundsLongSpeechWithoutChangingCanonicalEvent), Sync(DialogueEventAndHistoryContractTests.HistoryContextBoundsLongSpeechWithoutChangingCanonicalEvent)),
                (nameof(DialogueEventAndHistoryContractTests.CheckpointAdmissionPolicyFailsClosedBeforeCommit), Sync(DialogueEventAndHistoryContractTests.CheckpointAdmissionPolicyFailsClosedBeforeCommit)),
                (nameof(DialogueEventAndHistoryContractTests.CurrentAdmissionSurfacesCannotProvideAtomicDualWrite), Sync(DialogueEventAndHistoryContractTests.CurrentAdmissionSurfacesCannotProvideAtomicDualWrite)),
                (nameof(SpeechBubblePresentationContractTests.QueueBoundsAndEvictionAreDeterministic), Sync(SpeechBubblePresentationContractTests.QueueBoundsAndEvictionAreDeterministic)),
                (nameof(SpeechBubblePresentationContractTests.PerSpeakerBoundCannotBeBrokenByGlobalEviction), Sync(SpeechBubblePresentationContractTests.PerSpeakerBoundCannotBeBrokenByGlobalEviction)),
                (nameof(SpeechBubblePresentationContractTests.DuplicateUtterancesCannotDisplayTwice), Sync(SpeechBubblePresentationContractTests.DuplicateUtterancesCannotDisplayTwice)),
                (nameof(SpeechBubblePresentationContractTests.IdentityNotDisplayNameControlsIsolationAndRename), Sync(SpeechBubblePresentationContractTests.IdentityNotDisplayNameControlsIsolationAndRename)),
                (nameof(SpeechBubblePresentationContractTests.UnicodeWrappingIsBoundedAndTextElementSafe), Sync(SpeechBubblePresentationContractTests.UnicodeWrappingIsBoundedAndTextElementSafe)),
                (nameof(SpeechBubblePresentationContractTests.TimingAndScreenClampHonorBounds), Sync(SpeechBubblePresentationContractTests.TimingAndScreenClampHonorBounds)),
                (nameof(SpeechBubblePresentationContractTests.LifecycleAndDisposalAreIdempotentAndSilent), Sync(SpeechBubblePresentationContractTests.LifecycleAndDisposalAreIdempotentAndSilent)),
                (nameof(SpeechBubblePresentationContractTests.AvailabilityFailsClosedForThreadBindingAndMapLoss), Sync(SpeechBubblePresentationContractTests.AvailabilityFailsClosedForThreadBindingAndMapLoss)),
                (nameof(SpeechBubblePresentationContractTests.ReceiptsReportOnlyTheActualSuccessfulChannel), Sync(SpeechBubblePresentationContractTests.ReceiptsReportOnlyTheActualSuccessfulChannel)),
                (nameof(SpeechBubblePresentationContractTests.PresentationReadsPreserveCanonicalFingerprint), Sync(SpeechBubblePresentationContractTests.PresentationReadsPreserveCanonicalFingerprint)),
                (nameof(DialogueAdmissionOutboxContractTests.CrashBeforeOutboxWriteLeavesNoAdmission), Sync(DialogueAdmissionOutboxContractTests.CrashBeforeOutboxWriteLeavesNoAdmission)),
                (nameof(DialogueAdmissionOutboxContractTests.CrashAfterOutboxBeforeDestinationsLeavesRecoverablePending), Sync(DialogueAdmissionOutboxContractTests.CrashAfterOutboxBeforeDestinationsLeavesRecoverablePending)),
                (nameof(DialogueAdmissionOutboxContractTests.RecoveryAfterLedgerOnlyMaterializesJournalExactlyOnce), Sync(DialogueAdmissionOutboxContractTests.RecoveryAfterLedgerOnlyMaterializesJournalExactlyOnce)),
                (nameof(DialogueAdmissionOutboxContractTests.RecoveryAfterJournalOnlyMaterializesLedgerExactlyOnce), Sync(DialogueAdmissionOutboxContractTests.RecoveryAfterJournalOnlyMaterializesLedgerExactlyOnce)),
                (nameof(DialogueAdmissionOutboxContractTests.RecoveryAfterBothBeforeCompletionMarksTombstoneWithoutDuplicates), Sync(DialogueAdmissionOutboxContractTests.RecoveryAfterBothBeforeCompletionMarksTombstoneWithoutDuplicates)),
                (nameof(DialogueAdmissionOutboxContractTests.CompletedTombstoneSurvivesCurrentCheckpointAndCompactsLater), Sync(DialogueAdmissionOutboxContractTests.CompletedTombstoneSurvivesCurrentCheckpointAndCompactsLater)),
                (nameof(DialogueAdmissionOutboxContractTests.RestartReplayCompletesPendingAdmission), Sync(DialogueAdmissionOutboxContractTests.RestartReplayCompletesPendingAdmission)),
                (nameof(DialogueAdmissionOutboxContractTests.IdenticalDuplicateAdmissionIsIdempotent), Sync(DialogueAdmissionOutboxContractTests.IdenticalDuplicateAdmissionIsIdempotent)),
                (nameof(DialogueAdmissionOutboxContractTests.ConflictingDuplicateEventIdFailsClosed), Sync(DialogueAdmissionOutboxContractTests.ConflictingDuplicateEventIdFailsClosed)),
                (nameof(DialogueAdmissionOutboxContractTests.StalePendingGenerationFailsClosed), Sync(DialogueAdmissionOutboxContractTests.StalePendingGenerationFailsClosed)),
                (nameof(DialogueAdmissionOutboxContractTests.OutboxAheadOfLoadedSaveFailsClosed), Sync(DialogueAdmissionOutboxContractTests.OutboxAheadOfLoadedSaveFailsClosed)),
                (nameof(DialogueAdmissionOutboxContractTests.ReadOnlyStoragePerformsNoWrites), Sync(DialogueAdmissionOutboxContractTests.ReadOnlyStoragePerformsNoWrites)),
                (nameof(DialogueAdmissionOutboxContractTests.InvalidTruncatedJournalBlocksRecovery), Sync(DialogueAdmissionOutboxContractTests.InvalidTruncatedJournalBlocksRecovery)),
                (nameof(DialogueAdmissionOutboxContractTests.InvalidPrimaryUsesVerifiedBackup), Sync(DialogueAdmissionOutboxContractTests.InvalidPrimaryUsesVerifiedBackup)),
                (nameof(DialogueAdmissionOutboxContractTests.InvalidPrimaryAndBackupFailClosed), Sync(DialogueAdmissionOutboxContractTests.InvalidPrimaryAndBackupFailClosed)),
                (nameof(DialogueAdmissionOutboxContractTests.UnchangedSaveAsGenerationRemainsIdempotent), Sync(DialogueAdmissionOutboxContractTests.UnchangedSaveAsGenerationRemainsIdempotent)),
                (nameof(DialogueAdmissionOutboxContractTests.SaveRollbackRejectsForwardOutbox), Sync(DialogueAdmissionOutboxContractTests.SaveRollbackRejectsForwardOutbox)),
                (nameof(DialogueAdmissionOutboxContractTests.CrossStoreSaveOrWorldBindingFailsClosed), Sync(DialogueAdmissionOutboxContractTests.CrossStoreSaveOrWorldBindingFailsClosed)),
                (nameof(DialogueAdmissionOutboxContractTests.OutboxCodecRoundTripIsDeterministic), Sync(DialogueAdmissionOutboxContractTests.OutboxCodecRoundTripIsDeterministic)),
                (nameof(DialogueAdmissionOutboxContractTests.InterruptedTemporaryReplacementPreservesLastGoodOutbox), Sync(DialogueAdmissionOutboxContractTests.InterruptedTemporaryReplacementPreservesLastGoodOutbox)),
                (nameof(ExternalIntegrationContractTests.OptionalCapabilityDoesNotRequireHardDependency), Sync(ExternalIntegrationContractTests.OptionalCapabilityDoesNotRequireHardDependency)),
                (nameof(ExternalIntegrationContractTests.ExternalEventAdmissionRejectsUnknownSourceAndUnsupportedKind), Sync(ExternalIntegrationContractTests.ExternalEventAdmissionRejectsUnknownSourceAndUnsupportedKind)),
                (nameof(ExternalIntegrationContractTests.ExternalEventAdmissionAcceptsPortableStoryEvent), Sync(ExternalIntegrationContractTests.ExternalEventAdmissionAcceptsPortableStoryEvent)),
                (nameof(StoryAndPersonalityContractTests.WorldStoryEventPreservesLifecycleAndPortableContext), Sync(StoryAndPersonalityContractTests.WorldStoryEventPreservesLifecycleAndPortableContext)),
                (nameof(StoryAndPersonalityContractTests.ExternalTraitInfluenceIsNotMosaicLearnedPersonality), Sync(StoryAndPersonalityContractTests.ExternalTraitInfluenceIsNotMosaicLearnedPersonality)),
                (nameof(StoryAndPersonalityContractTests.LearnedPreferenceIsDistinctFromGameTrait), Sync(StoryAndPersonalityContractTests.LearnedPreferenceIsDistinctFromGameTrait)),
                (nameof(StoryProgressContractTests.LongRunningProjectRecordsMilestonesInsteadOfEveryTick), Sync(StoryProgressContractTests.LongRunningProjectRecordsMilestonesInsteadOfEveryTick)),
                (nameof(StoryProgressContractTests.TerminalStoryOutcomeAlwaysRecords), Sync(StoryProgressContractTests.TerminalStoryOutcomeAlwaysRecords)),
                (nameof(StoryProgressContractTests.StoryProgressCanAttributeAlliedFactionContribution), Sync(StoryProgressContractTests.StoryProgressCanAttributeAlliedFactionContribution)),
                (nameof(ProviderContractTests.CurrentUserConfigurationOverridesStaleProcessValues), Sync(ProviderContractTests.CurrentUserConfigurationOverridesStaleProcessValues)),
                (nameof(ProviderContractTests.FakeProviderIsDeterministicAsync), ProviderContractTests.FakeProviderIsDeterministicAsync),
                (nameof(ProviderContractTests.FakeProviderNormalizesFailuresAsync), ProviderContractTests.FakeProviderNormalizesFailuresAsync),
                (nameof(ProviderContractTests.GoogleMissingKeyDoesNotTouchTransportAsync), ProviderContractTests.GoogleMissingKeyDoesNotTouchTransportAsync),
                (nameof(ProviderContractTests.GoogleTransportUsesHeaderAndStructuredSchemaAsync), ProviderContractTests.GoogleTransportUsesHeaderAndStructuredSchemaAsync),
                (nameof(ProviderContractTests.GoogleRateLimitIsRetryableAsync), ProviderContractTests.GoogleRateLimitIsRetryableAsync)
            };

            var failures = 0;
            foreach (var test in tests)
            {
                try
                {
                    await test.Run().ConfigureAwait(false);
                    Console.WriteLine($"PASS {test.Name}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
                }
            }

            Console.WriteLine($"Executed {tests.Count} tests; {failures} failed.");
            return failures == 0 ? 0 : 1;
        }

        private static Func<Task> Sync(Action action)
        {
            return () =>
            {
                action();
                return Task.CompletedTask;
            };
        }
    }
}
