namespace Dagmay.RimWorld.Reflection
{
    public sealed class PostLoadReflectionCheckpointGate
    {
        public bool AwaitingRimWorldSaveCheckpoint { get; private set; }

        public void BeginLoadedSession()
        {
            AwaitingRimWorldSaveCheckpoint = true;
        }

        public bool AllowsSidecarPersistence(bool rimWorldSaveInProgress)
        {
            return !AwaitingRimWorldSaveCheckpoint || rimWorldSaveInProgress;
        }

        public void CompleteRimWorldSaveCheckpoint()
        {
            AwaitingRimWorldSaveCheckpoint = false;
        }
    }
}
