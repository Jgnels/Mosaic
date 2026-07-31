using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public enum ConversationState
    {
        Created = 0,
        Queued = 1,
        Dispatched = 2,
        ProposalReceived = 3,
        Validated = 4,
        Displayed = 5,
        AdmissionPrepared = 6,
        Admitted = 7,
        Cancelled = 8,
        Expired = 9,
        Failed = 10
    }

    public enum ConversationTransition
    {
        Dispatch = 0,
        ReceiveProposal = 1,
        AcceptValidation = 2,
        RecordDisplay = 3,
        PrepareAdmission = 4,
        ConfirmAdmission = 5,
        Cancel = 6,
        Timeout = 7,
        ProviderFailure = 8,
        ValidationRejection = 9,
        DisplayFailure = 10
    }

    public enum ConversationTerminationReason
    {
        None = 0,
        Cancelled = 1,
        Timeout = 2,
        ProviderFailure = 3,
        ValidationRejection = 4,
        DisplayFailure = 5
    }

    /// <summary>
    /// Core-only lifecycle for a bounded conversation. It records coordination
    /// state only and owns no identity, memory, relationship, mood, belief, goal,
    /// provider, presentation, or gameplay authority.
    /// </summary>
    public sealed class ConversationStateMachine
    {
        private readonly HashSet<DialogueRequestId> _usedRequestIds =
            new HashSet<DialogueRequestId>();

        public ConversationStateMachine(
            ConversationId conversationId,
            IEnumerable<IndividualId> participantIds,
            int hardTurnCap,
            long createdAtTick)
        {
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
            if (participantIds is null) throw new ArgumentNullException(nameof(participantIds));
            if (hardTurnCap < 1 || hardTurnCap > 32)
                throw new ArgumentOutOfRangeException(nameof(hardTurnCap));
            if (createdAtTick < 0) throw new ArgumentOutOfRangeException(nameof(createdAtTick));

            var participants = participantIds.ToArray();
            if (participants.Length == 0 || participants.Length > 8)
                throw new ArgumentOutOfRangeException(
                    nameof(participantIds),
                    "A conversation requires between 1 and 8 participants.");
            if (participants.Any(id => id.Value == Guid.Empty))
                throw new ArgumentException("Participant IDs cannot be empty.", nameof(participantIds));
            if (participants.Distinct().Count() != participants.Length)
                throw new ArgumentException("Participant IDs must be unique.", nameof(participantIds));

            ConversationId = conversationId;
            ParticipantIds = Array.AsReadOnly(participants);
            HardTurnCap = hardTurnCap;
            LastTransitionTick = createdAtTick;
            State = ConversationState.Created;
            TerminationReason = ConversationTerminationReason.None;
        }

        public ConversationId ConversationId { get; }
        public IReadOnlyList<IndividualId> ParticipantIds { get; }
        public int HardTurnCap { get; }
        public int AdmittedTurnCount { get; private set; }
        public long LastTransitionTick { get; private set; }
        public ConversationState State { get; private set; }
        public ConversationTerminationReason TerminationReason { get; private set; }
        public DialogueRequest? CurrentRequest { get; private set; }

        public bool IsTerminal =>
            State == ConversationState.Cancelled ||
            State == ConversationState.Expired ||
            State == ConversationState.Failed ||
            (State == ConversationState.Admitted && AdmittedTurnCount >= HardTurnCap);

        public void QueueTurn(DialogueRequest request, long transitionTick)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            RequireMonotonicTick(transitionTick);
            if (IsTerminal) throw new InvalidOperationException("A terminal conversation cannot reopen.");
            if (State != ConversationState.Created && State != ConversationState.Admitted)
                throw new InvalidOperationException($"Cannot queue a turn while conversation state is {State}.");
            if (AdmittedTurnCount >= HardTurnCap)
                throw new InvalidOperationException("The conversation hard turn cap has been reached.");
            if (request.ConversationId != ConversationId)
                throw new ArgumentException("Request conversation ID does not match.", nameof(request));
            if (transitionTick < request.CreatedAtTick)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionTick),
                    "A turn cannot be queued before its request was created.");
            if (request.IsExpired(transitionTick))
                throw new ArgumentException("An expired request cannot be queued.", nameof(request));
            if (_usedRequestIds.Contains(request.Id))
                throw new ArgumentException("A request ID may identify only one expected turn.", nameof(request));
            if (request.Scene.Participants.Any(
                    participant => !ParticipantIds.Contains(participant.IndividualId)))
            {
                throw new ArgumentException(
                    "Request scene contains a participant outside the stable conversation set.",
                    nameof(request));
            }
            if (!ParticipantIds.Contains(request.ExpectedSpeakerId) ||
                (request.RecipientId.HasValue && !ParticipantIds.Contains(request.RecipientId.Value)))
            {
                throw new ArgumentException(
                    "Expected speaker and recipient must belong to the stable conversation set.",
                    nameof(request));
            }

            _usedRequestIds.Add(request.Id);
            CurrentRequest = request;
            State = ConversationState.Queued;
            LastTransitionTick = transitionTick;
        }

        public void Apply(ConversationTransition transition, long transitionTick)
        {
            if (!Enum.IsDefined(typeof(ConversationTransition), transition))
                throw new ArgumentOutOfRangeException(nameof(transition));
            RequireMonotonicTick(transitionTick);
            if (IsTerminal) throw new InvalidOperationException("A terminal conversation cannot reopen.");

            ConversationState next;
            ConversationTerminationReason reason = ConversationTerminationReason.None;
            switch (transition)
            {
                case ConversationTransition.Dispatch:
                    next = RequireState(ConversationState.Queued, ConversationState.Dispatched);
                    break;
                case ConversationTransition.ReceiveProposal:
                    next = RequireState(ConversationState.Dispatched, ConversationState.ProposalReceived);
                    break;
                case ConversationTransition.AcceptValidation:
                    next = RequireState(ConversationState.ProposalReceived, ConversationState.Validated);
                    break;
                case ConversationTransition.RecordDisplay:
                    next = RequireState(ConversationState.Validated, ConversationState.Displayed);
                    break;
                case ConversationTransition.PrepareAdmission:
                    next = RequireState(ConversationState.Displayed, ConversationState.AdmissionPrepared);
                    break;
                case ConversationTransition.ConfirmAdmission:
                    next = RequireState(ConversationState.AdmissionPrepared, ConversationState.Admitted);
                    AdmittedTurnCount++;
                    break;
                case ConversationTransition.Cancel:
                    next = ConversationState.Cancelled;
                    reason = ConversationTerminationReason.Cancelled;
                    break;
                case ConversationTransition.Timeout:
                    next = ConversationState.Expired;
                    reason = ConversationTerminationReason.Timeout;
                    break;
                case ConversationTransition.ProviderFailure:
                    RequireState(ConversationState.Dispatched, ConversationState.Failed);
                    next = ConversationState.Failed;
                    reason = ConversationTerminationReason.ProviderFailure;
                    break;
                case ConversationTransition.ValidationRejection:
                    RequireState(ConversationState.ProposalReceived, ConversationState.Failed);
                    next = ConversationState.Failed;
                    reason = ConversationTerminationReason.ValidationRejection;
                    break;
                case ConversationTransition.DisplayFailure:
                    RequireState(ConversationState.Validated, ConversationState.Failed);
                    next = ConversationState.Failed;
                    reason = ConversationTerminationReason.DisplayFailure;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transition));
            }

            State = next;
            TerminationReason = reason;
            LastTransitionTick = transitionTick;
        }

        private ConversationState RequireState(
            ConversationState required,
            ConversationState next)
        {
            if (State != required)
                throw new InvalidOperationException(
                    $"Transition requires state {required}, but current state is {State}.");
            return next;
        }

        private void RequireMonotonicTick(long transitionTick)
        {
            if (transitionTick < LastTransitionTick)
                throw new ArgumentOutOfRangeException(
                    nameof(transitionTick),
                    "Conversation transition ticks must be monotonic.");
        }
    }
}
