using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Dialogue
{
    public enum CheckpointAdmissionDisposition
    {
        Ready = 0,
        StaleCheckpoint = 1,
        ReadOnlyStorage = 2,
        InvalidJournal = 3,
        AlreadyAdmitted = 4
    }

    /// <summary>
    /// Pure fail-closed gate for the owner-selected checkpoint-aligned policy.
    /// It deliberately performs no writes: the current event ledger and durable
    /// journal do not provide a shared atomic commit or recoverable outbox.
    /// </summary>
    public sealed class CheckpointAlignedDialogueAdmissionPolicy
    {
        public CheckpointAdmissionDisposition Evaluate(
            DialogueEventAdmissionPlan plan,
            long expectedCheckpointGeneration,
            long currentCheckpointGeneration,
            bool storageWritable,
            ExperienceJournalLoadStatus journalStatus,
            ISet<EventId> admittedEventIds)
        {
            if (plan is null) throw new ArgumentNullException(nameof(plan));
            if (expectedCheckpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedCheckpointGeneration));
            if (currentCheckpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(currentCheckpointGeneration));
            if (!Enum.IsDefined(typeof(ExperienceJournalLoadStatus), journalStatus))
                throw new ArgumentOutOfRangeException(nameof(journalStatus));
            if (admittedEventIds is null) throw new ArgumentNullException(nameof(admittedEventIds));

            if (currentCheckpointGeneration != expectedCheckpointGeneration)
                return CheckpointAdmissionDisposition.StaleCheckpoint;
            if (!storageWritable)
                return CheckpointAdmissionDisposition.ReadOnlyStorage;
            if (journalStatus == ExperienceJournalLoadStatus.Invalid)
                return CheckpointAdmissionDisposition.InvalidJournal;
            if (admittedEventIds.Contains(plan.FactualEvent.Id))
                return CheckpointAdmissionDisposition.AlreadyAdmitted;
            return CheckpointAdmissionDisposition.Ready;
        }
    }
}
