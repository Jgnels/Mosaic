using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Views
{
    public sealed class OrdinaryMindMemory
    {
        public OrdinaryMindMemory(
            DateTimeOffset occurredAtUtc,
            string diaryEntry,
            string tier,
            double confidence)
        {
            OccurredAtUtc = occurredAtUtc;
            DiaryEntry = Required(diaryEntry, nameof(diaryEntry), 2000);
            Tier = Required(tier, nameof(tier), 64);
            if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            Confidence = confidence;
        }

        public DateTimeOffset OccurredAtUtc { get; }
        public string DiaryEntry { get; }
        public string Tier { get; }
        public double Confidence { get; }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }
    }

    public sealed class OrdinaryMindSnapshot
    {
        public OrdinaryMindSnapshot(
            string externalId,
            string displayName,
            string serviceStatus,
            string lifecycle,
            string currentState,
            IEnumerable<string> identityFacts,
            IEnumerable<OrdinaryMindMemory> disclosedMemories)
        {
            ExternalId = Required(externalId, nameof(externalId), 512);
            DisplayName = Required(displayName, nameof(displayName), 256);
            ServiceStatus = Required(serviceStatus, nameof(serviceStatus), 64);
            Lifecycle = Required(lifecycle, nameof(lifecycle), 64);
            CurrentState = Required(currentState, nameof(currentState), 1000);
            IdentityFacts = ReadOnlyStrings(identityFacts, nameof(identityFacts), 100);
            if (disclosedMemories is null) throw new ArgumentNullException(nameof(disclosedMemories));
            var memories = disclosedMemories.ToList();
            if (memories.Count > 100) throw new ArgumentOutOfRangeException(nameof(disclosedMemories));
            DisclosedMemories = new ReadOnlyCollection<OrdinaryMindMemory>(memories);
        }

        public string ExternalId { get; }
        public string DisplayName { get; }
        public string ServiceStatus { get; }
        public string Lifecycle { get; }
        public string CurrentState { get; }
        public IReadOnlyList<string> IdentityFacts { get; }
        public IReadOnlyList<OrdinaryMindMemory> DisclosedMemories { get; }

        private static IReadOnlyList<string> ReadOnlyStrings(
            IEnumerable<string> values,
            string parameterName,
            int maximum)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var result = values.Select(value => Required(value, parameterName, 2048)).ToList();
            if (result.Count > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return new ReadOnlyCollection<string>(result);
        }

        private static string Required(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }
    }

    public static class OrdinaryDisclosurePolicy
    {
        private const double MinimumAccessibility = 0.35;

        public static bool CanShowMemory(SubjectiveMemory memory)
        {
            if (memory is null) throw new ArgumentNullException(nameof(memory));
            return memory.Privacy == PrivacyClassification.Shareable
                && memory.Accessibility >= MinimumAccessibility;
        }

        public static string DescribeCurrentState(AffectVector affect)
        {
            if (affect is null) throw new ArgumentNullException(nameof(affect));
            var phrases = new List<string>();

            if (affect.Threat >= 0.35) phrases.Add("feeling unsafe or under pressure");
            else if (affect.Threat <= -0.35) phrases.Add("feeling relatively safe");

            if (affect.Valence >= 0.35) phrases.Add("in a generally positive frame of mind");
            else if (affect.Valence <= -0.35) phrases.Add("having a difficult time emotionally");

            if (affect.Agency >= 0.35) phrases.Add("feeling capable of influencing what happens next");
            else if (affect.Agency <= -0.35) phrases.Add("feeling low control over the situation");

            if (affect.Attachment >= 0.35) phrases.Add("feeling connected to others");
            else if (affect.Attachment <= -0.35) phrases.Add("feeling socially distant");

            if (affect.Certainty >= 0.35) phrases.Add("feeling fairly certain about their situation");
            else if (affect.Certainty <= -0.35) phrases.Add("feeling uncertain or confused");

            if (phrases.Count == 0) return "No strong outwardly shareable concern is apparent right now.";
            if (phrases.Count == 1) return Capitalize(phrases[0]) + ".";
            return Capitalize(string.Join(", ", phrases.Take(3))) + ".";
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
