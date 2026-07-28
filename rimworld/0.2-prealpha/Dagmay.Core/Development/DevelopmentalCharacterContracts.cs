using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Development
{

    internal static class DevelopmentalContractGuard
    {
        public static double SignedUnit(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1d || value > 1d)
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between -1 and 1.");
            return value;
        }
    }

    public enum BaselineTraitDimension
    {
        Empathy = 0,
        GuiltSensitivity = 1,
        Attachment = 2,
        Loyalty = 3,
        Honesty = 4,
        Cooperation = 5,
        Dominance = 6,
        Aggression = 7,
        Deliberation = 8,
        RiskTolerance = 9,
        Expressiveness = 10,
        Optimism = 11,
        Persistence = 12,
        Curiosity = 13,
        StatusSensitivity = 14
    }

    public enum LearnedDispositionDimension
    {
        Mercy = 0,
        PromiseKeeping = 1,
        Truthfulness = 2,
        Courage = 3,
        Generosity = 4,
        Cooperation = 5,
        Restraint = 6,
        Duty = 7,
        GrievanceRelease = 8,
        LoyaltyBehavior = 9,
        CalculatedSelfInterest = 10
    }

    public enum KnowledgeChannel
    {
        Direct = 0,
        Report = 1,
        Rumor = 2,
        Secret = 3,
        Correction = 4,
        Retraction = 5
    }

    public enum KnowledgePrivacyDomain
    {
        Public = 0,
        RelationshipPrivate = 1,
        Secret = 2
    }

    public enum KnowledgeClaimStatus
    {
        Current = 0,
        Superseded = 1,
        Retracted = 2
    }

    public enum GroundedWantStatus
    {
        Active = 0,
        Completed = 1,
        Expired = 2,
        Invalidated = 3
    }

    public enum DevelopmentMilestoneStatus
    {
        NotEarned = 0,
        Active = 1,
        Dormant = 2
    }

    public sealed class BaselineTraitValue
    {
        public BaselineTraitValue(BaselineTraitDimension dimension, double value)
        {
            if (!Enum.IsDefined(typeof(BaselineTraitDimension), dimension))
                throw new ArgumentOutOfRangeException(nameof(dimension));
            Dimension = dimension;
            Value = DevelopmentalContractGuard.SignedUnit(value, nameof(value));
        }

        public BaselineTraitDimension Dimension { get; }
        public double Value { get; }
    }

    public sealed class LearnedDispositionValue
    {
        public LearnedDispositionValue(LearnedDispositionDimension dimension, double value)
        {
            if (!Enum.IsDefined(typeof(LearnedDispositionDimension), dimension))
                throw new ArgumentOutOfRangeException(nameof(dimension));
            Dimension = dimension;
            Value = DevelopmentalContractGuard.SignedUnit(value, nameof(value));
        }

        public LearnedDispositionDimension Dimension { get; }
        public double Value { get; }
    }

    /// <summary>
    /// Constant-size character reference to evidence admitted by the canonical ledger.
    /// Strict deduplication remains owned by the ledger admission index.
    /// </summary>
    public sealed class EvidenceRevisionReference
    {
        public EvidenceRevisionReference(
            string consumerId,
            long ledgerRevision,
            long evidenceCount,
            string rollingDigest,
            EventId? lastEvidenceId)
        {
            if (ledgerRevision < 0) throw new ArgumentOutOfRangeException(nameof(ledgerRevision));
            if (evidenceCount < 0) throw new ArgumentOutOfRangeException(nameof(evidenceCount));
            ConsumerId = ContractGuard.Text(consumerId, nameof(consumerId), 256);
            LedgerRevision = ledgerRevision;
            EvidenceCount = evidenceCount;
            RollingDigest = LowerHex(rollingDigest, nameof(rollingDigest));
            LastEvidenceId = lastEvidenceId;
        }

        public string ConsumerId { get; }
        public long LedgerRevision { get; }
        public long EvidenceCount { get; }
        public string RollingDigest { get; }
        public EventId? LastEvidenceId { get; }

        internal static string LowerHex(string value, string parameterName)
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
    }

    /// <summary>
    /// One domain of exposure. Exposure, adaptation, sensitization, unresolved
    /// load, and integration remain separate so experience is not one irreversible meter.
    /// </summary>
    public sealed class ExposureDomainState
    {
        public ExposureDomainState(
            string domain,
            double cumulativeExposure,
            double adaptation,
            double sensitization,
            double unresolvedLoad,
            double integration)
        {
            Domain = ContractGuard.Text(domain, nameof(domain), 128);
            CumulativeExposure = ContractGuard.UnitInterval(cumulativeExposure, nameof(cumulativeExposure));
            Adaptation = ContractGuard.UnitInterval(adaptation, nameof(adaptation));
            Sensitization = ContractGuard.UnitInterval(sensitization, nameof(sensitization));
            UnresolvedLoad = ContractGuard.UnitInterval(unresolvedLoad, nameof(unresolvedLoad));
            Integration = ContractGuard.UnitInterval(integration, nameof(integration));
        }

        public string Domain { get; }
        public double CumulativeExposure { get; }
        public double Adaptation { get; }
        public double Sensitization { get; }
        public double UnresolvedLoad { get; }
        public double Integration { get; }
    }

    public sealed class GroundedWantCandidate
    {
        public GroundedWantCandidate(
            string wantId,
            IndividualId ownerId,
            string category,
            string? targetKey,
            long assignedTick,
            long expiresTick,
            double priorityScore,
            IEnumerable<EventId> sourceEvidenceIds,
            GroundedWantStatus status)
        {
            if (ownerId.Value == Guid.Empty)
                throw new ArgumentException("Want owner cannot be empty.", nameof(ownerId));
            if (assignedTick < 0) throw new ArgumentOutOfRangeException(nameof(assignedTick));
            if (expiresTick <= assignedTick) throw new ArgumentOutOfRangeException(nameof(expiresTick));
            if (!Enum.IsDefined(typeof(GroundedWantStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));

            WantId = ContractGuard.Text(wantId, nameof(wantId), 128);
            OwnerId = ownerId;
            Category = ContractGuard.Text(category, nameof(category), 128);
            TargetKey = string.IsNullOrWhiteSpace(targetKey)
                ? null
                : ContractGuard.Text(targetKey!, nameof(targetKey), 256);
            AssignedTick = assignedTick;
            ExpiresTick = expiresTick;
            PriorityScore = DevelopmentalContractGuard.SignedUnit(priorityScore, nameof(priorityScore));

            var evidence = ContractGuard.List(sourceEvidenceIds, nameof(sourceEvidenceIds));
            if (evidence.Count == 0)
                throw new ArgumentException("Grounded wants require evidence.", nameof(sourceEvidenceIds));
            if (evidence.Distinct().Count() != evidence.Count)
                throw new ArgumentException("Grounded want evidence IDs must be unique.", nameof(sourceEvidenceIds));
            SourceEvidenceIds = evidence;
            Status = status;
        }

        public string WantId { get; }
        public IndividualId OwnerId { get; }
        public string Category { get; }
        public string? TargetKey { get; }
        public long AssignedTick { get; }
        public long ExpiresTick { get; }
        public double PriorityScore { get; }
        public IReadOnlyList<EventId> SourceEvidenceIds { get; }
        public GroundedWantStatus Status { get; }
        public bool DirectActionAuthority => false;
    }

    /// <summary>
    /// Perspective-owned report. The source event remains separate from the
    /// observer's belief about that event.
    /// </summary>
    public sealed class PerspectiveKnowledgeClaim
    {
        public PerspectiveKnowledgeClaim(
            string claimId,
            IndividualId observerId,
            string subjectKey,
            string predicate,
            string value,
            EventId sourceEventId,
            IndividualId originalWitnessId,
            IEnumerable<IndividualId> speakerChain,
            double confidence,
            KnowledgeChannel channel,
            KnowledgePrivacyDomain privacyDomain,
            KnowledgeClaimStatus status,
            long learnedAtTick,
            string? supersedesClaimId = null,
            IEnumerable<string>? distortionTags = null)
        {
            if (observerId.Value == Guid.Empty)
                throw new ArgumentException("Observer cannot be empty.", nameof(observerId));
            if (sourceEventId.Value == Guid.Empty)
                throw new ArgumentException("Source event cannot be empty.", nameof(sourceEventId));
            if (originalWitnessId.Value == Guid.Empty)
                throw new ArgumentException("Original witness cannot be empty.", nameof(originalWitnessId));
            if (learnedAtTick < 0)
                throw new ArgumentOutOfRangeException(nameof(learnedAtTick));
            if (!Enum.IsDefined(typeof(KnowledgeChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (!Enum.IsDefined(typeof(KnowledgePrivacyDomain), privacyDomain))
                throw new ArgumentOutOfRangeException(nameof(privacyDomain));
            if (!Enum.IsDefined(typeof(KnowledgeClaimStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));

            ClaimId = EvidenceRevisionReference.LowerHex(claimId, nameof(claimId));
            ObserverId = observerId;
            SubjectKey = ContractGuard.Text(subjectKey, nameof(subjectKey), 256);
            Predicate = ContractGuard.Text(predicate, nameof(predicate), 128);
            Value = ContractGuard.Text(value, nameof(value), 2048);
            SourceEventId = sourceEventId;
            OriginalWitnessId = originalWitnessId;
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Channel = channel;
            PrivacyDomain = privacyDomain;
            Status = status;
            LearnedAtTick = learnedAtTick;
            SupersedesClaimId = string.IsNullOrWhiteSpace(supersedesClaimId)
                ? null
                : EvidenceRevisionReference.LowerHex(
                    supersedesClaimId!,
                    nameof(supersedesClaimId));

            var chain = ContractGuard.List(speakerChain, nameof(speakerChain));
            if (chain.Count == 0)
                throw new ArgumentException("Speaker chain cannot be empty.", nameof(speakerChain));
            if (chain.Distinct().Count() != chain.Count)
                throw new ArgumentException("Speaker chain cannot contain a loop.", nameof(speakerChain));
            if (!chain[chain.Count - 1].Equals(observerId))
                throw new ArgumentException("Speaker chain must end at the observer.", nameof(speakerChain));
            SpeakerChain = chain;

            var tags = (distortionTags ?? Array.Empty<string>())
                .Select(value => ContractGuard.Text(value, nameof(distortionTags), 128))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
                throw new ArgumentException("Distortion tags must be unique.", nameof(distortionTags));
            DistortionTags = new ReadOnlyCollection<string>(tags);
        }

        public string ClaimId { get; }
        public IndividualId ObserverId { get; }
        public string SubjectKey { get; }
        public string Predicate { get; }
        public string Value { get; }
        public EventId SourceEventId { get; }
        public IndividualId OriginalWitnessId { get; }
        public IReadOnlyList<IndividualId> SpeakerChain { get; }
        public double Confidence { get; }
        public KnowledgeChannel Channel { get; }
        public KnowledgePrivacyDomain PrivacyDomain { get; }
        public KnowledgeClaimStatus Status { get; }
        public long LearnedAtTick { get; }
        public string? SupersedesClaimId { get; }
        public IReadOnlyList<string> DistortionTags { get; }
        public bool IsCanonicalWorldFact => false;
    }

    public sealed class DevelopmentMilestoneProjection
    {
        public DevelopmentMilestoneProjection(
            string projectionId,
            IndividualId individualId,
            string milestoneId,
            string label,
            DevelopmentMilestoneStatus status,
            double confidence,
            IEnumerable<EventId> supportingEvidenceIds,
            IEnumerable<EventId> counterEvidenceIds,
            LearnedDispositionDimension sourceDimension,
            double sourceDispositionValue)
        {
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Individual cannot be empty.", nameof(individualId));
            if (!Enum.IsDefined(typeof(DevelopmentMilestoneStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(LearnedDispositionDimension), sourceDimension))
                throw new ArgumentOutOfRangeException(nameof(sourceDimension));

            ProjectionId = EvidenceRevisionReference.LowerHex(projectionId, nameof(projectionId));
            IndividualId = individualId;
            MilestoneId = ContractGuard.Text(milestoneId, nameof(milestoneId), 128);
            Label = ContractGuard.Text(label, nameof(label), 128);
            Status = status;
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            SourceDimension = sourceDimension;
            SourceDispositionValue = DevelopmentalContractGuard.SignedUnit(
                sourceDispositionValue,
                nameof(sourceDispositionValue));

            var supporting = ContractGuard.List(
                supportingEvidenceIds,
                nameof(supportingEvidenceIds));
            var counter = ContractGuard.List(
                counterEvidenceIds,
                nameof(counterEvidenceIds));
            if (supporting.Distinct().Count() != supporting.Count)
                throw new ArgumentException("Supporting evidence must be unique.", nameof(supportingEvidenceIds));
            if (counter.Distinct().Count() != counter.Count)
                throw new ArgumentException("Counterevidence must be unique.", nameof(counterEvidenceIds));
            if (supporting.Intersect(counter).Any())
                throw new ArgumentException("One event cannot be both supporting and counterevidence.");

            SupportingEvidenceIds = supporting;
            CounterEvidenceIds = counter;
        }

        public string ProjectionId { get; }
        public IndividualId IndividualId { get; }
        public string MilestoneId { get; }
        public string Label { get; }
        public DevelopmentMilestoneStatus Status { get; }
        public double Confidence { get; }
        public IReadOnlyList<EventId> SupportingEvidenceIds { get; }
        public IReadOnlyList<EventId> CounterEvidenceIds { get; }
        public LearnedDispositionDimension SourceDimension { get; }
        public double SourceDispositionValue { get; }
        public bool DirectTraitMutation => false;
        public bool DirectActionAuthority => false;
    }

    public sealed class BetrayalPressureResult
    {
        public BetrayalPressureResult(
            double stayLoyal,
            double confessOrNegotiate,
            double withdraw,
            double betray,
            IEnumerable<EventId> evidenceIds,
            string explanation)
        {
            StayLoyal = ContractGuard.UnitInterval(stayLoyal, nameof(stayLoyal));
            ConfessOrNegotiate = ContractGuard.UnitInterval(
                confessOrNegotiate,
                nameof(confessOrNegotiate));
            Withdraw = ContractGuard.UnitInterval(withdraw, nameof(withdraw));
            Betray = ContractGuard.UnitInterval(betray, nameof(betray));
            var sum = StayLoyal + ConfessOrNegotiate + Withdraw + Betray;
            if (Math.Abs(sum - 1d) > 0.000001d)
                throw new ArgumentException("Betrayal pressure probabilities must total one.");

            var evidence = ContractGuard.List(evidenceIds, nameof(evidenceIds));
            if (evidence.Count == 0)
                throw new ArgumentException("Betrayal pressure requires evidence.", nameof(evidenceIds));
            EvidenceIds = evidence;
            Explanation = ContractGuard.Text(explanation, nameof(explanation), 1024);
        }

        public double StayLoyal { get; }
        public double ConfessOrNegotiate { get; }
        public double Withdraw { get; }
        public double Betray { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public string Explanation { get; }
        public bool DirectActionAuthority => false;
    }
}
