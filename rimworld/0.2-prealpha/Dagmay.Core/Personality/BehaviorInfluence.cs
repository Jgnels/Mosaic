using System;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Personality
{
    public enum BehaviorInfluenceKind
    {
        VanillaTrait=0, ExternalModTrait=1, CurrentNeed=2, CurrentMood=3, Ideology=4,
        RelationshipHistory=5, MosaicLearnedPreference=6, MosaicCommitment=7
    }

    public sealed class BehaviorInfluence
    {
        public BehaviorInfluence(BehaviorInfluenceKind kind, string key, double weight,
            string? sourceModId=null, string? explanation=null)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < -1d || weight > 1d)
                throw new ArgumentOutOfRangeException(nameof(weight));
            Kind = kind;
            Key = ContractGuard.Text(key, nameof(key), 256);
            Weight = weight;
            SourceModId = ContractGuard.OptionalText(sourceModId, nameof(sourceModId), 256);
            Explanation = ContractGuard.OptionalText(explanation, nameof(explanation), 1024);
            if (kind == BehaviorInfluenceKind.ExternalModTrait && SourceModId is null)
                throw new ArgumentException("External-mod trait influences require source mod provenance.", nameof(sourceModId));
        }
        public BehaviorInfluenceKind Kind { get; }
        public string Key { get; }
        public double Weight { get; }
        public string? SourceModId { get; }
        public string? Explanation { get; }
        public bool IsMosaicLearned => Kind==BehaviorInfluenceKind.MosaicLearnedPreference ||
            Kind==BehaviorInfluenceKind.RelationshipHistory || Kind==BehaviorInfluenceKind.MosaicCommitment;
    }
}
