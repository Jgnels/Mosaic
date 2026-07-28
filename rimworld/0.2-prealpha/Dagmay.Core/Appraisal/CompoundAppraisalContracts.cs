using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Appraisal
{
    /// <summary>
    /// Owner-calibrated emotion families supported by the 0.3 Core boundary.
    /// This enum describes game-story interpretation; it does not claim human
    /// psychological completeness or authentic inner experience.
    /// </summary>
    public enum MosaicEmotionFamily
    {
        PositiveWellbeing = 0,
        NegativeWellbeing = 1,
        Gratitude = 2,
        Admiration = 3,
        Reproach = 4,
        Anger = 5,
        Betrayal = 6,
        Heartbreak = 7,
        Love = 8,
        Trust = 9,
        Resentment = 10,
        Affection = 11,
        Suspicion = 12,
        Uncertainty = 13,
        Ambiguity = 14,
        Pride = 15
    }

    /// <summary>
    /// One ranked interpretation of a grounded event.
    /// </summary>
    public sealed class RankedAffectMeaning
    {
        public RankedAffectMeaning(
            MosaicEmotionFamily family,
            double intensity,
            int rank,
            string reasonCode,
            string explanation)
        {
            if (!Enum.IsDefined(typeof(MosaicEmotionFamily), family))
                throw new ArgumentOutOfRangeException(nameof(family));
            if (rank < 1 || rank > CompoundAppraisalResult.MaximumCandidates)
                throw new ArgumentOutOfRangeException(nameof(rank));

            Family = family;
            Intensity = ContractGuard.UnitInterval(intensity, nameof(intensity));
            Rank = rank;
            ReasonCode = ContractGuard.Text(reasonCode, nameof(reasonCode), 128);
            Explanation = ContractGuard.Text(explanation, nameof(explanation), 512);
        }

        public MosaicEmotionFamily Family { get; }
        public double Intensity { get; }
        public int Rank { get; }
        public string ReasonCode { get; }
        public string Explanation { get; }
    }

    /// <summary>
    /// Immutable compound appraisal output. It proposes interpretation only.
    /// It owns no memory, goal, relationship, persistence, provider, or pawn authority.
    /// </summary>
    public sealed class CompoundAppraisalResult
    {
        public const int MaximumCandidates = 4;
        public const int MaximumRuleIds = 16;

        public CompoundAppraisalResult(
            EventId evidenceId,
            IndividualId perspectiveOwnerId,
            IEnumerable<RankedAffectMeaning> candidates,
            int storyIntensity,
            string? immediateVisibleReaction,
            string? laterStoryConsequence,
            IEnumerable<string> sourceRuleIds)
        {
            if (evidenceId.Value == Guid.Empty)
                throw new ArgumentException("Compound appraisal requires evidence.", nameof(evidenceId));
            if (perspectiveOwnerId.Value == Guid.Empty)
                throw new ArgumentException("Compound appraisal requires a perspective owner.", nameof(perspectiveOwnerId));
            if (storyIntensity < 0 || storyIntensity > 5)
                throw new ArgumentOutOfRangeException(nameof(storyIntensity));

            var normalizedCandidates = (candidates ?? throw new ArgumentNullException(nameof(candidates)))
                .ToArray();
            if (normalizedCandidates.Length < 1 || normalizedCandidates.Length > MaximumCandidates)
                throw new ArgumentOutOfRangeException(nameof(candidates));
            if (normalizedCandidates.Any(value => value is null))
                throw new ArgumentException("Compound appraisal candidates cannot contain null.", nameof(candidates));
            if (normalizedCandidates.Select(value => value.Family).Distinct().Count() != normalizedCandidates.Length)
                throw new ArgumentException("Compound appraisal families must be unique.", nameof(candidates));

            var ordered = normalizedCandidates
                .OrderBy(value => value.Rank)
                .ThenBy(value => value.Family)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (ordered[index].Rank != index + 1)
                    throw new ArgumentException("Compound appraisal ranks must be contiguous and one-based.", nameof(candidates));
            }

            var normalizedRules = (sourceRuleIds ?? throw new ArgumentNullException(nameof(sourceRuleIds)))
                .Select(value => ContractGuard.Text(value, nameof(sourceRuleIds), 128))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (normalizedRules.Length < 1 || normalizedRules.Length > MaximumRuleIds)
                throw new ArgumentOutOfRangeException(nameof(sourceRuleIds));
            if (normalizedRules.Distinct(StringComparer.Ordinal).Count() != normalizedRules.Length)
                throw new ArgumentException("Source rule IDs must be unique.", nameof(sourceRuleIds));

            EvidenceId = evidenceId;
            PerspectiveOwnerId = perspectiveOwnerId;
            Candidates = new ReadOnlyCollection<RankedAffectMeaning>(ordered);
            StoryIntensity = storyIntensity;
            ImmediateVisibleReaction = ContractGuard.OptionalText(
                immediateVisibleReaction,
                nameof(immediateVisibleReaction),
                256);
            LaterStoryConsequence = ContractGuard.OptionalText(
                laterStoryConsequence,
                nameof(laterStoryConsequence),
                512);
            SourceRuleIds = new ReadOnlyCollection<string>(normalizedRules);
        }

        public EventId EvidenceId { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public IReadOnlyList<RankedAffectMeaning> Candidates { get; }
        public int StoryIntensity { get; }
        public string? ImmediateVisibleReaction { get; }
        public string? LaterStoryConsequence { get; }
        public IReadOnlyList<string> SourceRuleIds { get; }
        public RankedAffectMeaning Primary => Candidates[0];
        public RankedAffectMeaning? Secondary => Candidates.Count > 1 ? Candidates[1] : null;
    }
}
