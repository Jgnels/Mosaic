using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dagmay.RimWorld.Perception
{
    internal sealed class ObservedPawnChange
    {
        public ObservedPawnChange(string kind, IDictionary<string, string> factualPayload)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            FactualPayload = new Dictionary<string, string>(
                factualPayload ?? throw new ArgumentNullException(nameof(factualPayload)),
                StringComparer.Ordinal);
        }

        public string Kind { get; }
        public IDictionary<string, string> FactualPayload { get; }
    }

    internal static class PawnChangeDetector
    {
        private const double CriticalNeedThreshold = 0.15;
        private const int MaterialOpinionDelta = 15;

        public static IReadOnlyList<ObservedPawnChange> Detect(
            PawnObservationSnapshot previous,
            PawnObservationSnapshot current)
        {
            if (previous is null) throw new ArgumentNullException(nameof(previous));
            if (current is null) throw new ArgumentNullException(nameof(current));
            var changes = new List<ObservedPawnChange>();

            foreach (var pair in current.Needs)
            {
                if (previous.Needs.TryGetValue(pair.Key, out var oldLevel)
                    && oldLevel > CriticalNeedThreshold
                    && pair.Value <= CriticalNeedThreshold)
                {
                    changes.Add(new ObservedPawnChange(
                        "rimworld.need.critical",
                        new Dictionary<string, string>
                        {
                            ["need"] = pair.Key,
                            ["level"] = pair.Value.ToString("0.000", CultureInfo.InvariantCulture)
                        }));
                }
            }

            foreach (var condition in current.HealthConditions)
            {
                if (!previous.HealthConditions.Contains(condition))
                {
                    changes.Add(new ObservedPawnChange(
                        "rimworld.health.condition_added",
                        new Dictionary<string, string> { ["condition"] = condition }));
                }
            }

            foreach (var condition in previous.HealthConditions)
            {
                if (!current.HealthConditions.Contains(condition))
                {
                    changes.Add(new ObservedPawnChange(
                        "rimworld.health.condition_removed",
                        new Dictionary<string, string> { ["condition"] = condition }));
                }
            }

            foreach (var pair in current.Skills)
            {
                if (previous.Skills.TryGetValue(pair.Key, out var oldLevel) && pair.Value > oldLevel)
                {
                    changes.Add(new ObservedPawnChange(
                        "rimworld.skill.level_gained",
                        new Dictionary<string, string>
                        {
                            ["skill"] = pair.Key,
                            ["previous_level"] = oldLevel.ToString(CultureInfo.InvariantCulture),
                            ["level"] = pair.Value.ToString(CultureInfo.InvariantCulture)
                        }));
                }
            }

            DetectSocial(previous, current, changes);
            return changes;
        }

        private static void DetectSocial(
            PawnObservationSnapshot previous,
            PawnObservationSnapshot current,
            ICollection<ObservedPawnChange> changes)
        {
            foreach (var pair in current.Social)
            {
                if (!previous.Social.TryGetValue(pair.Key, out var oldSocial)) continue;
                var newSocial = pair.Value;

                if (oldSocial.HasOpinion && newSocial.HasOpinion)
                {
                    var delta = newSocial.Opinion - oldSocial.Opinion;
                    if (Math.Abs(delta) >= MaterialOpinionDelta)
                    {
                        changes.Add(new ObservedPawnChange(
                            "rimworld.social.opinion_changed",
                            new Dictionary<string, string>
                            {
                                ["target_external_id"] = newSocial.TargetExternalId,
                                ["target_name"] = newSocial.TargetDisplayName,
                                ["opinion_before"] = oldSocial.Opinion.ToString(CultureInfo.InvariantCulture),
                                ["opinion_after"] = newSocial.Opinion.ToString(CultureInfo.InvariantCulture),
                                ["opinion_delta"] = delta.ToString(CultureInfo.InvariantCulture)
                            }));
                    }
                }

                if (!oldSocial.DirectRelations.SetEquals(newSocial.DirectRelations))
                {
                    changes.Add(new ObservedPawnChange(
                        "rimworld.relationship.direct_changed",
                        new Dictionary<string, string>
                        {
                            ["target_external_id"] = newSocial.TargetExternalId,
                            ["target_name"] = newSocial.TargetDisplayName,
                            ["relations_before"] = JoinRelations(oldSocial.DirectRelations),
                            ["relations_after"] = JoinRelations(newSocial.DirectRelations)
                        }));
                }
            }
        }

        private static string JoinRelations(IEnumerable<string> values)
        {
            var result = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return result.Length == 0 ? "none" : string.Join(",", result);
        }
    }
}
