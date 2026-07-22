namespace Dagmay.Core.Lifecycle
{
    public enum LifecycleState
    {
        Active,
        Unavailable,
        Dead,
        Archived,
        Forked
    }

    public enum LifecycleTransitionKind
    {
        OrdinaryAvailabilityChange,
        InWorldDeath,
        ArchiveAfterDeath,
        InWorldRevival,
        DeliberateFork
    }
}

