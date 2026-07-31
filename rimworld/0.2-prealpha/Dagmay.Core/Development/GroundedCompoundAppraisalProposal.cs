using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Dagmay.Core.Development
{
    public static class GroundedCompoundAppraisalSource
    {
        public const string Contract = "Mosaic.Core.GroundedCompoundAppraisalProposal.v1";
        public const string FormalGateDigest = "045ef8a2fd6084c183ced7d2ec9516b3289a309f0b3c55963a895746c70de0d8";
        public const string AcceptedV42GateDigest = "b1da1f6ef60701772e890da2e546dd87a58a6bf433161681b0f9d848682c4157";
        public const string V42FormalGateDigest = "c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9";
        public const string CurrentSeedContract = "Mosaic.Core.GroundedCurrentEventAppraisalSeed.v1";
        public const string CurrentSeedAuthority = "TRUSTED_CURRENT_EVENT_EVIDENCE_NO_CANONICAL_OR_PAWN_AUTHORITY";
        public const string Authority = "PROPOSAL_ONLY_NO_CANONICAL_PRESENTATION_PROVIDER_PLANNER_UI_OR_PAWN_AUTHORITY";
        public const int MaximumFamilies = 3;
        public const int MaximumAffectDimensions = 6;
        public const int MaximumRelationshipDimensions = 4;
        public const int MaximumWhyContributions = 16;
        public const int MaximumBps = 10_000;

        internal static string FamilyToken(GroundedAppraisalFamily family) => family.ToString().ToUpperInvariant();
        internal static string RegimeToken(GroundedAppraisalRegime regime) =>
            regime == GroundedAppraisalRegime.NoHistory ? "NO_HISTORY" :
            regime == GroundedAppraisalRegime.OverwhelmingCurrent ? "OVERWHELMING_CURRENT" :
            regime == GroundedAppraisalRegime.RepeatedPattern ? "REPEATED_PATTERN" :
            regime == GroundedAppraisalRegime.ContradictoryHistory ? "CONTRADICTORY_HISTORY" :
            regime == GroundedAppraisalRegime.Balanced ? "BALANCED" :
            throw new ArgumentOutOfRangeException(nameof(regime));
        internal static string WhySourceToken(GroundedWhySource source) =>
            source == GroundedWhySource.CurrentEvent ? "CURRENT_EVENT" :
            source == GroundedWhySource.HistoricalContext ? "HISTORICAL_CONTEXT" :
            source == GroundedWhySource.Policy ? "POLICY" :
            throw new ArgumentOutOfRangeException(nameof(source));
    }

    public enum GroundedAppraisalFamily
    {
        Gratitude,
        Trust,
        Suspicion,
        Resentment,
        Betrayal,
        Heartbreak,
        Affection,
        Pride,
        Respect,
        Relief,
        Fear,
        Anger,
        Shame,
        Distress,
        Wellbeing,
        Ambiguity
    }

    public enum GroundedAppraisalRegime
    {
        NoHistory,
        OverwhelmingCurrent,
        RepeatedPattern,
        ContradictoryHistory,
        Balanced
    }

    public enum GroundedWhySource
    {
        CurrentEvent,
        HistoricalContext,
        Policy
    }

    public sealed class GroundedAppraisalFamilySynthesis
    {
        internal GroundedAppraisalFamilySynthesis(
            GroundedAppraisalFamily family,
            int currentEvidenceBps,
            int historicalEvidenceBps,
            int netEvidenceBps,
            bool contradictory,
            int confidenceBps,
            int sourceCount)
        {
            Family = family;
            CurrentEvidenceBps = currentEvidenceBps;
            HistoricalEvidenceBps = historicalEvidenceBps;
            NetEvidenceBps = netEvidenceBps;
            Contradictory = contradictory;
            ConfidenceBps = confidenceBps;
            SourceCount = sourceCount;
            if (currentEvidenceBps < 0 || currentEvidenceBps > GroundedCompoundAppraisalSource.MaximumBps ||
                historicalEvidenceBps < 0 || historicalEvidenceBps > GroundedCompoundAppraisalSource.MaximumBps ||
                netEvidenceBps != Math.Min(GroundedCompoundAppraisalSource.MaximumBps, currentEvidenceBps + historicalEvidenceBps) ||
                contradictory || confidenceBps < 0 || confidenceBps > GroundedCompoundAppraisalSource.MaximumBps ||
                sourceCount < 1 || sourceCount > 5)
                throw new ArgumentException("Appraisal family synthesis is invalid.");
        }

        public GroundedAppraisalFamily Family { get; }
        public int CurrentEvidenceBps { get; }
        public int HistoricalEvidenceBps { get; }
        public int NetEvidenceBps { get; }
        public bool Contradictory { get; }
        public int ConfidenceBps { get; }
        public int SourceCount { get; }

        internal string DeterministicJson() =>
            "{" +
            "\"confidence_bps\":" + GroundedJson.Number(ConfidenceBps) + "," +
            "\"contradictory\":" + (Contradictory ? "true" : "false") + "," +
            "\"current_evidence_bps\":" + GroundedJson.Number(CurrentEvidenceBps) + "," +
            "\"family\":" + GroundedJson.String(GroundedCompoundAppraisalSource.FamilyToken(Family)) + "," +
            "\"historical_evidence_bps\":" + GroundedJson.Number(HistoricalEvidenceBps) + "," +
            "\"net_evidence_bps\":" + GroundedJson.Number(NetEvidenceBps) + "," +
            "\"source_count\":" + GroundedJson.Number(SourceCount) +
            "}";
    }

    public sealed class GroundedAppraisalDimensionProposal
    {
        internal GroundedAppraisalDimensionProposal(
            string dimension,
            int currentEvidenceBps,
            int historicalEvidenceBps,
            int proposedBps,
            bool contradictory,
            int confidenceBps)
        {
            Dimension = RetrievalCanonical.Token(dimension, nameof(dimension));
            CurrentEvidenceBps = currentEvidenceBps;
            HistoricalEvidenceBps = historicalEvidenceBps;
            ProposedBps = proposedBps;
            Contradictory = contradictory;
            ConfidenceBps = confidenceBps;
            if (Math.Abs(currentEvidenceBps) > GroundedCompoundAppraisalSource.MaximumBps ||
                Math.Abs(historicalEvidenceBps) > GroundedCompoundAppraisalSource.MaximumBps ||
                proposedBps != Bound(currentEvidenceBps + historicalEvidenceBps) ||
                contradictory != (currentEvidenceBps != 0 && historicalEvidenceBps != 0 &&
                    Math.Sign(currentEvidenceBps) != Math.Sign(historicalEvidenceBps)) ||
                confidenceBps < 0 || confidenceBps > GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentException("Appraisal dimension proposal is invalid.");
        }

        public string Dimension { get; }
        public int CurrentEvidenceBps { get; }
        public int HistoricalEvidenceBps { get; }
        public int ProposedBps { get; }
        public bool Contradictory { get; }
        public int ConfidenceBps { get; }

        internal string DeterministicJson() =>
            "{" +
            "\"confidence_bps\":" + GroundedJson.Number(ConfidenceBps) + "," +
            "\"contradictory\":" + (Contradictory ? "true" : "false") + "," +
            "\"current_evidence_bps\":" + GroundedJson.Number(CurrentEvidenceBps) + "," +
            "\"dimension\":" + GroundedJson.String(Dimension) + "," +
            "\"historical_evidence_bps\":" + GroundedJson.Number(HistoricalEvidenceBps) + "," +
            "\"proposed_bps\":" + GroundedJson.Number(ProposedBps) +
            "}";

        private static int Bound(int value) =>
            Math.Max(-GroundedCompoundAppraisalSource.MaximumBps,
                Math.Min(GroundedCompoundAppraisalSource.MaximumBps, value));
    }

    public sealed class GroundedCharacterWhyContribution
    {
        internal GroundedCharacterWhyContribution(
            GroundedWhySource source,
            string ruleId,
            string subject,
            int signedBps,
            IEnumerable<string> rootEventIds)
        {
            Source = source;
            RuleId = RetrievalCanonical.Token(ruleId, nameof(ruleId));
            Subject = RetrievalCanonical.Token(subject, nameof(subject));
            if (Math.Abs(signedBps) > GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentOutOfRangeException(nameof(signedBps));
            SignedBps = signedBps;
            var roots = (rootEventIds ?? throw new ArgumentNullException(nameof(rootEventIds)))
                .Select(value => RetrievalCanonical.Text(value, nameof(rootEventIds)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (roots.Count > 8) throw new ArgumentOutOfRangeException(nameof(rootEventIds));
            RootEventIds = new ReadOnlyCollection<string>(roots);
            if (Source == GroundedWhySource.Policy && RootEventIds.Count != 0)
                throw new ArgumentException("Policy contributions cannot claim event roots.", nameof(rootEventIds));
            if (Source != GroundedWhySource.Policy && RootEventIds.Count == 0)
                throw new ArgumentException("Evidence contributions require exact event roots.", nameof(rootEventIds));
        }

        public GroundedWhySource Source { get; }
        public string RuleId { get; }
        public string Subject { get; }
        public int SignedBps { get; }
        public IReadOnlyList<string> RootEventIds { get; }

        internal string DeterministicJson() =>
            "{" +
            "\"root_event_ids\":" + GroundedJson.Strings(RootEventIds) + "," +
            "\"rule_id\":" + GroundedJson.String(RuleId) + "," +
            "\"signed_bps\":" + GroundedJson.Number(SignedBps) + "," +
            "\"source\":" + GroundedJson.String(GroundedCompoundAppraisalSource.WhySourceToken(Source)) + "," +
            "\"subject\":" + GroundedJson.String(Subject) +
            "}";
    }

    public sealed class GroundedCompoundAppraisalProposal
    {
        internal GroundedCompoundAppraisalProposal(
            string proposalId,
            string ownerId,
            string lineageId,
            string? counterpartId,
            string currentEventId,
            string currentEventHash,
            string currentSeedFingerprint,
            string contextPacketFingerprint,
            string requestFingerprint,
            GroundedAppraisalRegime regime,
            int currentCapBps,
            int historyCapBps,
            IEnumerable<GroundedAppraisalFamilySynthesis> families,
            IEnumerable<GroundedAppraisalDimensionProposal> affectDimensions,
            IEnumerable<GroundedAppraisalDimensionProposal> relationshipConsiderations,
            int intensityBps,
            int confidenceBps,
            IEnumerable<string> uncertaintyCodes,
            IEnumerable<GroundedCharacterWhyContribution> why,
            string fingerprint,
            bool verifyFingerprint)
        {
            ProposalId = RetrievalCanonical.Text(proposalId, nameof(proposalId));
            if (!ProposalId.StartsWith("appraisal-proposal:", StringComparison.Ordinal))
                throw new ArgumentException("Proposal ID namespace is invalid.", nameof(proposalId));
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            CurrentEventId = RetrievalCanonical.Text(currentEventId, nameof(currentEventId));
            CurrentEventHash = RetrievalCanonical.Hex(currentEventHash, nameof(currentEventHash));
            CurrentSeedFingerprint = RetrievalCanonical.Hex(currentSeedFingerprint, nameof(currentSeedFingerprint));
            ContextPacketFingerprint = RetrievalCanonical.Hex(contextPacketFingerprint, nameof(contextPacketFingerprint));
            RequestFingerprint = RetrievalCanonical.Hex(requestFingerprint, nameof(requestFingerprint));
            Regime = regime;
            if (currentCapBps < 0 || historyCapBps < 0 ||
                currentCapBps + historyCapBps != GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentException("Appraisal source budgets are invalid.");
            CurrentCapBps = currentCapBps;
            HistoryCapBps = historyCapBps;
            Families = new ReadOnlyCollection<GroundedAppraisalFamilySynthesis>(
                (families ?? throw new ArgumentNullException(nameof(families))).ToList());
            AffectDimensions = new ReadOnlyCollection<GroundedAppraisalDimensionProposal>(
                (affectDimensions ?? throw new ArgumentNullException(nameof(affectDimensions))).ToList());
            RelationshipConsiderations = new ReadOnlyCollection<GroundedAppraisalDimensionProposal>(
                (relationshipConsiderations ?? throw new ArgumentNullException(nameof(relationshipConsiderations))).ToList());
            if (Families.Count > GroundedCompoundAppraisalSource.MaximumFamilies ||
                AffectDimensions.Count > GroundedCompoundAppraisalSource.MaximumAffectDimensions ||
                RelationshipConsiderations.Count > GroundedCompoundAppraisalSource.MaximumRelationshipDimensions)
                throw new ArgumentException("Compound appraisal output bound exceeded.");
            RequireOrderAndUniqueness();
            if (Families.Sum(value => value.CurrentEvidenceBps) > CurrentCapBps ||
                Families.Sum(value => value.HistoricalEvidenceBps) > HistoryCapBps)
                throw new ArgumentException("Shared family support budget exceeded.");
            var dimensions = AffectDimensions.Concat(RelationshipConsiderations).ToArray();
            if (dimensions.Sum(value => Math.Abs(value.CurrentEvidenceBps)) > CurrentCapBps ||
                dimensions.Sum(value => Math.Abs(value.HistoricalEvidenceBps)) > HistoryCapBps ||
                dimensions.Sum(value => Math.Abs(value.ProposedBps)) > GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentException("Shared dimension influence budget exceeded.");
            if (intensityBps < 0 || intensityBps > GroundedCompoundAppraisalSource.MaximumBps ||
                confidenceBps < 0 || confidenceBps > GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentOutOfRangeException(nameof(intensityBps));
            IntensityBps = intensityBps;
            ConfidenceBps = confidenceBps;
            UncertaintyCodes = new ReadOnlyCollection<string>((uncertaintyCodes ?? throw new ArgumentNullException(nameof(uncertaintyCodes)))
                .Select(value => RetrievalCanonical.Token(value, nameof(uncertaintyCodes)).ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList());
            Why = new ReadOnlyCollection<GroundedCharacterWhyContribution>(
                (why ?? throw new ArgumentNullException(nameof(why))).ToList());
            if (Why.Count > GroundedCompoundAppraisalSource.MaximumWhyContributions)
                throw new ArgumentOutOfRangeException(nameof(why));
            SourceContract = GroundedCompoundAppraisalSource.Contract;
            SourceGateDigest = GroundedCompoundAppraisalSource.FormalGateDigest;
            Authority = GroundedCompoundAppraisalSource.Authority;
            Fingerprint = RetrievalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (verifyFingerprint && !VerifyFingerprint())
                throw new ArgumentException("Compound appraisal fingerprint mismatch.", nameof(fingerprint));
        }

        public string ProposalId { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string CurrentEventId { get; }
        public string CurrentEventHash { get; }
        public string CurrentSeedFingerprint { get; }
        public string ContextPacketFingerprint { get; }
        public string RequestFingerprint { get; }
        public GroundedAppraisalRegime Regime { get; }
        public int CurrentCapBps { get; }
        public int HistoryCapBps { get; }
        public IReadOnlyList<GroundedAppraisalFamilySynthesis> Families { get; }
        public IReadOnlyList<GroundedAppraisalDimensionProposal> AffectDimensions { get; }
        public IReadOnlyList<GroundedAppraisalDimensionProposal> RelationshipConsiderations { get; }
        public int IntensityBps { get; }
        public int ConfidenceBps { get; }
        public IReadOnlyList<string> UncertaintyCodes { get; }
        public IReadOnlyList<GroundedCharacterWhyContribution> Why { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string Authority { get; }
        public string Fingerprint { get; }

        public string ToDeterministicJson() => DeterministicJson(true);

        public bool VerifyFingerprint() =>
            string.Equals(Fingerprint, GroundedJson.Hash(DeterministicJson(false)), StringComparison.Ordinal);

        public static GroundedCompoundAppraisalProposal RequireValid(GroundedCompoundAppraisalProposal proposal)
        {
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));
            if (!string.Equals(proposal.SourceContract, GroundedCompoundAppraisalSource.Contract, StringComparison.Ordinal) ||
                !string.Equals(proposal.SourceGateDigest, GroundedCompoundAppraisalSource.FormalGateDigest, StringComparison.Ordinal) ||
                !string.Equals(proposal.Authority, GroundedCompoundAppraisalSource.Authority, StringComparison.Ordinal) ||
                !proposal.VerifyFingerprint())
                throw new ArgumentException("Compound appraisal proposal failed public verification.", nameof(proposal));
            return proposal;
        }

        internal string DeterministicJson(bool includeFingerprint)
        {
            var fields = new List<string>
            {
                "\"affect_dimensions\":[" + string.Join(",", AffectDimensions.Select(value => value.DeterministicJson())) + "]",
                "\"authority\":" + GroundedJson.String(Authority),
                "\"confidence_bps\":" + GroundedJson.Number(ConfidenceBps),
                "\"context_packet_fingerprint\":" + GroundedJson.String(ContextPacketFingerprint),
                "\"counterpart_id\":" + GroundedJson.OptionalString(CounterpartId),
                "\"current_cap_bps\":" + GroundedJson.Number(CurrentCapBps),
                "\"current_event_hash\":" + GroundedJson.String(CurrentEventHash),
                "\"current_event_id\":" + GroundedJson.String(CurrentEventId),
                "\"current_seed_fingerprint\":" + GroundedJson.String(CurrentSeedFingerprint),
                "\"families\":[" + string.Join(",", Families.Select(value => value.DeterministicJson())) + "]"
            };
            if (includeFingerprint) fields.Add("\"fingerprint\":" + GroundedJson.String(Fingerprint));
            fields.Add("\"history_cap_bps\":" + GroundedJson.Number(HistoryCapBps));
            fields.Add("\"intensity_bps\":" + GroundedJson.Number(IntensityBps));
            fields.Add("\"lineage_id\":" + GroundedJson.String(LineageId));
            fields.Add("\"owner_id\":" + GroundedJson.String(OwnerId));
            fields.Add("\"proposal_id\":" + GroundedJson.String(ProposalId));
            fields.Add("\"regime\":" + GroundedJson.String(GroundedCompoundAppraisalSource.RegimeToken(Regime)));
            fields.Add("\"relationship_considerations\":[" + string.Join(",", RelationshipConsiderations.Select(value => value.DeterministicJson())) + "]");
            fields.Add("\"request_fingerprint\":" + GroundedJson.String(RequestFingerprint));
            fields.Add("\"source_contract\":" + GroundedJson.String(SourceContract));
            fields.Add("\"source_gate_digest\":" + GroundedJson.String(SourceGateDigest));
            fields.Add("\"uncertainty_codes\":" + GroundedJson.Strings(UncertaintyCodes));
            fields.Add("\"why\":[" + string.Join(",", Why.Select(value => value.DeterministicJson())) + "]");
            return "{" + string.Join(",", fields) + "}";
        }

        private void RequireOrderAndUniqueness()
        {
            var expectedFamilies = Families
                .OrderByDescending(value => Math.Abs(value.NetEvidenceBps))
                .ThenBy(value => GroundedCompoundAppraisalSource.FamilyToken(value.Family), StringComparer.Ordinal)
                .ToArray();
            if (!Families.SequenceEqual(expectedFamilies) || Families.Select(value => value.Family).Distinct().Count() != Families.Count)
                throw new ArgumentException("Family order or uniqueness is invalid.");
            foreach (var values in new[] { AffectDimensions, RelationshipConsiderations })
            {
                var expected = values.OrderByDescending(value => Math.Abs(value.ProposedBps))
                    .ThenBy(value => value.Dimension, StringComparer.Ordinal).ToArray();
                if (!values.SequenceEqual(expected) ||
                    values.Select(value => value.Dimension).Distinct(StringComparer.Ordinal).Count() != values.Count)
                    throw new ArgumentException("Dimension order or uniqueness is invalid.");
            }
        }
    }
}
