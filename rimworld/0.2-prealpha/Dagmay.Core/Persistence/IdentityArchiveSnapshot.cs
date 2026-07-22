using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Persistence
{
    public sealed class PersistedIdentityRecord
    {
        public PersistedIdentityRecord(string externalEntityId, IndividualState state)
        {
            if (string.IsNullOrWhiteSpace(externalEntityId))
            {
                throw new ArgumentException("An external entity ID is required.", nameof(externalEntityId));
            }

            if (externalEntityId.Length > 512) throw new ArgumentOutOfRangeException(nameof(externalEntityId));
            ExternalEntityId = externalEntityId.Trim();
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public string ExternalEntityId { get; }
        public IndividualState State { get; }
    }

    public sealed class IdentityArchiveSnapshot
    {
        public IdentityArchiveSnapshot(
            Guid storeId,
            long generation,
            DateTimeOffset savedAtUtc,
            IEnumerable<PersistedIdentityRecord> records)
        {
            if (storeId == Guid.Empty) throw new ArgumentException("Store ID cannot be empty.", nameof(storeId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (records is null) throw new ArgumentNullException(nameof(records));

            var materialized = new List<PersistedIdentityRecord>(records);
            var externalIds = new HashSet<string>(StringComparer.Ordinal);
            var individualIds = new HashSet<Contracts.IndividualId>();
            foreach (var record in materialized)
            {
                if (record is null) throw new ArgumentException("Archive records cannot contain null.", nameof(records));
                if (!externalIds.Add(record.ExternalEntityId))
                {
                    throw new ArgumentException("Duplicate external entity ID in identity archive.", nameof(records));
                }

                if (!individualIds.Add(record.State.Id))
                {
                    throw new ArgumentException("Duplicate Dagmay individual ID in identity archive.", nameof(records));
                }
            }

            StoreId = storeId;
            Generation = generation;
            SavedAtUtc = savedAtUtc.ToUniversalTime();
            Records = new ReadOnlyCollection<PersistedIdentityRecord>(materialized);
        }

        public Guid StoreId { get; }
        public long Generation { get; }
        public DateTimeOffset SavedAtUtc { get; }
        public IReadOnlyList<PersistedIdentityRecord> Records { get; }
    }
}
