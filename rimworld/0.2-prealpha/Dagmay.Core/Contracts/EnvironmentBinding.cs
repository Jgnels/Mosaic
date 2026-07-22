using System;

namespace Dagmay.Core.Contracts
{
    public enum EnvironmentBindingState
    {
        Bound = 0,
        TemporarilyContained = 1,
        WorldPawn = 2,
        UnresolvedEnvironmentBinding = 3,
        Dead = 4,
        DestroyedConfirmed = 5
    }

    /// <summary>Environment locator/lifecycle state. It never owns durable identity.</summary>
    public sealed class EnvironmentBinding
    {
        public EnvironmentBinding(IndividualId individualId, EnvironmentBindingState state, string? environmentObjectKey, long observedAtTick)
        {
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));
            IndividualId = individualId;
            State = state;
            EnvironmentObjectKey = NormalizeOptional(environmentObjectKey);
            ObservedAtTick = observedAtTick;
        }

        public IndividualId IndividualId { get; }
        public EnvironmentBindingState State { get; }
        public string? EnvironmentObjectKey { get; }
        public long ObservedAtTick { get; }
        public bool IsDurableIdentityTerminal => State == EnvironmentBindingState.DestroyedConfirmed;
        public bool IsTemporarilyUnresolved =>
            State == EnvironmentBindingState.TemporarilyContained ||
            State == EnvironmentBindingState.WorldPawn ||
            State == EnvironmentBindingState.UnresolvedEnvironmentBinding;

        public EnvironmentBinding Transition(EnvironmentBindingState nextState, string? environmentObjectKey, long observedAtTick)
        {
            if (IsDurableIdentityTerminal && nextState != EnvironmentBindingState.DestroyedConfirmed)
                throw new InvalidOperationException("A confirmed-destroyed binding cannot silently rebind.");
            return new EnvironmentBinding(IndividualId, nextState, environmentObjectKey, observedAtTick);
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim();
            if (normalized.Length > 512) throw new ArgumentOutOfRangeException(nameof(value));
            return normalized;
        }
    }
}
