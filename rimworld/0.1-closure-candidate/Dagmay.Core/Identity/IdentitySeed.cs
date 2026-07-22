using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dagmay.Core.Identity
{
    public enum SeedFactCategory
    {
        Backstory,
        Trait,
        Skill,
        Passion,
        Ideology,
        Gene,
        Relationship,
        Health,
        Age,
        Circumstance
    }

    public sealed class SeedFact
    {
        public SeedFact(SeedFactCategory category, string key, string value, string source, double confidence)
        {
            Category = category;
            Key = RequireText(key, nameof(key), 128);
            Value = RequireText(value, nameof(value), 2048);
            Source = RequireText(source, nameof(source), 256);

            if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            Confidence = confidence;
        }

        public SeedFactCategory Category { get; }
        public string Key { get; }
        public string Value { get; }
        public string Source { get; }
        public double Confidence { get; }

        private static string RequireText(string value, string parameterName, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }
    }

    public sealed class IdentitySeed
    {
        public IdentitySeed(string environment, string adapterVersion, IEnumerable<SeedFact> facts)
        {
            if (string.IsNullOrWhiteSpace(environment)) throw new ArgumentException("Environment is required.", nameof(environment));
            if (string.IsNullOrWhiteSpace(adapterVersion)) throw new ArgumentException("Adapter version is required.", nameof(adapterVersion));
            if (facts is null) throw new ArgumentNullException(nameof(facts));

            Environment = environment.Trim();
            AdapterVersion = adapterVersion.Trim();
            Facts = new ReadOnlyCollection<SeedFact>(new List<SeedFact>(facts));
        }

        public string Environment { get; }
        public string AdapterVersion { get; }
        public IReadOnlyList<SeedFact> Facts { get; }
    }
}

