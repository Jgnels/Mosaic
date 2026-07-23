using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Scheduling;

namespace Dagmay.Tests
{
    internal static class CanonicalStateFingerprint
    {
        public static string Compute(
            IdentityArchiveSnapshot identities,
            EnvironmentBinding binding,
            IReadOnlyList<EventLedgerEntry> events,
            IReadOnlyList<SubjectiveMemory> memories,
            IReadOnlyList<PendingReflectionTask> pendingReflections)
        {
            if (identities is null) throw new ArgumentNullException(nameof(identities));
            if (binding is null) throw new ArgumentNullException(nameof(binding));
            if (events is null) throw new ArgumentNullException(nameof(events));
            if (memories is null) throw new ArgumentNullException(nameof(memories));
            if (pendingReflections is null) throw new ArgumentNullException(nameof(pendingReflections));

            var builder = new StringBuilder();
            Append(builder, Convert.ToBase64String(new IdentityArchiveCodec().Encode(identities)));
            Append(builder, binding.IndividualId.ToString());
            Append(builder, binding.State.ToString());
            Append(builder, binding.EnvironmentObjectKey ?? string.Empty);
            Append(builder, binding.ObservedAtTick.ToString(CultureInfo.InvariantCulture));

            foreach (var entry in events.OrderBy(value => value.Position))
            {
                Append(builder, entry.Position.ToString(CultureInfo.InvariantCulture));
                Append(builder, entry.Value.Id.ToString());
                Append(builder, entry.Value.DeduplicationKey);
                Append(builder, entry.Value.Kind);
                Append(builder, entry.Value.ObservedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                foreach (var pair in entry.Value.FactualPayload.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    Append(builder, pair.Key);
                    Append(builder, pair.Value);
                }
            }

            foreach (var memory in memories.OrderBy(value => value.Id.ToString(), StringComparer.Ordinal))
            {
                Append(builder, memory.Id.ToString());
                Append(builder, memory.OwnerId.ToString());
                Append(builder, memory.EncodedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                Append(builder, memory.ConciseDiaryEntry);
                Append(builder, memory.Appraisal);
                Append(builder, memory.Importance.ToString("R", CultureInfo.InvariantCulture));
                Append(builder, memory.Accessibility.ToString("R", CultureInfo.InvariantCulture));
                Append(builder, memory.Tier.ToString());
                Append(builder, memory.Privacy.ToString());
            }

            foreach (var pending in pendingReflections.OrderBy(value => value.Task.Id.ToString(), StringComparer.Ordinal))
            {
                Append(builder, pending.Task.Id.ToString());
                Append(builder, pending.Task.IndividualId.ToString());
                Append(builder, pending.Task.TaskKind.ToString());
                Append(builder, pending.Task.Priority.ToString());
                Append(builder, pending.Task.CoalescingKey);
                Append(builder, pending.AttemptCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, pending.NextAttemptAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            }

            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }
    }
}
