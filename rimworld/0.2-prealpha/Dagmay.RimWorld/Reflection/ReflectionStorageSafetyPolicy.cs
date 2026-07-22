namespace Dagmay.RimWorld.Reflection
{
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
    }
}