namespace Dagmay.Core.Lifecycle
{
    public static class LifecycleRules
    {
        public static bool CanTransition(LifecycleState current, LifecycleState target, LifecycleTransitionKind kind)
        {
            if (current == target) return false;

            if (kind == LifecycleTransitionKind.OrdinaryAvailabilityChange)
            {
                return (current == LifecycleState.Active && target == LifecycleState.Unavailable)
                    || (current == LifecycleState.Unavailable && target == LifecycleState.Active);
            }

            if (kind == LifecycleTransitionKind.InWorldDeath)
            {
                return (current == LifecycleState.Active || current == LifecycleState.Unavailable)
                    && (target == LifecycleState.Dead || target == LifecycleState.Archived);
            }

            if (kind == LifecycleTransitionKind.ArchiveAfterDeath)
            {
                return current == LifecycleState.Dead && target == LifecycleState.Archived;
            }

            if (kind == LifecycleTransitionKind.InWorldRevival)
            {
                return (current == LifecycleState.Dead || current == LifecycleState.Archived)
                    && target == LifecycleState.Active;
            }

            return false;
        }
    }
}

