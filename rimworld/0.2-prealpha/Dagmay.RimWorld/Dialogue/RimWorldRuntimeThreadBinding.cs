using System;
using System.Threading;

namespace Dagmay.RimWorld.Dialogue
{
    /// <summary>
    /// One-time thread-affinity state for presentation. Construction is
    /// deliberately unbound; only a trusted GameComponent lifecycle entry may
    /// establish the runtime thread.
    /// </summary>
    internal sealed class RimWorldRuntimeThreadBinding : IDisposable
    {
        private int _runtimeThreadId;
        private int _disposed;

        public RimWorldRuntimeThreadBinding()
        {
        }

        public RimWorldRuntimeThreadBinding(int runtimeThreadId)
        {
            if (runtimeThreadId < 1)
                throw new ArgumentOutOfRangeException(nameof(runtimeThreadId));
            _runtimeThreadId = runtimeThreadId;
        }

        public bool IsBound => Volatile.Read(ref _runtimeThreadId) > 0;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public int EstablishedThreadId => Volatile.Read(ref _runtimeThreadId);

        public void BindFromTrustedGameComponentLifecycle(int runtimeThreadId)
        {
            if (runtimeThreadId < 1)
                throw new ArgumentOutOfRangeException(nameof(runtimeThreadId));
            if (IsDisposed) return;

            var established = Volatile.Read(ref _runtimeThreadId);
            if (established == 0)
            {
                established = Interlocked.CompareExchange(
                    ref _runtimeThreadId,
                    runtimeThreadId,
                    0);
                if (established == 0) established = runtimeThreadId;
            }

            if (established != runtimeThreadId)
                throw WrongThreadException();
        }

        public void EnsureCurrentThread(int currentThreadId)
        {
            if (currentThreadId < 1)
                throw new ArgumentOutOfRangeException(nameof(currentThreadId));
            if (IsDisposed) return;

            var established = Volatile.Read(ref _runtimeThreadId);
            if (established == 0)
            {
                throw new InvalidOperationException(
                    "RimWorld dialogue presentation has not been bound by a trusted GameComponent lifecycle call.");
            }
            if (currentThreadId != established)
                throw WrongThreadException();
        }

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

        private static InvalidOperationException WrongThreadException() =>
            new InvalidOperationException("RimWorld dialogue presentation is main-thread-only.");
    }
}
