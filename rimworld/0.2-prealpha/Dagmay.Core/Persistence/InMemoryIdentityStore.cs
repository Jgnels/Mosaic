using System;
using System.Collections.Generic;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Persistence
{
    public sealed class InMemoryIdentityStore : IIdentityStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<IndividualId, IndividualState> _states = new Dictionary<IndividualId, IndividualState>();

        public bool TryGet(IndividualId id, out IndividualState? state)
        {
            lock (_gate)
            {
                return _states.TryGetValue(id, out state);
            }
        }

        public IdentityWriteStatus TryAdd(IndividualState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));

            lock (_gate)
            {
                if (_states.ContainsKey(state.Id)) return IdentityWriteStatus.AlreadyExists;
                _states.Add(state.Id, state);
                return IdentityWriteStatus.Succeeded;
            }
        }

        public IdentityWriteStatus TryReplace(long expectedVersion, IndividualState replacement)
        {
            if (replacement is null) throw new ArgumentNullException(nameof(replacement));

            lock (_gate)
            {
                if (!_states.TryGetValue(replacement.Id, out var current)) return IdentityWriteStatus.NotFound;
                if (current.Id != replacement.Id) return IdentityWriteStatus.IdentityMismatch;
                if (current.LineageId != replacement.LineageId) return IdentityWriteStatus.LineageMismatch;
                if (current.Version != expectedVersion) return IdentityWriteStatus.StaleVersion;
                if (replacement.Version != expectedVersion + 1) return IdentityWriteStatus.InvalidVersionStep;

                _states[replacement.Id] = replacement;
                return IdentityWriteStatus.Succeeded;
            }
        }
    }
}

