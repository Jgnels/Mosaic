using System;

namespace Dagmay.RimWorld.Dialogue
{
    public enum RimWorldDialoguePresentationAvailability
    {
        Ready = 0,
        Disposed = 1,
        WrongThread = 2,
        MissingBinding = 3,
        PawnDespawned = 4,
        MapUnavailable = 5,
        PawnOnDifferentMap = 6
    }

    public static class RimWorldDialoguePresentationPolicy
    {
        public static RimWorldDialoguePresentationAvailability Evaluate(
            bool disposed,
            int mainThreadId,
            int currentThreadId,
            bool hasPawnBinding,
            bool pawnSpawned,
            bool currentMapAvailable,
            bool pawnOnCurrentMap)
        {
            if (mainThreadId < 1) throw new ArgumentOutOfRangeException(nameof(mainThreadId));
            if (currentThreadId < 1) throw new ArgumentOutOfRangeException(nameof(currentThreadId));
            if (disposed) return RimWorldDialoguePresentationAvailability.Disposed;
            if (mainThreadId != currentThreadId)
                return RimWorldDialoguePresentationAvailability.WrongThread;
            if (!hasPawnBinding)
                return RimWorldDialoguePresentationAvailability.MissingBinding;
            if (!pawnSpawned)
                return RimWorldDialoguePresentationAvailability.PawnDespawned;
            if (!currentMapAvailable)
                return RimWorldDialoguePresentationAvailability.MapUnavailable;
            if (!pawnOnCurrentMap)
                return RimWorldDialoguePresentationAvailability.PawnOnDifferentMap;
            return RimWorldDialoguePresentationAvailability.Ready;
        }
    }
}
