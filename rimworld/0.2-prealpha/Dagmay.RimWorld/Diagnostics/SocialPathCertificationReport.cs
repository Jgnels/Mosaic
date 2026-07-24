using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.RimWorld.Reflection;

namespace Dagmay.RimWorld.Diagnostics
{
    internal sealed class SocialPathCertificationSummary
    {
        public SocialPathCertificationSummary(
            int socialEvents,
            int linkedSocialEvents,
            int socialMemories,
            int relationshipSensitiveMemories,
            int counterpartProvenanceMemories,
            int reflectionEligibleEvents,
            int uniqueOwners,
            int uniqueCounterparts,
            bool postLoadAudit,
            bool identityStorageHealthy,
            bool experienceStorageHealthy,
            bool reflectionStorageHealthy)
        {
            SocialEvents = socialEvents;
            LinkedSocialEvents = linkedSocialEvents;
            SocialMemories = socialMemories;
            RelationshipSensitiveMemories = relationshipSensitiveMemories;
            CounterpartProvenanceMemories = counterpartProvenanceMemories;
            ReflectionEligibleEvents = reflectionEligibleEvents;
            UniqueOwners = uniqueOwners;
            UniqueCounterparts = uniqueCounterparts;
            PostLoadAudit = postLoadAudit;
            IdentityStorageHealthy = identityStorageHealthy;
            ExperienceStorageHealthy = experienceStorageHealthy;
            ReflectionStorageHealthy = reflectionStorageHealthy;
        }

        public int SocialEvents { get; }
        public int LinkedSocialEvents { get; }
        public int SocialMemories { get; }
        public int RelationshipSensitiveMemories { get; }
        public int CounterpartProvenanceMemories { get; }
        public int ReflectionEligibleEvents { get; }
        public int UniqueOwners { get; }
        public int UniqueCounterparts { get; }
        public bool PostLoadAudit { get; }
        public bool IdentityStorageHealthy { get; }
        public bool ExperienceStorageHealthy { get; }
        public bool ReflectionStorageHealthy { get; }

        public bool EventToMemoryComplete =>
            SocialEvents > 0 && SocialMemories == SocialEvents;

        public bool StableCounterpartLinkComplete =>
            SocialEvents > 0 && LinkedSocialEvents == SocialEvents;

        public bool PrivacyComplete =>
            SocialEvents > 0 && RelationshipSensitiveMemories == SocialEvents;

        public bool CounterpartProvenanceComplete =>
            SocialEvents > 0 && CounterpartProvenanceMemories == SocialEvents;

        public bool ReflectionPathObserved =>
            ReflectionEligibleEvents > 0;

        public bool PersistenceObserved =>
            PostLoadAudit && SocialEvents > 0;

        public bool StorageHealthy =>
            IdentityStorageHealthy && ExperienceStorageHealthy && ReflectionStorageHealthy;

        public bool Passed =>
            EventToMemoryComplete
            && StableCounterpartLinkComplete
            && PrivacyComplete
            && CounterpartProvenanceComplete
            && ReflectionPathObserved
            && PersistenceObserved
            && StorageHealthy;
    }

    internal static class SocialPathCertificationReport
    {
        public static SocialPathCertificationSummary Build(
            IReadOnlyList<EventLedgerEntry> ledger,
            IReadOnlyList<SubjectiveMemory> memories,
            IReadOnlyDictionary<PerceptionId, EventId> perceptionSources,
            bool postLoadAudit,
            bool identityStorageHealthy,
            bool experienceStorageHealthy,
            bool reflectionStorageHealthy)
        {
            if (ledger is null) throw new ArgumentNullException(nameof(ledger));
            if (memories is null) throw new ArgumentNullException(nameof(memories));
            if (perceptionSources is null) throw new ArgumentNullException(nameof(perceptionSources));

            var socialEntries = ledger
                .Where(entry => IsSocial(entry.Value.Kind))
                .ToList();

            var socialEventIds = new HashSet<EventId>(
                socialEntries.Select(entry => entry.Value.Id));

            var memoryByEvent = new Dictionary<EventId, List<SubjectiveMemory>>();
            foreach (var memory in memories)
            {
                foreach (var perceptionId in memory.SourcePerceptionIds)
                {
                    if (!perceptionSources.TryGetValue(perceptionId, out var eventId)
                        || !socialEventIds.Contains(eventId))
                    {
                        continue;
                    }

                    if (!memoryByEvent.TryGetValue(eventId, out var values))
                    {
                        values = new List<SubjectiveMemory>();
                        memoryByEvent.Add(eventId, values);
                    }

                    if (!values.Any(value => value.Id == memory.Id))
                    {
                        values.Add(memory);
                    }
                }
            }

            var linkedSocialEvents = socialEntries.Count(entry =>
                HasStableCounterpartLink(entry.Value));

            var socialMemories = 0;
            var relationshipSensitiveMemories = 0;
            var counterpartProvenanceMemories = 0;

            foreach (var entry in socialEntries)
            {
                if (!memoryByEvent.TryGetValue(entry.Value.Id, out var values)
                    || entry.Value.Subjects.Count == 0)
                {
                    continue;
                }

                var ownerMemories = values
                    .Where(value => value.OwnerId == entry.Value.Subjects[0])
                    .ToList();

                // The current bounded RimWorld encoder creates exactly one subjective memory
                // for each experienced event. Duplicate source links are a certification failure.
                if (ownerMemories.Count != 1)
                {
                    continue;
                }

                var memory = ownerMemories[0];
                socialMemories++;

                if (memory.Privacy == PrivacyClassification.RelationshipSensitive)
                {
                    relationshipSensitiveMemories++;
                }

                if (HasStableCounterpartLink(entry.Value)
                    && memory.PeopleInvolved.Contains(entry.Value.Subjects[1]))
                {
                    counterpartProvenanceMemories++;
                }
            }

            var reflectionEligible = socialEntries.Count(entry =>
                IsReflectionEligible(entry.Value, ledger));

            var owners = new HashSet<IndividualId>();
            var counterparts = new HashSet<IndividualId>();

            foreach (var entry in socialEntries)
            {
                if (entry.Value.Subjects.Count > 0)
                {
                    owners.Add(entry.Value.Subjects[0]);
                }

                if (HasStableCounterpartLink(entry.Value))
                {
                    counterparts.Add(entry.Value.Subjects[1]);
                }
            }

            return new SocialPathCertificationSummary(
                socialEntries.Count,
                linkedSocialEvents,
                socialMemories,
                relationshipSensitiveMemories,
                counterpartProvenanceMemories,
                reflectionEligible,
                owners.Count,
                counterparts.Count,
                postLoadAudit,
                identityStorageHealthy,
                experienceStorageHealthy,
                reflectionStorageHealthy);
        }

        public static void Write(
            string path,
            string storeId,
            string version,
            SocialPathCertificationSummary summary,
            long experiencePosition,
            int queueCount)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            if (!Guid.TryParse(storeId, out var parsedStoreId) || parsedStoreId == Guid.Empty)
            {
                throw new ArgumentException("A valid store ID is required.", nameof(storeId));
            }
            if (summary is null) throw new ArgumentNullException(nameof(summary));

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Certification directory is invalid.");
            Directory.CreateDirectory(directory);

            var status = summary.Passed
                ? "PASS"
                : summary.SocialEvents == 0
                    ? "WAITING_FOR_SOCIAL_EVENT"
                    : "PARTIAL";

            var lines = new[]
            {
                "DagmaySocialPathCertificationVersion=2",
                "StoreId=" + parsedStoreId.ToString("N"),
                "DagmayVersion=" + version,
                "CreatedUtc=" + DateTimeOffset.UtcNow.ToString("o"),
                "Status=" + status,
                "PostLoadAudit=" + summary.PostLoadAudit,
                "SocialEvents=" + summary.SocialEvents,
                "LinkedSocialEvents=" + summary.LinkedSocialEvents,
                "SocialMemories=" + summary.SocialMemories,
                "RelationshipSensitiveMemories=" + summary.RelationshipSensitiveMemories,
                "CounterpartProvenanceMemories=" + summary.CounterpartProvenanceMemories,
                "ReflectionEligibleSocialEvents=" + summary.ReflectionEligibleEvents,
                "UniqueOwners=" + summary.UniqueOwners,
                "UniqueCounterparts=" + summary.UniqueCounterparts,
                "ExperiencePosition=" + experiencePosition,
                "ReflectionQueueCount=" + queueCount,
                "IdentityStorageHealthy=" + summary.IdentityStorageHealthy,
                "ExperienceStorageHealthy=" + summary.ExperienceStorageHealthy,
                "ReflectionStorageHealthy=" + summary.ReflectionStorageHealthy,
                "Gate.EventToMemory=" + summary.EventToMemoryComplete,
                "Gate.StableCounterpartLink=" + summary.StableCounterpartLinkComplete,
                "Gate.RelationshipSensitivePrivacy=" + summary.PrivacyComplete,
                "Gate.CounterpartProvenance=" + summary.CounterpartProvenanceComplete,
                "Gate.ReflectionPathObserved=" + summary.ReflectionPathObserved,
                "Gate.PostLoadPersistenceObserved=" + summary.PersistenceObserved,
                "Gate.StorageHealthy=" + summary.StorageHealthy,
                "Gate.Overall=" + summary.Passed,
                "Interpretation=PASS requires at least one persisted social event with an exact stable counterpart link, one relationship-sensitive owner memory with matching counterpart provenance, a reflection-eligible evidence path, a post-load audit, and healthy stores."
            };

            var temporaryPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
            var backupPath = fullPath + ".bak";
            var bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static bool IsSocial(string kind)
        {
            return string.Equals(
                    kind,
                    "rimworld.social.opinion_changed",
                    StringComparison.Ordinal)
                || string.Equals(
                    kind,
                    "rimworld.relationship.direct_changed",
                    StringComparison.Ordinal);
        }

        private static bool HasStableCounterpartLink(EnvironmentEvent value)
        {
            return value.Subjects.Count == 2
                && value.Subjects[0] != value.Subjects[1]
                && value.FactualPayload.TryGetValue("target_external_id", out var target)
                && !string.IsNullOrWhiteSpace(target);
        }

        private static bool IsReflectionEligible(
            EnvironmentEvent value,
            IReadOnlyList<EventLedgerEntry> ledger)
        {
            if (value.Subjects.Count == 0) return false;

            var ownerId = value.Subjects[0];
            var plan = ReflectionEventTaskPolicy.Plan(ownerId, value);
            var count = ledger.Count(entry =>
                IsSocial(entry.Value.Kind)
                && string.Equals(entry.Value.Kind, value.Kind, StringComparison.Ordinal)
                && entry.Value.Subjects.Count > 0
                && entry.Value.Subjects[0] == ownerId
                && string.Equals(
                    ReflectionEventTaskPolicy.Plan(ownerId, entry.Value).CoalescingKey,
                    plan.CoalescingKey,
                    StringComparison.Ordinal));

            return count >= plan.MinimumEvidenceCount;
        }
    }
}
