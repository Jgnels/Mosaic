using System;

namespace Dagmay.RimWorld.Safety
{
    public static class ObserverOnlyGuard
    {
        public const bool AllowsPawnControl = false;

        public static void RejectPawnControl(string requestedCapability)
        {
            var capability = string.IsNullOrWhiteSpace(requestedCapability)
                ? "unspecified pawn control"
                : requestedCapability.Trim();

            throw new InvalidOperationException(
                $"Dagmay Version 0.1 is Observer-only. Rejected capability: {capability}.");
        }
    }
}

