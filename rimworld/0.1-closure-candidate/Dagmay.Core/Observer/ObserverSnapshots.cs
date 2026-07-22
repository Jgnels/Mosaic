using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Scheduling;

namespace Dagmay.Core.Observer
{
    public enum ObserverEnrollmentState
    {
        NotEnrolled,
        Active,
        Paused,
        Archived
    }

    public sealed class ObserverSeedFact
    {
        public ObserverSeedFact(string category, string key, string value, double confidence)
        {
            Category = Required(category, nameof(category), 128);
            Key = Required(key, nameof(key), 128);
            Value = Required(value, nameof(value), 2048);
            if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            Confidence = confidence;
        }

        public string Category { get; }
        public string Key { get; }
        public string Value { get; }
        public double Confidence { get; }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }
    }

    public sealed class ObserverMemory
    {
        public ObserverMemory(
            string memoryId,
            IEnumerable<string> sourcePerceptionIds,
            IEnumerable<string> sourceEventIds,
            DateTimeOffset occurredAtUtc,
            DateTimeOffset encodedAtUtc,
            string diaryEntry,
            string appraisal,
            AffectVector affectAtEncoding,
            string tier,
            string privacy,
            double importance,
            double emotionalWeight,
            double confidence,
            double accessibility,
            IEnumerable<string> peopleInvolved)
        {
            MemoryId = Required(memoryId, nameof(memoryId), 64);
            SourcePerceptionIds = ReadOnlyStrings(sourcePerceptionIds, nameof(sourcePerceptionIds), 100);
            SourceEventIds = ReadOnlyStrings(sourceEventIds, nameof(sourceEventIds), 100);
            OccurredAtUtc = occurredAtUtc;
            EncodedAtUtc = encodedAtUtc;
            DiaryEntry = Required(diaryEntry, nameof(diaryEntry), 2000);
            Appraisal = Required(appraisal, nameof(appraisal), 2000);
            AffectAtEncoding = affectAtEncoding ?? throw new ArgumentNullException(nameof(affectAtEncoding));
            Tier = Required(tier, nameof(tier), 64);
            Privacy = Required(privacy, nameof(privacy), 64);
            Importance = Unit(importance, nameof(importance));
            EmotionalWeight = Unit(emotionalWeight, nameof(emotionalWeight));
            Confidence = Unit(confidence, nameof(confidence));
            Accessibility = Unit(accessibility, nameof(accessibility));
            PeopleInvolved = ReadOnlyStrings(peopleInvolved, nameof(peopleInvolved), 100);
        }

        public string MemoryId { get; }
        public IReadOnlyList<string> SourcePerceptionIds { get; }
        public IReadOnlyList<string> SourceEventIds { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public DateTimeOffset EncodedAtUtc { get; }
        public string DiaryEntry { get; }
        public string Appraisal { get; }
        public AffectVector AffectAtEncoding { get; }
        public string Tier { get; }
        public string Privacy { get; }
        public double Importance { get; }
        public double EmotionalWeight { get; }
        public double Confidence { get; }
        public double Accessibility { get; }
        public IReadOnlyList<string> PeopleInvolved { get; }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }

        private static double Unit(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static IReadOnlyList<string> ReadOnlyStrings(
            IEnumerable<string> values,
            string parameterName,
            int maximum)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var result = values.Select(value => Required(value, parameterName, 64)).ToList();
            if (result.Count > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return new ReadOnlyCollection<string>(result);
        }
    }

    public sealed class ObserverReflection
    {
        public ObserverReflection(
            string requestId,
            DateTimeOffset occurredAtUtc,
            string provider,
            string model,
            double confidence,
            string interpretation,
            string autobiographicalReflection,
            string decisionSummary,
            int evidenceCount)
        {
            if (evidenceCount < 1 || evidenceCount > 100) throw new ArgumentOutOfRangeException(nameof(evidenceCount));
            RequestId = Required(requestId, nameof(requestId), 64);
            OccurredAtUtc = occurredAtUtc;
            Provider = Optional(provider, 128);
            Model = Optional(model, 128);
            if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            Confidence = confidence;
            Interpretation = Required(interpretation, nameof(interpretation), 2000);
            AutobiographicalReflection = Required(autobiographicalReflection, nameof(autobiographicalReflection), 2000);
            DecisionSummary = Required(decisionSummary, nameof(decisionSummary), 2000);
            EvidenceCount = evidenceCount;
        }

        public string RequestId { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public string Provider { get; }
        public string Model { get; }
        public double Confidence { get; }
        public string Interpretation { get; }
        public string AutobiographicalReflection { get; }
        public string DecisionSummary { get; }
        public int EvidenceCount { get; }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }

    public sealed class ObserverIndividual
    {
        public ObserverIndividual(
            string externalId,
            string displayName,
            ObserverEnrollmentState enrollment,
            string individualId,
            string lineageId,
            long version,
            string lifecycle,
            AffectVector affect,
            IEnumerable<ObserverSeedFact> seedFacts,
            IEnumerable<ObserverMemory> memories,
            IEnumerable<ObserverReflection> reflections)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(ObserverEnrollmentState), enrollment)) throw new ArgumentOutOfRangeException(nameof(enrollment));
            ExternalId = Required(externalId, nameof(externalId), 512);
            DisplayName = Required(displayName, nameof(displayName), 256);
            Enrollment = enrollment;
            IndividualId = Optional(individualId, 64);
            LineageId = Optional(lineageId, 64);
            Version = version;
            Lifecycle = Optional(lifecycle, 64);
            Affect = affect ?? AffectVector.Neutral;
            SeedFacts = ReadOnly(seedFacts, nameof(seedFacts), 1000);
            Memories = ReadOnly(memories, nameof(memories), 1000);
            Reflections = ReadOnly(reflections, nameof(reflections), 500);
            if (enrollment != ObserverEnrollmentState.NotEnrolled
                && (IndividualId.Length == 0 || LineageId.Length == 0 || Lifecycle.Length == 0))
            {
                throw new ArgumentException("An existing individual requires identity, lineage, and lifecycle fields.");
            }
        }

        public string ExternalId { get; }
        public string DisplayName { get; }
        public ObserverEnrollmentState Enrollment { get; }
        public string IndividualId { get; }
        public string LineageId { get; }
        public long Version { get; }
        public string Lifecycle { get; }
        public AffectVector Affect { get; }
        public IReadOnlyList<ObserverSeedFact> SeedFacts { get; }
        public IReadOnlyList<ObserverMemory> Memories { get; }
        public IReadOnlyList<ObserverReflection> Reflections { get; }
        public bool HasIdentity => Enrollment != ObserverEnrollmentState.NotEnrolled;

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameterName, int maximum)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var result = values.ToList();
            if (result.Count > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return new ReadOnlyCollection<T>(result);
        }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }

    public sealed class ReflectionUsageSummary
    {
        public ReflectionUsageSummary(
            int attemptsThisSession,
            int attemptsLastHour,
            int attemptsToday,
            int successfulRequestsToday,
            int failedAttemptsToday,
            int estimatedTokensToday,
            int reportedTokensToday,
            DateTimeOffset? lastActivityUtc,
            string lastOutcome,
            string lastErrorCode)
        {
            AttemptsThisSession = Nonnegative(attemptsThisSession, nameof(attemptsThisSession));
            AttemptsLastHour = Nonnegative(attemptsLastHour, nameof(attemptsLastHour));
            AttemptsToday = Nonnegative(attemptsToday, nameof(attemptsToday));
            SuccessfulRequestsToday = Nonnegative(successfulRequestsToday, nameof(successfulRequestsToday));
            FailedAttemptsToday = Nonnegative(failedAttemptsToday, nameof(failedAttemptsToday));
            EstimatedTokensToday = Nonnegative(estimatedTokensToday, nameof(estimatedTokensToday));
            ReportedTokensToday = Nonnegative(reportedTokensToday, nameof(reportedTokensToday));
            LastActivityUtc = lastActivityUtc;
            LastOutcome = Optional(lastOutcome, 128);
            LastErrorCode = Optional(lastErrorCode, 128);
        }

        public int AttemptsThisSession { get; }
        public int AttemptsLastHour { get; }
        public int AttemptsToday { get; }
        public int SuccessfulRequestsToday { get; }
        public int FailedAttemptsToday { get; }
        public int EstimatedTokensToday { get; }
        public int ReportedTokensToday { get; }
        public DateTimeOffset? LastActivityUtc { get; }
        public string LastOutcome { get; }
        public string LastErrorCode { get; }

        public static ReflectionUsageSummary Calculate(
            DateTimeOffset nowUtc,
            IEnumerable<ReflectionAuditRecord> records,
            int attemptsThisSession)
        {
            if (records is null) throw new ArgumentNullException(nameof(records));
            var values = records.OrderBy(value => value.OccurredAtUtc).ToList();
            var hourStart = nowUtc.Subtract(TimeSpan.FromHours(1));
            var dayStart = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
            var attempts = values.Where(value => value.Status == ReflectionAuditStatus.AttemptStarted).ToList();
            var today = values.Where(value => value.OccurredAtUtc >= dayStart && value.OccurredAtUtc <= nowUtc).ToList();
            var usageByRequest = today
                .Where(value => value.TotalTokens > 0)
                .GroupBy(value => value.RequestId)
                .Select(group => group.Max(value => value.TotalTokens));
            var terminal = values
                .Where(value => value.Status == ReflectionAuditStatus.Committed
                    || value.Status == ReflectionAuditStatus.AttemptFailed
                    || value.Status == ReflectionAuditStatus.Quarantined)
                .OrderByDescending(value => value.OccurredAtUtc)
                .FirstOrDefault();

            return new ReflectionUsageSummary(
                attemptsThisSession,
                attempts.Count(value => value.OccurredAtUtc > hourStart && value.OccurredAtUtc <= nowUtc),
                attempts.Count(value => value.OccurredAtUtc >= dayStart && value.OccurredAtUtc <= nowUtc),
                today.Where(value => value.Status == ReflectionAuditStatus.Committed)
                    .Select(value => value.RequestId)
                    .Distinct()
                    .Count(),
                today.Count(value => value.Status == ReflectionAuditStatus.AttemptFailed),
                attempts.Where(value => value.OccurredAtUtc >= dayStart && value.OccurredAtUtc <= nowUtc)
                    .Sum(value => value.EstimatedTokens),
                usageByRequest.Sum(),
                terminal?.OccurredAtUtc,
                terminal?.Status.ToString() ?? string.Empty,
                terminal?.ErrorCode ?? string.Empty);
        }

        private static int Nonnegative(int value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }

    public sealed class ObserverSystemSnapshot
    {
        public ObserverSystemSnapshot(
            DateTimeOffset capturedAtUtc,
            string reflectionMode,
            string provider,
            string model,
            string providerDiagnostic,
            bool providerDispatchPaused,
            bool identityStorageHealthy,
            bool experienceStorageHealthy,
            bool reflectionStorageHealthy,
            int pendingReflectionCount,
            int auditRecordCount,
            ReflectionBudgetPolicy budget,
            ReflectionUsageSummary usage,
            IEnumerable<ObserverIndividual> individuals)
        {
            if (pendingReflectionCount < 0) throw new ArgumentOutOfRangeException(nameof(pendingReflectionCount));
            if (auditRecordCount < 0) throw new ArgumentOutOfRangeException(nameof(auditRecordCount));
            CapturedAtUtc = capturedAtUtc;
            ReflectionMode = Required(reflectionMode, nameof(reflectionMode), 64);
            Provider = Optional(provider, 128);
            Model = Optional(model, 128);
            ProviderDiagnostic = Optional(providerDiagnostic, 2000);
            ProviderDispatchPaused = providerDispatchPaused;
            IdentityStorageHealthy = identityStorageHealthy;
            ExperienceStorageHealthy = experienceStorageHealthy;
            ReflectionStorageHealthy = reflectionStorageHealthy;
            PendingReflectionCount = pendingReflectionCount;
            AuditRecordCount = auditRecordCount;
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            Usage = usage ?? throw new ArgumentNullException(nameof(usage));
            if (individuals is null) throw new ArgumentNullException(nameof(individuals));
            Individuals = new ReadOnlyCollection<ObserverIndividual>(individuals.ToList());
        }

        public DateTimeOffset CapturedAtUtc { get; }
        public string ReflectionMode { get; }
        public string Provider { get; }
        public string Model { get; }
        public string ProviderDiagnostic { get; }
        public bool ProviderDispatchPaused { get; }
        public bool IdentityStorageHealthy { get; }
        public bool ExperienceStorageHealthy { get; }
        public bool ReflectionStorageHealthy { get; }
        public int PendingReflectionCount { get; }
        public int AuditRecordCount { get; }
        public ReflectionBudgetPolicy Budget { get; }
        public ReflectionUsageSummary Usage { get; }
        public IReadOnlyList<ObserverIndividual> Individuals { get; }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }
}
