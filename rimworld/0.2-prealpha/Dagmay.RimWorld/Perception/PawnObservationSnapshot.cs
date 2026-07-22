using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Verse;

namespace Dagmay.RimWorld.Perception
{
    internal sealed class PawnSocialObservation
    {
        public PawnSocialObservation(
            string targetExternalId,
            string targetDisplayName,
            int opinion,
            bool hasOpinion,
            IEnumerable<string> directRelations)
        {
            TargetExternalId = targetExternalId ?? throw new ArgumentNullException(nameof(targetExternalId));
            TargetDisplayName = targetDisplayName ?? throw new ArgumentNullException(nameof(targetDisplayName));
            Opinion = opinion;
            HasOpinion = hasOpinion;
            DirectRelations = new HashSet<string>(
                directRelations ?? throw new ArgumentNullException(nameof(directRelations)),
                StringComparer.Ordinal);
        }

        public string TargetExternalId { get; }
        public string TargetDisplayName { get; }
        public int Opinion { get; }
        public bool HasOpinion { get; }
        public HashSet<string> DirectRelations { get; }
    }

    internal sealed class PawnObservationSnapshot
    {
        public PawnObservationSnapshot(
            IDictionary<string, double> needs,
            IEnumerable<string> healthConditions,
            IDictionary<string, int> skills,
            IDictionary<string, PawnSocialObservation> social)
        {
            Needs = new Dictionary<string, double>(needs, StringComparer.Ordinal);
            HealthConditions = new HashSet<string>(healthConditions, StringComparer.Ordinal);
            Skills = new Dictionary<string, int>(skills, StringComparer.Ordinal);
            Social = new Dictionary<string, PawnSocialObservation>(social, StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, double> Needs { get; }
        public HashSet<string> HealthConditions { get; }
        public IReadOnlyDictionary<string, int> Skills { get; }
        public IReadOnlyDictionary<string, PawnSocialObservation> Social { get; }
    }

    internal static class PawnObservationCapture
    {
        private static readonly HashSet<string> ObservedNeeds = new HashSet<string>(
            new[] { "Food", "Rest", "Mood" },
            StringComparer.OrdinalIgnoreCase);

        public static PawnObservationSnapshot Capture(Pawn pawn, IEnumerable<Pawn>? socialPeers = null)
        {
            if (pawn is null) throw new ArgumentNullException(nameof(pawn));
            return new PawnObservationSnapshot(
                CaptureNeeds(pawn),
                CaptureHealth(pawn),
                CaptureSkills(pawn),
                CaptureSocial(pawn, socialPeers ?? Array.Empty<Pawn>()));
        }

        private static IDictionary<string, double> CaptureNeeds(Pawn pawn)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var need in Enumerate(ReadPath(pawn, "needs", "AllNeeds")))
            {
                var name = DefinitionName(ReadMember(need, "def"));
                if (name is null || !ObservedNeeds.Contains(name)) continue;
                if (TryDouble(ReadMember(need, "CurLevelPercentage"), out var level)
                    || TryDouble(ReadMember(need, "CurLevel"), out level))
                {
                    result[name] = Math.Max(0, Math.Min(1, level));
                }
            }

            return result;
        }

        private static IEnumerable<string> CaptureHealth(Pawn pawn)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in Enumerate(ReadPath(pawn, "health", "hediffSet", "hediffs")))
            {
                var name = DefinitionName(ReadMember(condition, "def"));
                if (name is not null && !string.IsNullOrWhiteSpace(name))
                {
                    result.Add(Limit(name, 128));
                }
            }

            return result;
        }

        private static IDictionary<string, int> CaptureSkills(Pawn pawn)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var skill in Enumerate(ReadPath(pawn, "skills", "skills")))
            {
                var name = DefinitionName(ReadMember(skill, "def"));
                if (name is null || string.IsNullOrWhiteSpace(name)) continue;
                if (TryInt(ReadMember(skill, "Level"), out var level)
                    || TryInt(ReadMember(skill, "levelInt"), out level))
                {
                    result[Limit(name, 128)] = Math.Max(0, level);
                }
            }

            return result;
        }

        private static IDictionary<string, PawnSocialObservation> CaptureSocial(
            Pawn pawn,
            IEnumerable<Pawn> socialPeers)
        {
            var result = new Dictionary<string, PawnSocialObservation>(StringComparer.Ordinal);
            var relations = ReadMember(pawn, "relations");
            var directByTarget = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var relation in Enumerate(ReadMember(relations, "DirectRelations")))
            {
                var otherPawn = ReadMember(relation, "otherPawn") as Pawn;
                if (otherPawn is null || string.IsNullOrWhiteSpace(otherPawn.ThingID)) continue;
                var label = DefinitionName(ReadMember(relation, "def"));
                if (string.IsNullOrWhiteSpace(label)) continue;
                if (!directByTarget.TryGetValue(otherPawn.ThingID, out var labels))
                {
                    labels = new HashSet<string>(StringComparer.Ordinal);
                    directByTarget.Add(otherPawn.ThingID, labels);
                }

                labels.Add(Limit(label ?? string.Empty, 128));
            }

            foreach (var peer in socialPeers.Where(value => value is not null))
            {
                if (ReferenceEquals(peer, pawn)
                    || string.IsNullOrWhiteSpace(peer.ThingID)
                    || string.Equals(peer.ThingID, pawn.ThingID, StringComparison.Ordinal))
                {
                    continue;
                }

                var hasOpinion = TryInvokeInt(relations, "OpinionOf", peer, out var opinion);
                directByTarget.TryGetValue(peer.ThingID, out var directRelations);
                var relationSet = directRelations ?? new HashSet<string>(StringComparer.Ordinal);
                if (!hasOpinion && relationSet.Count == 0) continue;

                result[peer.ThingID] = new PawnSocialObservation(
                    peer.ThingID,
                    Limit(PawnName(peer), 256),
                    opinion,
                    hasOpinion,
                    relationSet);
            }

            return result;
        }

        private static string PawnName(Pawn pawn)
        {
            try
            {
                return pawn.Name?.ToStringShort ?? pawn.LabelShort ?? pawn.ThingID;
            }
            catch (Exception)
            {
                return pawn.ThingID;
            }
        }

        private static bool TryInvokeInt(object? source, string methodName, object argument, out int result)
        {
            result = 0;
            if (source is null) return false;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var method in source.GetType().GetMethods(flags))
                {
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(argument)) continue;
                    return TryInt(method.Invoke(source, new[] { argument }), out result);
                }
            }
            catch (Exception)
            {
                // Social observation is best-effort. Unsupported modded relation trackers degrade silently.
            }

            return false;
        }

        private static string? DefinitionName(object? definition)
        {
            return Text(ReadMember(definition, "defName"))
                ?? Text(ReadMember(definition, "label"));
        }

        private static object? ReadPath(object? source, params string[] members)
        {
            var current = source;
            foreach (var member in members)
            {
                current = ReadMember(current, member);
                if (current is null) return null;
            }

            return current;
        }

        private static object? ReadMember(object? source, string name)
        {
            if (source is null) return null;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var type = source.GetType();
                var property = type.GetProperty(name, flags);
                if (property is not null && property.GetIndexParameters().Length == 0) return property.GetValue(source, null);
                return type.GetField(name, flags)?.GetValue(source);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IEnumerable<object> Enumerate(object? value)
        {
            if (!(value is IEnumerable values) || value is string) yield break;
            foreach (var item in values)
            {
                if (item is not null) yield return item;
            }
        }

        private static bool TryDouble(object? value, out double result)
        {
            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return !double.IsNaN(result) && !double.IsInfinity(result);
            }
            catch (Exception)
            {
                result = 0;
                return false;
            }
        }

        private static bool TryInt(object? value, out int result)
        {
            try
            {
                result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                result = 0;
                return false;
            }
        }

        private static string? Text(object? value)
        {
            if (value is null) return null;
            try
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Limit(string value, int maximum)
        {
            var cleaned = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
            return cleaned.Length <= maximum ? cleaned : cleaned.Substring(0, maximum);
        }
    }
}
