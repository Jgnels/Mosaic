using System;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;

namespace Dagmay.RimWorld.Reflection
{
    internal sealed class ReflectionEventTaskPlan
    {
        public ReflectionEventTaskPlan(
            ModelTaskKind taskKind,
            ReflectionPriority priority,
            string coalescingKey,
            bool compactable,
            int minimumEvidenceCount)
        {
            if (minimumEvidenceCount <= 0) throw new ArgumentOutOfRangeException(nameof(minimumEvidenceCount));
            TaskKind = taskKind;
            Priority = priority;
            CoalescingKey = coalescingKey ?? throw new ArgumentNullException(nameof(coalescingKey));
            Compactable = compactable;
            MinimumEvidenceCount = minimumEvidenceCount;
        }

        public ModelTaskKind TaskKind { get; }
        public ReflectionPriority Priority { get; }
        public string CoalescingKey { get; }
        public bool Compactable { get; }
        public int MinimumEvidenceCount { get; }
    }

    internal static class ReflectionEventTaskPolicy
    {
        private const long PhysiologyWindowTicks = 5_000;
        private const long GrowthWindowTicks = 60_000;
        private const long SocialWindowTicks = 10_000;
        private const long DefaultWindowTicks = 2_500;

        public static ReflectionEventTaskPlan Plan(IndividualId individualId, EnvironmentEvent value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            if (IsCriticalLifecycle(value.Kind))
            {
                return new ReflectionEventTaskPlan(
                    ModelTaskKind.InterpretMeaningfulEvent,
                    ReflectionPriority.CriticalLifecycle,
                    "critical:" + individualId + ":" + value.Id,
                    compactable: false,
                    minimumEvidenceCount: 1);
            }

            if (IsPhysiology(value.Kind))
            {
                var isConditionAdded = string.Equals(
                    value.Kind,
                    "rimworld.health.condition_added",
                    StringComparison.Ordinal);
                var isCriticalNeed = string.Equals(
                    value.Kind,
                    "rimworld.need.critical",
                    StringComparison.Ordinal);
                return new ReflectionEventTaskPlan(
                    ModelTaskKind.InterpretMeaningfulEvent,
                    isConditionAdded ? ReflectionPriority.MeaningfulEvent : ReflectionPriority.Consolidation,
                    "experience:" + individualId + ":physiology:" + Bucket(value, PhysiologyWindowTicks, TimeSpan.FromMinutes(10)),
                    compactable: true,
                    minimumEvidenceCount: isCriticalNeed ? 3 : 2);
            }

            if (string.Equals(value.Kind, "rimworld.skill.level_gained", StringComparison.Ordinal))
            {
                return new ReflectionEventTaskPlan(
                    ModelTaskKind.InterpretMeaningfulEvent,
                    ReflectionPriority.Consolidation,
                    "experience:" + individualId + ":growth:" + Bucket(value, GrowthWindowTicks, TimeSpan.FromHours(4)),
                    compactable: true,
                    minimumEvidenceCount: 2);
            }

            if (string.Equals(value.Kind, "rimworld.relationship.direct_changed", StringComparison.Ordinal))
            {
                return new ReflectionEventTaskPlan(
                    ModelTaskKind.InterpretMeaningfulEvent,
                    ReflectionPriority.MeaningfulEvent,
                    "relationship:" + individualId + ":direct:" + Target(value) + ":" + value.Id,
                    compactable: false,
                    minimumEvidenceCount: 1);
            }

            if (string.Equals(value.Kind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                return new ReflectionEventTaskPlan(
                    ModelTaskKind.InterpretMeaningfulEvent,
                    ReflectionPriority.Consolidation,
                    "relationship:" + individualId + ":opinion:" + Target(value) + ":" + Bucket(value, SocialWindowTicks, TimeSpan.FromMinutes(20)),
                    compactable: true,
                    minimumEvidenceCount: 2);
            }

            return new ReflectionEventTaskPlan(
                ModelTaskKind.InterpretMeaningfulEvent,
                ReflectionPriority.MeaningfulEvent,
                "experience:" + individualId + ":" + value.Kind + ":" + Bucket(value, DefaultWindowTicks, TimeSpan.FromMinutes(5)),
                compactable: false,
                minimumEvidenceCount: 1);
        }

        private static bool IsCriticalLifecycle(string kind)
        {
            return string.Equals(kind, "rimworld.lifecycle.death", StringComparison.Ordinal)
                || string.Equals(kind, "rimworld.lifecycle.revival", StringComparison.Ordinal)
                || string.Equals(kind, "rimworld.lifecycle.joined_colony", StringComparison.Ordinal)
                || string.Equals(kind, "rimworld.lifecycle.left_colony", StringComparison.Ordinal);
        }

        private static bool IsPhysiology(string kind)
        {
            return string.Equals(kind, "rimworld.need.critical", StringComparison.Ordinal)
                || string.Equals(kind, "rimworld.health.condition_added", StringComparison.Ordinal)
                || string.Equals(kind, "rimworld.health.condition_removed", StringComparison.Ordinal);
        }

        private static string Target(EnvironmentEvent value)
        {
            return value.FactualPayload.TryGetValue("target_external_id", out var target)
                && !string.IsNullOrWhiteSpace(target)
                ? target
                : "unknown";
        }

        private static string Bucket(EnvironmentEvent value, long tickWindow, TimeSpan fallbackWindow)
        {
            if (value.GameTick.HasValue)
            {
                return (value.GameTick.Value / tickWindow).ToString();
            }

            var seconds = Math.Max(1L, (long)fallbackWindow.TotalSeconds);
            return (value.ObservedAtUtc.ToUnixTimeSeconds() / seconds).ToString();
        }
    }
}
