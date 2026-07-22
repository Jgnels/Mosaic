using System;

namespace Dagmay.Core.Affect
{
    public sealed class AffectVector : IEquatable<AffectVector>
    {
        private const double Minimum = -1.0;
        private const double Maximum = 1.0;

        public AffectVector(
            double valence,
            double arousal,
            double threat,
            double agency,
            double attachment,
            double certainty,
            double socialStanding)
        {
            Valence = Validate(valence, nameof(valence));
            Arousal = Validate(arousal, nameof(arousal));
            Threat = Validate(threat, nameof(threat));
            Agency = Validate(agency, nameof(agency));
            Attachment = Validate(attachment, nameof(attachment));
            Certainty = Validate(certainty, nameof(certainty));
            SocialStanding = Validate(socialStanding, nameof(socialStanding));
        }

        public double Valence { get; }
        public double Arousal { get; }
        public double Threat { get; }
        public double Agency { get; }
        public double Attachment { get; }
        public double Certainty { get; }
        public double SocialStanding { get; }

        public static AffectVector Neutral { get; } = new AffectVector(0, 0, 0, 0, 0, 0, 0);

        public AffectVector BlendToward(AffectVector target, double intensity)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (double.IsNaN(intensity) || double.IsInfinity(intensity) || intensity < 0 || intensity > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be between 0 and 1.");
            }

            return new AffectVector(
                Blend(Valence, target.Valence, intensity),
                Blend(Arousal, target.Arousal, intensity),
                Blend(Threat, target.Threat, intensity),
                Blend(Agency, target.Agency, intensity),
                Blend(Attachment, target.Attachment, intensity),
                Blend(Certainty, target.Certainty, intensity),
                Blend(SocialStanding, target.SocialStanding, intensity));
        }

        public bool Equals(AffectVector? other)
        {
            return other is not null
                && Valence.Equals(other.Valence)
                && Arousal.Equals(other.Arousal)
                && Threat.Equals(other.Threat)
                && Agency.Equals(other.Agency)
                && Attachment.Equals(other.Attachment)
                && Certainty.Equals(other.Certainty)
                && SocialStanding.Equals(other.SocialStanding);
        }

        public override bool Equals(object? obj) => Equals(obj as AffectVector);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Valence.GetHashCode();
                hash = (hash * 31) + Arousal.GetHashCode();
                hash = (hash * 31) + Threat.GetHashCode();
                hash = (hash * 31) + Agency.GetHashCode();
                hash = (hash * 31) + Attachment.GetHashCode();
                hash = (hash * 31) + Certainty.GetHashCode();
                hash = (hash * 31) + SocialStanding.GetHashCode();
                return hash;
            }
        }

        private static double Validate(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < Minimum || value > Maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Affect dimensions must be finite values from -1 to 1.");
            }

            return value;
        }

        private static double Blend(double current, double target, double intensity)
        {
            var value = current + ((target - current) * intensity);
            return Math.Max(Minimum, Math.Min(Maximum, value));
        }
    }
}

