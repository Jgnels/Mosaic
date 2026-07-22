using System;
using System.Collections.Generic;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Lifecycle;
using Dagmay.Core.Reflection;

namespace Dagmay.Core.Validation
{
    public enum MutationCommitStatus
    {
        Committed,
        IdentityNotFound,
        IdentityMismatch,
        StaleState,
        DuplicateResponse,
        MissingEvidence,
        LifecycleBlocked,
        ExcessiveChange,
        StoreConflict
    }

    public sealed class MutationCommitResult
    {
        public MutationCommitResult(MutationCommitStatus status, string summary, IndividualState? state)
        {
            Status = status;
            Summary = summary;
            State = state;
        }

        public MutationCommitStatus Status { get; }
        public string Summary { get; }
        public IndividualState? State { get; }
    }

    public sealed class AffectMutationService
    {
        private readonly object _gate = new object();
        private readonly IIdentityStore _identityStore;
        private readonly IEventLedger _eventLedger;
        private readonly HashSet<RequestId> _committedRequests = new HashSet<RequestId>();
        private readonly double _maximumDimensionChange;

        public AffectMutationService(
            IIdentityStore identityStore,
            IEventLedger eventLedger,
            double maximumDimensionChange = 0.35)
        {
            _identityStore = identityStore ?? throw new ArgumentNullException(nameof(identityStore));
            _eventLedger = eventLedger ?? throw new ArgumentNullException(nameof(eventLedger));
            _maximumDimensionChange = ContractGuard.UnitInterval(maximumDimensionChange, nameof(maximumDimensionChange));
        }

        public MutationCommitResult TryCommit(ProposedAffectMutation proposal)
        {
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));

            lock (_gate)
            {
                if (_committedRequests.Contains(proposal.RequestId))
                {
                    return Failure(MutationCommitStatus.DuplicateResponse, "This response was already committed.");
                }

                if (!_identityStore.TryGet(proposal.IndividualId, out var current) || current is null)
                {
                    return Failure(MutationCommitStatus.IdentityNotFound, "The target identity does not exist.");
                }

                if (current.Id != proposal.IndividualId)
                {
                    return Failure(MutationCommitStatus.IdentityMismatch, "The proposal targets a different identity.");
                }

                if (current.Version != proposal.BaseStateVersion)
                {
                    return Failure(MutationCommitStatus.StaleState, "The proposal was generated from a stale identity version.");
                }

                if (current.Lifecycle == LifecycleState.Dead || current.Lifecycle == LifecycleState.Archived)
                {
                    return Failure(MutationCommitStatus.LifecycleBlocked, "Ordinary reflection cannot mutate a dead or archived individual.");
                }

                if (proposal.EvidenceEventIds.Count == 0)
                {
                    return Failure(MutationCommitStatus.MissingEvidence, "At least one source event is required.");
                }

                for (var index = 0; index < proposal.EvidenceEventIds.Count; index++)
                {
                    if (!_eventLedger.Contains(proposal.EvidenceEventIds[index]))
                    {
                        return Failure(MutationCommitStatus.MissingEvidence, "A referenced source event does not exist.");
                    }
                }

                if (ExceedsMaximumChange(current, proposal))
                {
                    return Failure(MutationCommitStatus.ExcessiveChange, "The proposed affect change exceeds the configured continuity bound.");
                }

                var replacement = current.WithAffect(proposal.TargetAffect, proposal.BaseStateVersion);
                var write = _identityStore.TryReplace(proposal.BaseStateVersion, replacement);
                if (write != IdentityWriteStatus.Succeeded)
                {
                    return Failure(MutationCommitStatus.StoreConflict, $"The identity store rejected the commit: {write}.");
                }

                _committedRequests.Add(proposal.RequestId);
                return new MutationCommitResult(MutationCommitStatus.Committed, proposal.DecisionSummary, replacement);
            }
        }

        private bool ExceedsMaximumChange(IndividualState current, ProposedAffectMutation proposal)
        {
            return Math.Abs(current.Affect.Valence - proposal.TargetAffect.Valence) > _maximumDimensionChange
                || Math.Abs(current.Affect.Arousal - proposal.TargetAffect.Arousal) > _maximumDimensionChange
                || Math.Abs(current.Affect.Threat - proposal.TargetAffect.Threat) > _maximumDimensionChange
                || Math.Abs(current.Affect.Agency - proposal.TargetAffect.Agency) > _maximumDimensionChange
                || Math.Abs(current.Affect.Attachment - proposal.TargetAffect.Attachment) > _maximumDimensionChange
                || Math.Abs(current.Affect.Certainty - proposal.TargetAffect.Certainty) > _maximumDimensionChange
                || Math.Abs(current.Affect.SocialStanding - proposal.TargetAffect.SocialStanding) > _maximumDimensionChange;
        }

        private static MutationCommitResult Failure(MutationCommitStatus status, string summary)
        {
            return new MutationCommitResult(status, summary, null);
        }
    }
}

