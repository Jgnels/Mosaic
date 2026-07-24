namespace Dagmay.RimWorld.Reflection
{
    internal enum PendingCommitRecoveryDisposition
    {
        StorageUnavailable,
        NothingToRecover,
        Recover
    }

    internal static class ReflectionStorageSafetyPolicy
    {
        public static bool AllowsQueueMutation(bool experienceWritesEnabled, bool reflectionWritesEnabled)
        {
            return experienceWritesEnabled && reflectionWritesEnabled;
        }

        public static bool AllowsProcessing(
            bool identityWritesEnabled,
            bool experienceWritesEnabled,
            bool reflectionWritesEnabled)
        {
            return identityWritesEnabled && experienceWritesEnabled && reflectionWritesEnabled;
        }

        public static PendingCommitRecoveryDisposition ClassifyPendingCommitRecovery(
            bool identityWritesEnabled,
            bool experienceWritesEnabled,
            bool reflectionWritesEnabled,
            int auditRecordCount)
        {
            if (!AllowsProcessing(
                    identityWritesEnabled,
                    experienceWritesEnabled,
                    reflectionWritesEnabled))
            {
                return PendingCommitRecoveryDisposition.StorageUnavailable;
            }

            return auditRecordCount <= 0
                ? PendingCommitRecoveryDisposition.NothingToRecover
                : PendingCommitRecoveryDisposition.Recover;
        }
    }
}
