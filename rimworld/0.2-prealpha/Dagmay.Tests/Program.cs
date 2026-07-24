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
                (nameof(ExecutionAndPresentationContractTests.ObserverAndPresentationReadsPreserveCanonicalFingerprint), Sync(ExecutionAndPresentationContractTests.ObserverAndPresentationReadsPreserveCanonicalFingerprint)),
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
