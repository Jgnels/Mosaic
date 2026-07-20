using System;

namespace Dagmay.Core.Identity
{
    public sealed class ContinuityProfile
    {
        public ContinuityProfile(
            double groundedSeed,
            double autobiographicalHistory,
            double relationships,
            double valuesAndCommitments,
            double expressionAndHabits)
        {
            GroundedSeed = Validate(groundedSeed, nameof(groundedSeed));
            AutobiographicalHistory = Validate(autobiographicalHistory, nameof(autobiographicalHistory));
            Relationships = Validate(relationships, nameof(relationships));
            ValuesAndCommitments = Validate(valuesAndCommitments, nameof(valuesAndCommitments));
            ExpressionAndHabits = Validate(expressionAndHabits, nameof(expressionAndHabits));

            var total = GroundedSeed + AutobiographicalHistory + Relationships + ValuesAndCommitments + ExpressionAndHabits;
            if (Math.Abs(total - 1.0) > 0.000001)
            {
                throw new ArgumentException("Continuity weights must total 1.0.");
            }
        }

        public double GroundedSeed { get; }
        public double AutobiographicalHistory { get; }
        public double Relationships { get; }
        public double ValuesAndCommitments { get; }
        public double ExpressionAndHabits { get; }

        public static ContinuityProfile InitialDefault { get; } = new ContinuityProfile(
            groundedSeed: 0.30,
            autobiographicalHistory: 0.20,
            relationships: 0.20,
            valuesAndCommitments: 0.20,
            expressionAndHabits: 0.10);

        private static double Validate(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(parameterName, "A continuity weight must be between 0 and 1.");
            }

            return value;
        }
    }
}

