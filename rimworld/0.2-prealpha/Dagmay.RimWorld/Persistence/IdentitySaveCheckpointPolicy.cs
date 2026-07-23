namespace Dagmay.RimWorld.Persistence
{
    public static class IdentitySaveCheckpointPolicy
    {
        public static bool RequiresArchiveWrite(bool identityStateChanged)
        {
            return identityStateChanged;
        }
    }
}
