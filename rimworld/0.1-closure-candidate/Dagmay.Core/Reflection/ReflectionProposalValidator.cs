using System;
using System.Collections.Generic;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Identity;
using Dagmay.Core.Lifecycle;

namespace Dagmay.Core.Reflection
{
    public enum ReflectionValidationStatus
    {
        Valid,
        RequestMismatch,
        IdentityMismatch,
        StaleState,
        LifecycleBlocked,
        MissingEvidence,
        EvidenceNotAllowed,
        ExcessiveChange
    }

    public sealed class ReflectionValidationResult
    {
        public ReflectionValidationResult(
            ReflectionValidationStatus status,
            string diagnostic,
            IndividualState? replacement)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Replacement = replacement;
        }

        public ReflectionValidationStatus Status { get; }
        public string Diagnostic { get; }
        public IndividualState? Replacement { get; }
        public bool IsValid => Status == ReflectionValidationStatus.Valid && Replacement is not null;
    }

    public sealed class ReflectionProposalValidator
    {
        private readonly double _maximumDimensionChange;

        public ReflectionProposalValidator(double maximumDimensionChange = 0.25)
        {
            if (double.IsNaN(maximumDimensionChange)
                || double.IsInfinity(maximumDimensionChange)
                || maximumDimensionChange <= 0
                || maximumDimensionChange > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDimensionChange));
            }

            _maximumDimensionChange = maximumDimensionChange;
        }

        public ReflectionValidationResult Validate(
            ModelRequest request,
            ReflectionProposal proposal,
            IndividualState current,
            IEventLedger eventLedger)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));
            if (current is null) throw new ArgumentNullException(nameof(current));
            if (eventLedger is null) throw new ArgumentNullException(nameof(eventLedger));

            if (proposal.RequestId != request.Id)
            {
                return Failure(ReflectionValidationStatus.RequestMismatch, "The response request ID does not match the dispatched request.");
            }

            if (proposal.IndividualId != request.IndividualId
                || current.Id != request.IndividualId
                || current.LineageId != request.LineageId)
            {
                return Failure(ReflectionValidationStatus.IdentityMismatch, "The response does not target the requested identity and lineage.");
            }

            if (proposal.BaseStateVersion != request.BaseStateVersion
                || current.Version != request.BaseStateVersion)
            {
                return Failure(ReflectionValidationStatus.StaleState, "The response was generated from a stale identity version.");
            }

            if (current.Lifecycle == LifecycleState.Dead || current.Lifecycle == LifecycleState.Archived)
            {
                return Failure(ReflectionValidationStatus.LifecycleBlocked, "Ordinary reflection cannot mutate a dead or archived individual.");
            }

            var allowed = new HashSet<Contracts.EventId>(request.EvidenceEventIds);
            if (proposal.EvidenceEventIds.Count == 0)
            {
                return Failure(ReflectionValidationStatus.MissingEvidence, "At least one evidence event is required.");
            }

            foreach (var evidenceId in proposal.EvidenceEventIds)
            {
                if (!allowed.Contains(evidenceId))
                {
                    return Failure(ReflectionValidationStatus.EvidenceNotAllowed, "The response cited evidence outside the dispatched context.");
                }

                if (!eventLedger.Contains(evidenceId))
                {
                    return Failure(ReflectionValidationStatus.MissingEvidence, "A cited event is absent from the canonical event ledger.");
                }
            }

            if (ExceedsMaximumChange(current, proposal))
            {
                return Failure(ReflectionValidationStatus.ExcessiveChange, "The proposed affect change exceeds the continuity bound.");
            }

            var replacement = current.WithAffect(proposal.TargetAffect, current.Version);
            return new ReflectionValidationResult(
                ReflectionValidationStatus.Valid,
                "The proposal passed identity, state, evidence, lifecycle, and continuity validation.",
                replacement);
        }

        private bool ExceedsMaximumChange(IndividualState current, ReflectionProposal proposal)
        {
            return Math.Abs(current.Affect.Valence - proposal.TargetAffect.Valence) > _maximumDimensionChange
                || Math.Abs(current.Affect.Arousal - proposal.TargetAffect.Arousal) > _maximumDimensionChange
                || Math.Abs(current.Affect.Threat - proposal.TargetAffect.Threat) > _maximumDimensionChange
                || Math.Abs(current.Affect.Agency - proposal.TargetAffect.Agency) > _maximumDimensionChange
                || Math.Abs(current.Affect.Attachment - proposal.TargetAffect.Attachment) > _maximumDimensionChange
                || Math.Abs(current.Affect.Certainty - proposal.TargetAffect.Certainty) > _maximumDimensionChange
                || Math.Abs(current.Affect.SocialStanding - proposal.TargetAffect.SocialStanding) > _maximumDimensionChange;
        }

        private static ReflectionValidationResult Failure(ReflectionValidationStatus status, string diagnostic)
        {
            return new ReflectionValidationResult(status, diagnostic, null);
        }
    }
}
