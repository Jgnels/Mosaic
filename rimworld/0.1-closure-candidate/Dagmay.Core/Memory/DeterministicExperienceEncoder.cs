using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Memory
{
    public sealed class DeterministicExperienceResult
    {
        public DeterministicExperienceResult(
            IndividualState updatedState,
            PerceivedEvent perception,
            SubjectiveMemory memory)
        {
            UpdatedState = updatedState ?? throw new ArgumentNullException(nameof(updatedState));
            Perception = perception ?? throw new ArgumentNullException(nameof(perception));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        }

        public IndividualState UpdatedState { get; }
        public PerceivedEvent Perception { get; }
        public SubjectiveMemory Memory { get; }
    }

    public sealed class DeterministicExperienceEncoder
    {
        public DeterministicExperienceResult EncodeExperiencedEvent(
            EnvironmentEvent source,
            IndividualState individual,
            DateTimeOffset encodedAtUtc)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (individual is null) throw new ArgumentNullException(nameof(individual));
            if (!ContainsSubject(source, individual))
            {
                throw new InvalidOperationException("An experienced event must name the individual as a factual subject.");
            }

            var appraisal = Appraise(source);
            var affect = individual.Affect.BlendToward(appraisal.TargetAffect, appraisal.Intensity);
            var updated = individual.WithAffect(affect, individual.Version);
            var details = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source.FactualPayload)
            {
                details.Add(pair.Key, pair.Value);
            }

            details["event_kind"] = source.Kind;
            var perception = new PerceivedEvent(
                Contracts.PerceptionId.New(),
                source.Id,
                individual.Id,
                PerceptionChannel.Experienced,
                appraisal.Confidence,
                details,
                Array.Empty<string>(),
                individual.Version);
            var memory = new SubjectiveMemory(
                Contracts.MemoryId.New(),
                individual.Id,
                new[] { perception.Id },
                source.OccurredAtUtc,
                encodedAtUtc,
                appraisal.Diary,
                appraisal.Summary,
                affect,
                appraisal.Importance,
                appraisal.EmotionalWeight,
                appraisal.Confidence,
                1.0,
                appraisal.Importance >= 0.75 ? MemoryTier.SignificantLongTerm : MemoryTier.Recent,
                appraisal.Privacy,
                source.Subjects.Where(value => value != individual.Id));

            return new DeterministicExperienceResult(updated, perception, memory);
        }

        private static ExperienceAppraisal Appraise(EnvironmentEvent source)
        {
            if (string.Equals(source.Kind, "rimworld.need.critical", StringComparison.Ordinal))
            {
                var need = Value(source, "need", "a basic need");
                return new ExperienceAppraisal(
                    $"My {need.ToLowerInvariant()} became urgently difficult to ignore.",
                    "A severe bodily or emotional need demanded attention.",
                    new AffectVector(-0.55, 0.45, 0.35, -0.45, 0, 0.1, 0),
                    PrivacyClassification.Private,
                    0.35,
                    0.68,
                    0.65,
                    1.0);
            }

            if (string.Equals(source.Kind, "rimworld.health.condition_added", StringComparison.Ordinal))
            {
                var condition = Value(source, "condition", "health");
                return new ExperienceAppraisal(
                    $"I realized that {condition} was affecting my body.",
                    "A new health condition increased vulnerability and demanded attention.",
                    new AffectVector(-0.6, 0.5, 0.65, -0.5, 0, 0.15, 0),
                    PrivacyClassification.Private,
                    0.45,
                    0.78,
                    0.8,
                    1.0);
            }

            if (string.Equals(source.Kind, "rimworld.health.condition_removed", StringComparison.Ordinal))
            {
                var condition = Value(source, "condition", "a health problem");
                return new ExperienceAppraisal(
                    $"The burden of {condition} was no longer present.",
                    "The end of a health condition brought modest relief.",
                    new AffectVector(0.5, -0.2, -0.45, 0.35, 0, 0.25, 0),
                    PrivacyClassification.Shareable,
                    0.3,
                    0.55,
                    0.45,
                    1.0);
            }

            if (string.Equals(source.Kind, "rimworld.skill.level_gained", StringComparison.Ordinal))
            {
                var skill = Value(source, "skill", "a skill");
                var level = Value(source, "level", "a new level");
                return new ExperienceAppraisal(
                    $"I became more capable at {skill}, reaching {level}.",
                    "Practice produced a concrete increase in competence.",
                    new AffectVector(0.45, 0.15, -0.1, 0.6, 0, 0.35, 0.15),
                    PrivacyClassification.Shareable,
                    0.25,
                    0.48,
                    0.35,
                    1.0);
            }

            if (string.Equals(source.Kind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                var target = Value(source, "target_name", "someone I know");
                var deltaText = Value(source, "opinion_delta", "0");
                var delta = 0;
                int.TryParse(deltaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out delta);
                var improved = delta >= 0;
                return new ExperienceAppraisal(
                    improved
                        ? $"My feelings toward {target} have grown warmer."
                        : $"Something has made me feel more negatively toward {target}.",
                    improved
                        ? "A meaningful shift in social opinion strengthened affiliation."
                        : "A meaningful shift in social opinion introduced distance or tension.",
                    improved
                        ? new AffectVector(0.35, 0.15, -0.15, 0.15, 0.55, 0.15, 0.1)
                        : new AffectVector(-0.45, 0.3, 0.25, -0.1, -0.4, 0.05, -0.1),
                    PrivacyClassification.RelationshipSensitive,
                    0.25,
                    Math.Min(0.8, 0.5 + (Math.Abs(delta) / 200.0)),
                    Math.Min(0.75, 0.45 + (Math.Abs(delta) / 250.0)),
                    0.95);
            }

            if (string.Equals(source.Kind, "rimworld.relationship.direct_changed", StringComparison.Ordinal))
            {
                var target = Value(source, "target_name", "someone I know");
                var after = Value(source, "relations_after", "a different relationship");
                return new ExperienceAppraisal(
                    $"My relationship with {target} changed; it is now {after}.",
                    "A direct social relationship changed in a way that may matter to my personal history.",
                    new AffectVector(0.05, 0.35, 0.05, 0.1, 0.35, 0.25, 0.1),
                    PrivacyClassification.RelationshipSensitive,
                    0.35,
                    0.82,
                    0.7,
                    1.0);
            }

            throw new InvalidOperationException($"Event kind '{source.Kind}' has no deterministic Version 0.1C appraisal.");
        }

        private static bool ContainsSubject(EnvironmentEvent source, IndividualState individual)
        {
            foreach (var subject in source.Subjects)
            {
                if (subject == individual.Id) return true;
            }

            return false;
        }

        private static string Value(EnvironmentEvent source, string key, string fallback)
        {
            return source.FactualPayload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }

        private sealed class ExperienceAppraisal
        {
            public ExperienceAppraisal(
                string diary,
                string summary,
                AffectVector targetAffect,
                PrivacyClassification privacy,
                double intensity,
                double importance,
                double emotionalWeight,
                double confidence)
            {
                Diary = diary;
                Summary = summary;
                TargetAffect = targetAffect;
                Privacy = privacy;
                Intensity = intensity;
                Importance = importance;
                EmotionalWeight = emotionalWeight;
                Confidence = confidence;
            }

            public string Diary { get; }
            public string Summary { get; }
            public AffectVector TargetAffect { get; }
            public PrivacyClassification Privacy { get; }
            public double Intensity { get; }
            public double Importance { get; }
            public double EmotionalWeight { get; }
            public double Confidence { get; }
        }
    }
}
