using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Dagmay.Core.Identity;
using Dagmay.RimWorld.Bootstrap;
using Verse;

namespace Dagmay.RimWorld.Identity
{
    internal static class PawnSeedExtractor
    {
        private const string Source = "RimWorld pawn snapshot at enrollment";

        public static IdentitySeed Capture(Pawn pawn)
        {
            if (pawn is null) throw new ArgumentNullException(nameof(pawn));
            var facts = new List<SeedFact>();

            Add(facts, SeedFactCategory.Circumstance, "environment_entity_id", pawn.ThingID);
            Add(facts, SeedFactCategory.Circumstance, "pawn_kind", ReadPath(pawn, "kindDef", "defName"));
            Add(facts, SeedFactCategory.Circumstance, "gender", ReadMember(pawn, "gender"));
            Add(facts, SeedFactCategory.Age, "biological_years", ReadPath(pawn, "ageTracker", "AgeBiologicalYears"));
            Add(facts, SeedFactCategory.Age, "chronological_years", ReadPath(pawn, "ageTracker", "AgeChronologicalYears"));

            var story = ReadMemberObject(pawn, "story");
            Add(facts, SeedFactCategory.Backstory, "childhood", DescribeDef(ReadMemberObject(story, "Childhood")));
            Add(facts, SeedFactCategory.Backstory, "adulthood", DescribeDef(ReadMemberObject(story, "Adulthood")));
            AddCollection(facts, SeedFactCategory.Trait, "trait", ReadPathObject(story, "traits", "allTraits"), DescribeTrait);

            AddCollection(facts, SeedFactCategory.Skill, "skill", ReadPathObject(pawn, "skills", "skills"), DescribeSkill);
            Add(facts, SeedFactCategory.Ideology, "ideology", DescribeDef(ReadMemberObject(pawn, "Ideo")));
            AddCollection(facts, SeedFactCategory.Gene, "gene", ReadPathObject(pawn, "genes", "GenesListForReading"), DescribeDef);
            AddCollection(facts, SeedFactCategory.Health, "health_condition", ReadPathObject(pawn, "health", "hediffSet", "hediffs"), DescribeDef);
            AddCollection(facts, SeedFactCategory.Relationship, "relationship", ReadPathObject(pawn, "relations", "DirectRelations"), DescribeRelationship);

            return new IdentitySeed("rimworld", DagmayBuildInfo.Version, facts);
        }

        private static void AddCollection(
            ICollection<SeedFact> facts,
            SeedFactCategory category,
            string keyPrefix,
            object? value,
            Func<object?, string?> describe)
        {
            if (!(value is IEnumerable enumerable) || value is string) return;
            var index = 0;
            foreach (var item in enumerable)
            {
                if (index >= 256) break;
                Add(facts, category, keyPrefix + ":" + index.ToString(CultureInfo.InvariantCulture), describe(item));
                index++;
            }
        }

        private static string? DescribeTrait(object? value)
        {
            if (value is null) return null;
            var definition = DescribeDef(ReadMemberObject(value, "def"));
            var degree = ReadMember(value, "Degree") ?? ReadMember(value, "degree");
            return Join(definition, degree is null ? null : "degree=" + degree);
        }

        private static string? DescribeSkill(object? value)
        {
            if (value is null) return null;
            var definition = DescribeDef(ReadMemberObject(value, "def"));
            var level = ReadMember(value, "Level") ?? ReadMember(value, "levelInt");
            var passion = ReadMember(value, "passion");
            return Join(
                definition,
                level is null ? null : "level=" + level,
                passion is null ? null : "passion=" + passion);
        }

        private static string? DescribeRelationship(object? value)
        {
            if (value is null) return null;
            var definition = DescribeDef(ReadMemberObject(value, "def"));
            var otherPawn = ReadMemberObject(value, "otherPawn");
            var otherId = ReadMember(otherPawn, "ThingID");
            var otherName = ReadMember(otherPawn, "LabelShort");
            return Join(definition, otherName, otherId);
        }

        private static string? DescribeDef(object? value)
        {
            if (value is null) return null;
            return Join(
                ReadMember(value, "defName"),
                ReadMember(value, "label"),
                ReadMember(value, "title"),
                ReadMember(value, "TitleCap"));
        }

        private static void Add(
            ICollection<SeedFact> facts,
            SeedFactCategory category,
            string key,
            string? value)
        {
            if (value is null || string.IsNullOrWhiteSpace(value)) return;
            facts.Add(new SeedFact(category, Limit(key, 128), Limit(value, 2048), Source, 1.0));
        }

        private static object? ReadPathObject(object? source, params string[] path)
        {
            var current = source;
            foreach (var name in path)
            {
                current = ReadMemberObject(current, name);
                if (current is null) return null;
            }

            return current;
        }

        private static string? ReadPath(object? source, params string[] path)
        {
            var result = ReadPathObject(source, path);
            return result is null ? null : SafeText(result);
        }

        private static string? ReadMember(object? source, string name)
        {
            var result = ReadMemberObject(source, name);
            return result is null ? null : SafeText(result);
        }

        private static object? ReadMemberObject(object? source, string name)
        {
            if (source is null) return null;
            try
            {
                var type = source.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var property = type.GetProperty(name, flags);
                if (property is not null && property.GetIndexParameters().Length == 0) return property.GetValue(source, null);
                var field = type.GetField(name, flags);
                return field?.GetValue(source);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? Join(params string?[] values)
        {
            var present = new List<string>();
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && !present.Contains(value!)) present.Add(value!);
            }

            return present.Count == 0 ? null : string.Join(" | ", present);
        }

        private static string? SafeText(object value)
        {
            try
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
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
