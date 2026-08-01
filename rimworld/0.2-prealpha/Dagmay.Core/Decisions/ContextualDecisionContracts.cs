using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;

namespace Dagmay.Core.Decisions
{
    public enum ReactionPresentationChannel
    {
        None = 0,
        Icon = 1,
        Thought = 2,
        SpeechFragment = 3,
        Deferred = 4
    }

    public enum NarrativeEvidenceCategory
    {
        DurableAnchor = 0,
        LivedExperience = 1,
        Commitment = 2,
        PatternSummary = 3
    }

    public enum ContextualRetrievalStrategy
    {
        DefaultHybrid = 0,
        ActiveCommitmentPlusContext = 1,
        PatternPlusInstance = 2,
        DominantSingle = 3,
        SevereLivedOverride = 4,
        NewRelationshipLivedContext = 5,
        BestAvailable = 6
    }

    public enum OwnerPolicyArea
    {
        Reaction = 0,
        Retrieval = 1,
        Planning = 2
    }

    public enum OwnerPolicyFeedbackVerdict
    {
        Prefer = 0,
        Accept = 1,
        Reject = 2
    }

    public enum RimWorldEvidenceOutcomeState
    {
        Observed = 0,
        Attempted = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    public enum RimWorldEvidencePrivacyDomain
    {
        Public = 0,
        Local = 1,
        RelationshipPrivate = 2,
        Secret = 3
    }

    internal static class ContextualDecisionContractGuard
    {
        public static string LowerHex(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                throw new ArgumentException("Value must be lowercase SHA-256.", parameterName);
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException("Value must be lowercase SHA-256.", parameterName);
                }
            }
            return value;
        }

        public static IReadOnlyList<string> StableTextIds(
            IEnumerable<string> values,
            string parameterName,
            int minimum,
            int maximum)
        {
            var normalized = (values ?? throw new ArgumentNullException(parameterName))
                .Select(value => ContractGuard.Text(value, parameterName, 128))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length < minimum || normalized.Length > maximum)
                throw new ArgumentOutOfRangeException(parameterName);
            if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
                throw new ArgumentException("Values must be unique.", parameterName);
            return new ReadOnlyCollection<string>(normalized);
        }
    }

    /// <summary>
    /// Presentation decision for an already-grounded appraisal.
    /// It cannot change appraisal meaning, memory, relationships, or pawn action.
    /// </summary>
    public sealed class ContextualReactionDecision
    {
        public ContextualReactionDecision(
            string decisionId,
            EventId evidenceId,
            IndividualId perspectiveOwnerId,
            string policyId,
            ReactionPresentationChannel channel,
            string? visibleTextSeed,
            bool createHistoryEntry,
            bool deferUntilSafe,
            int importance,
            IEnumerable<string> stableRuleIds,
            IEnumerable<string> rationale)
        {
            if (evidenceId.Value == Guid.Empty)
                throw new ArgumentException("Reaction requires evidence.", nameof(evidenceId));
            if (perspectiveOwnerId.Value == Guid.Empty)
                throw new ArgumentException("Reaction requires a perspective owner.", nameof(perspectiveOwnerId));
            if (!Enum.IsDefined(typeof(ReactionPresentationChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (importance < 0 || importance > 5)
                throw new ArgumentOutOfRangeException(nameof(importance));
            if (deferUntilSafe && channel != ReactionPresentationChannel.Deferred)
                throw new ArgumentException("Deferred decisions must use the Deferred channel.", nameof(channel));
            if (channel == ReactionPresentationChannel.Deferred && !deferUntilSafe)
                throw new ArgumentException("Deferred channel requires deferUntilSafe.", nameof(deferUntilSafe));
            if ((channel == ReactionPresentationChannel.Thought ||
                 channel == ReactionPresentationChannel.SpeechFragment) &&
                string.IsNullOrWhiteSpace(visibleTextSeed))
            {
                throw new ArgumentException("Text-bearing channels require a visible text seed.", nameof(visibleTextSeed));
            }

            DecisionId = ContextualDecisionContractGuard.LowerHex(decisionId, nameof(decisionId));
            EvidenceId = evidenceId;
            PerspectiveOwnerId = perspectiveOwnerId;
            PolicyId = ContractGuard.Text(policyId, nameof(policyId), 128);
            Channel = channel;
            VisibleTextSeed = ContractGuard.OptionalText(visibleTextSeed, nameof(visibleTextSeed), 512);
            CreateHistoryEntry = createHistoryEntry;
            DeferUntilSafe = deferUntilSafe;
            Importance = importance;
            StableRuleIds = ContextualDecisionContractGuard.StableTextIds(
                stableRuleIds,
                nameof(stableRuleIds),
                1,
                32);
            Rationale = ContextualDecisionContractGuard.StableTextIds(
                rationale,
                nameof(rationale),
                1,
                32);
        }

        public string DecisionId { get; }
        public EventId EvidenceId { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public string PolicyId { get; }
        public ReactionPresentationChannel Channel { get; }
        public string? VisibleTextSeed { get; }
        public bool CreateHistoryEntry { get; }
        public bool DeferUntilSafe { get; }
        public int Importance { get; }
        public IReadOnlyList<string> StableRuleIds { get; }
        public IReadOnlyList<string> Rationale { get; }
        public bool ChangesAppraisalMeaning => false;
        public bool DirectActionAuthority => false;
    }

    public sealed class NarrativeEvidenceReference
    {
        public NarrativeEvidenceReference(
            EventId evidenceId,
            IndividualId ownerId,
            IndividualId counterpartId,
            NarrativeEvidenceCategory category,
            long occurredAtTick,
            double valence,
            double confidence,
            double significance,
            string description,
            bool canEnterDialogue,
            bool privacyAuthorized)
        {
            if (evidenceId.Value == Guid.Empty)
                throw new ArgumentException("Narrative evidence ID cannot be empty.", nameof(evidenceId));
            if (ownerId.Value == Guid.Empty)
                throw new ArgumentException("Owner cannot be empty.", nameof(ownerId));
            if (counterpartId.Value == Guid.Empty)
                throw new ArgumentException("Counterpart cannot be empty.", nameof(counterpartId));
            if (ownerId.Equals(counterpartId))
                throw new ArgumentException("Narrative evidence counterpart must be another individual.", nameof(counterpartId));
            if (!Enum.IsDefined(typeof(NarrativeEvidenceCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category));
            if (occurredAtTick < 0)
                throw new ArgumentOutOfRangeException(nameof(occurredAtTick));

            EvidenceId = evidenceId;
            OwnerId = ownerId;
            CounterpartId = counterpartId;
            Category = category;
            OccurredAtTick = occurredAtTick;
            Valence = DevelopmentalContractGuard.SignedUnit(valence, nameof(valence));
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Significance = ContractGuard.UnitInterval(significance, nameof(significance));
            Description = ContractGuard.Text(description, nameof(description), 1024);
            CanEnterDialogue = canEnterDialogue;
            PrivacyAuthorized = privacyAuthorized;
        }

        public EventId EvidenceId { get; }
        public IndividualId OwnerId { get; }
        public IndividualId CounterpartId { get; }
        public NarrativeEvidenceCategory Category { get; }
        public long OccurredAtTick { get; }
        public double Valence { get; }
        public double Confidence { get; }
        public double Significance { get; }
        public string Description { get; }
        public bool CanEnterDialogue { get; }
        public bool PrivacyAuthorized { get; }
    }

    /// <summary>
    /// Read-only retrieval decision. Hybrid is one explicit strategy among bounded
    /// contextual strategies; this object cannot admit, delete, or rewrite memory.
    /// </summary>
    public sealed class ContextualRetrievalDecision
    {
        public ContextualRetrievalDecision(
            string decisionId,
            ContextualRetrievalStrategy strategy,
            IEnumerable<NarrativeEvidenceReference> selected,
            IEnumerable<string> stableRuleIds,
            IEnumerable<string> explanation)
        {
            if (!Enum.IsDefined(typeof(ContextualRetrievalStrategy), strategy))
                throw new ArgumentOutOfRangeException(nameof(strategy));
            var normalized = (selected ?? throw new ArgumentNullException(nameof(selected)))
                .ToArray();
            if (normalized.Length < 1 || normalized.Length > 4)
                throw new ArgumentOutOfRangeException(nameof(selected));
            if (normalized.Any(value => value is null))
                throw new ArgumentException("Selected evidence cannot contain null.", nameof(selected));
            if (normalized.Select(value => value.EvidenceId).Distinct().Count() != normalized.Length)
                throw new ArgumentException("Selected evidence IDs must be unique.", nameof(selected));
            if (normalized.Any(value => !value.CanEnterDialogue || !value.PrivacyAuthorized))
                throw new ArgumentException("Retrieval cannot include unauthorized evidence.", nameof(selected));

            var owner = normalized[0].OwnerId;
            var counterpart = normalized[0].CounterpartId;
            if (normalized.Any(value =>
                !value.OwnerId.Equals(owner) ||
                !value.CounterpartId.Equals(counterpart)))
            {
                throw new ArgumentException("Retrieval evidence must share owner and counterpart.", nameof(selected));
            }

            DecisionId = ContextualDecisionContractGuard.LowerHex(decisionId, nameof(decisionId));
            Strategy = strategy;
            Selected = new ReadOnlyCollection<NarrativeEvidenceReference>(normalized);
            StableRuleIds = ContextualDecisionContractGuard.StableTextIds(
                stableRuleIds,
                nameof(stableRuleIds),
                1,
                32);
            Explanation = ContextualDecisionContractGuard.StableTextIds(
                explanation,
                nameof(explanation),
                1,
                32);
        }

        public string DecisionId { get; }
        public ContextualRetrievalStrategy Strategy { get; }
        public IReadOnlyList<NarrativeEvidenceReference> Selected { get; }
        public IReadOnlyList<string> StableRuleIds { get; }
        public IReadOnlyList<string> Explanation { get; }
        public bool DirectMemoryMutation => false;
        public bool DirectActionAuthority => false;
    }

    public sealed class DevelopmentalPlanStep
    {
        public DevelopmentalPlanStep(
            string stepId,
            string task,
            string reason,
            bool executableCandidate,
            IEnumerable<string>? hypotheticalEffectKeys = null)
        {
            StepId = ContractGuard.Text(stepId, nameof(stepId), 128);
            Task = ContractGuard.Text(task, nameof(task), 128);
            Reason = ContractGuard.Text(reason, nameof(reason), 1024);
            ExecutableCandidate = executableCandidate;
            HypotheticalEffectKeys = ContextualDecisionContractGuard.StableTextIds(
                hypotheticalEffectKeys ?? Array.Empty<string>(),
                nameof(hypotheticalEffectKeys),
                executableCandidate ? 1 : 0,
                16);
            if (!executableCandidate && HypotheticalEffectKeys.Count > 0)
                throw new ArgumentException("Non-executable steps cannot claim hypothetical effects.", nameof(hypotheticalEffectKeys));
        }

        public string StepId { get; }
        public string Task { get; }
        public string Reason { get; }
        public bool ExecutableCandidate { get; }
        public IReadOnlyList<string> HypotheticalEffectKeys { get; }
        public bool SuccessClaimed => false;
        public bool DirectActionAuthority => false;
    }

    /// <summary>
    /// Inspectable plan-only HTN output. RimWorld still decides whether an
    /// executable candidate can become a job and observed evidence decides outcome.
    /// </summary>
    public sealed class DevelopmentalPlanProposal
    {
        public DevelopmentalPlanProposal(
            string planId,
            string sourceContextHash,
            string goal,
            IEnumerable<DevelopmentalPlanStep> steps,
            IEnumerable<string> stableRuleIds)
        {
            var normalizedSteps = (steps ?? throw new ArgumentNullException(nameof(steps)))
                .ToArray();
            if (normalizedSteps.Length < 1 || normalizedSteps.Length > 8)
                throw new ArgumentOutOfRangeException(nameof(steps));
            if (normalizedSteps.Any(value => value is null))
                throw new ArgumentException("Plan steps cannot contain null.", nameof(steps));
            if (normalizedSteps.Select(value => value.StepId).Distinct(StringComparer.Ordinal).Count() != normalizedSteps.Length)
                throw new ArgumentException("Plan step IDs must be unique.", nameof(steps));

            PlanId = ContextualDecisionContractGuard.LowerHex(planId, nameof(planId));
            SourceContextHash = ContextualDecisionContractGuard.LowerHex(
                sourceContextHash,
                nameof(sourceContextHash));
            Goal = ContractGuard.Text(goal, nameof(goal), 128);
            Steps = new ReadOnlyCollection<DevelopmentalPlanStep>(normalizedSteps);
            StableRuleIds = ContextualDecisionContractGuard.StableTextIds(
                stableRuleIds,
                nameof(stableRuleIds),
                1,
                32);
        }

        public string PlanId { get; }
        public string SourceContextHash { get; }
        public string Goal { get; }
        public IReadOnlyList<DevelopmentalPlanStep> Steps { get; }
        public IReadOnlyList<string> StableRuleIds { get; }
        public bool CanonicalMutationClaimed => false;
        public bool SuccessClaimed => false;
        public bool DirectActionAuthority => false;
    }

    /// <summary>
    /// Owner preference about a policy decision. It intentionally has no
    /// IndividualId and cannot become character evidence.
    /// </summary>
    public sealed class OwnerPolicyFeedbackRecord
    {
        public OwnerPolicyFeedbackRecord(
            string feedbackId,
            OwnerPolicyArea policyArea,
            string choiceKey,
            OwnerPolicyFeedbackVerdict verdict,
            long tick,
            string rationale)
        {
            if (!Enum.IsDefined(typeof(OwnerPolicyArea), policyArea))
                throw new ArgumentOutOfRangeException(nameof(policyArea));
            if (!Enum.IsDefined(typeof(OwnerPolicyFeedbackVerdict), verdict))
                throw new ArgumentOutOfRangeException(nameof(verdict));
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));

            FeedbackId = ContextualDecisionContractGuard.LowerHex(feedbackId, nameof(feedbackId));
            PolicyArea = policyArea;
            ChoiceKey = ContractGuard.Text(choiceKey, nameof(choiceKey), 128);
            Verdict = verdict;
            Tick = tick;
            Rationale = ContractGuard.Text(rationale, nameof(rationale), 1024);
        }

        public string FeedbackId { get; }
        public OwnerPolicyArea PolicyArea { get; }
        public string ChoiceKey { get; }
        public OwnerPolicyFeedbackVerdict Verdict { get; }
        public long Tick { get; }
        public string Rationale { get; }
        public bool IsCharacterEvidence => false;
    }

    public sealed class OwnerPolicyCalibrationSnapshot
    {
        public OwnerPolicyCalibrationSnapshot(
            long version,
            IDictionary<string, double> scores,
            IEnumerable<string> feedbackIds)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            if (scores is null)
                throw new ArgumentNullException(nameof(scores));

            var normalizedScores = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in scores)
            {
                var key = ContractGuard.Text(pair.Key, nameof(scores), 256);
                if (double.IsNaN(pair.Value) ||
                    double.IsInfinity(pair.Value) ||
                    pair.Value < -0.2d ||
                    pair.Value > 0.2d)
                {
                    throw new ArgumentOutOfRangeException(nameof(scores));
                }
                if (normalizedScores.ContainsKey(key))
                    throw new ArgumentException("Calibration keys must be unique.", nameof(scores));
                normalizedScores.Add(key, pair.Value);
            }

            Version = version;
            Scores = new ReadOnlyDictionary<string, double>(normalizedScores);
            FeedbackIds = ContextualDecisionContractGuard.StableTextIds(
                feedbackIds,
                nameof(feedbackIds),
                0,
                64);
        }

        public long Version { get; }
        public IReadOnlyDictionary<string, double> Scores { get; }
        public IReadOnlyList<string> FeedbackIds { get; }
        public bool IsCharacterState => false;
    }

    /// <summary>
    /// Structured read-only evidence emitted by a RimWorld adapter. The explicit
    /// outcome state prevents a job start or action attempt from becoming success.
    /// </summary>
    public sealed class RimWorldEventEvidenceEnvelope
    {
        public RimWorldEventEvidenceEnvelope(
            EventId eventId,
            string eventKind,
            long tick,
            IndividualId? actorId,
            IndividualId? targetId,
            IEnumerable<IndividualId> participantIds,
            RimWorldEvidenceOutcomeState outcomeState,
            RimWorldEvidencePrivacyDomain privacyDomain,
            string sourceAdapter,
            string deduplicationKey)
        {
            if (eventId.Value == Guid.Empty)
                throw new ArgumentException("Event ID cannot be empty.", nameof(eventId));
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));
            if (!Enum.IsDefined(typeof(RimWorldEvidenceOutcomeState), outcomeState))
                throw new ArgumentOutOfRangeException(nameof(outcomeState));
            if (!Enum.IsDefined(typeof(RimWorldEvidencePrivacyDomain), privacyDomain))
                throw new ArgumentOutOfRangeException(nameof(privacyDomain));

            var participants = (participantIds ?? throw new ArgumentNullException(nameof(participantIds)))
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (participants.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(participantIds));
            if (participants.Any(value => value.Value == Guid.Empty))
                throw new ArgumentException("Participants cannot contain an empty ID.", nameof(participantIds));
            if (participants.Distinct().Count() != participants.Length)
                throw new ArgumentException("Participants must be unique.", nameof(participantIds));

            EventId = eventId;
            EventKind = ContractGuard.Text(eventKind, nameof(eventKind), 256);
            Tick = tick;
            ActorId = actorId;
            TargetId = targetId;
            ParticipantIds = new ReadOnlyCollection<IndividualId>(participants);
            OutcomeState = outcomeState;
            PrivacyDomain = privacyDomain;
            SourceAdapter = ContractGuard.Text(sourceAdapter, nameof(sourceAdapter), 256);
            DeduplicationKey = ContextualDecisionContractGuard.LowerHex(
                deduplicationKey,
                nameof(deduplicationKey));
        }

        public EventId EventId { get; }
        public string EventKind { get; }
        public long Tick { get; }
        public IndividualId? ActorId { get; }
        public IndividualId? TargetId { get; }
        public IReadOnlyList<IndividualId> ParticipantIds { get; }
        public RimWorldEvidenceOutcomeState OutcomeState { get; }
        public RimWorldEvidencePrivacyDomain PrivacyDomain { get; }
        public string SourceAdapter { get; }
        public string DeduplicationKey { get; }
        public bool IsObservedOutcome =>
            OutcomeState == RimWorldEvidenceOutcomeState.Completed ||
            OutcomeState == RimWorldEvidenceOutcomeState.Failed ||
            OutcomeState == RimWorldEvidenceOutcomeState.Cancelled;
        public bool CanClaimSuccess =>
            OutcomeState == RimWorldEvidenceOutcomeState.Completed;
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;
    }
}
