using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Dagmay.Core.Development
{
    public sealed class GroundedCompoundAppraisalSynthesizer
    {
        public GroundedCompoundAppraisalProposal Synthesize(ContextualAppraisalRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var seed = request.CurrentSeed;
            var packet = request.ContextPacket;
            var regime = SelectRegime(seed, packet, out var currentCap, out var historyCap);

            var currentFamilies = Scale(
                seed.FamilyEvidenceBps.Select(value => new KeyValuePair<string, int>(
                    GroundedCompoundAppraisalSource.FamilyToken(value.Key), value.Value)), currentCap)
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
            var currentDimensions = Scale(
                seed.AffectEvidenceBps.Select(value => new KeyValuePair<string, int>("affect." + value.Key, value.Value))
                    .Concat(seed.RelationshipEvidenceBps.Select(value =>
                        new KeyValuePair<string, int>("relationship." + value.Key, value.Value))), currentCap)
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
            var historyDimensions = Scale(
                packet.Dimensions.Where(value => value.NetEvidenceBps != 0)
                    .Select(value => new KeyValuePair<string, int>(value.Dimension, value.NetEvidenceBps)), historyCap)
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
            var historicalFamilies = FamiliesFromHistory(historyDimensions);

            var familyRows = new List<GroundedAppraisalFamilySynthesis>();
            var allFamilies = new HashSet<GroundedAppraisalFamily>(historicalFamilies.Keys);
            foreach (var token in currentFamilies.Keys)
                allFamilies.Add(ParseFamily(token));
            foreach (var family in allFamilies.OrderBy(GroundedCompoundAppraisalSource.FamilyToken, StringComparer.Ordinal))
            {
                currentFamilies.TryGetValue(GroundedCompoundAppraisalSource.FamilyToken(family).ToLowerInvariant(), out var current);
                historicalFamilies.TryGetValue(family, out var historyValues);
                historyValues = historyValues ?? new List<int>();
                var historical = historyValues.Sum();
                var net = Math.Min(GroundedCompoundAppraisalSource.MaximumBps, current + historical);
                var total = Math.Abs(current) + historyValues.Sum(value => Math.Abs(value));
                var familyConfidence = total == 0 ? 0 : (int)((long)Math.Abs(net) * 10_000L / total);
                if (net != 0)
                    familyRows.Add(new GroundedAppraisalFamilySynthesis(
                        family, current, historical, net, false,
                        Math.Min(GroundedCompoundAppraisalSource.MaximumBps, familyConfidence),
                        (current == 0 ? 0 : 1) + historyValues.Count));
            }
            familyRows = familyRows
                .OrderByDescending(value => Math.Abs(value.NetEvidenceBps))
                .ThenBy(value => GroundedCompoundAppraisalSource.FamilyToken(value.Family), StringComparer.Ordinal)
                .ToList();
            var families = familyRows.Take(GroundedCompoundAppraisalSource.MaximumFamilies).ToList();
            if (families.Count == 0)
            {
                var valence = currentDimensions.TryGetValue("affect.valence", out var directValence)
                    ? directValence
                    : currentDimensions.Where(value => value.Key.StartsWith("affect.", StringComparison.Ordinal))
                        .Sum(value => value.Value);
                families.Add(new GroundedAppraisalFamilySynthesis(
                    valence >= 0 ? GroundedAppraisalFamily.Wellbeing : GroundedAppraisalFamily.Distress,
                    1, 0, 1, false, 1_000, 1));
            }

            var affect = SynthesizeDimensions(
                currentDimensions, historyDimensions, "affect.",
                GroundedCompoundAppraisalSource.MaximumAffectDimensions, true);
            var relationship = SynthesizeDimensions(
                currentDimensions, historyDimensions, "relationship.",
                GroundedCompoundAppraisalSource.MaximumRelationshipDimensions, false);

            var uncertainty = new HashSet<string>(packet.UncertaintyCodes, StringComparer.Ordinal);
            if (regime == GroundedAppraisalRegime.ContradictoryHistory)
                uncertainty.Add("CURRENT_AND_HISTORY_DISAGREE");
            if (packet.Items.Count == 0)
                uncertainty.Add("NO_HISTORICAL_CONTEXT_USED");
            if (affect.Concat(relationship).Any(value => value.Contradictory))
                uncertainty.Add("CONTRADICTION_PRESERVED");
            if (!seed.RelationshipRelevant && historyDimensions.Keys.Any(value =>
                    value.StartsWith("relationship.", StringComparison.Ordinal)))
                uncertainty.Add("HISTORY_NOT_ALLOWED_TO_CREATE_RELATIONSHIP_CHANGE");
            if (familyRows.Count > GroundedCompoundAppraisalSource.MaximumFamilies)
                uncertainty.Add("LOWER_RANKED_FAMILIES_OMITTED");

            var intensity = families.Select(value => Math.Abs(value.NetEvidenceBps))
                .Concat(affect.Select(value => Math.Abs(value.ProposedBps)))
                .Concat(relationship.Select(value => Math.Abs(value.ProposedBps)))
                .DefaultIfEmpty(0).Max();
            var confidenceValues = families.Select(value => value.ConfidenceBps)
                .Concat(affect.Select(value => value.ConfidenceBps))
                .Concat(relationship.Select(value => value.ConfidenceBps)).ToArray();
            var confidence = confidenceValues.Length == 0 ? 0 : confidenceValues.Sum() / confidenceValues.Length;
            if (uncertainty.Contains("CONTRADICTION_PRESERVED")) confidence = confidence * 3 / 4;

            var why = BuildWhy(seed, packet, regime, historyCap, uncertainty);
            var requestFingerprint = request.Fingerprint;
            var proposalId = "appraisal-proposal:" + GroundedJson.Hash(
                "{" +
                "\"contract\":" + GroundedJson.String(GroundedCompoundAppraisalSource.Contract) + "," +
                "\"request\":" + GroundedJson.String(requestFingerprint) +
                "}").Substring(0, 32);

            var provisional = new GroundedCompoundAppraisalProposal(
                proposalId, seed.OwnerId, seed.LineageId, seed.CounterpartId,
                seed.EventId, seed.EventHash, seed.Fingerprint, packet.Fingerprint,
                requestFingerprint, regime, currentCap, historyCap, families, affect,
                relationship, intensity, confidence, uncertainty, why, new string('0', 64), false);
            var fingerprint = GroundedJson.Hash(provisional.DeterministicJson(false));
            return GroundedCompoundAppraisalProposal.RequireValid(
                new GroundedCompoundAppraisalProposal(
                    proposalId, seed.OwnerId, seed.LineageId, seed.CounterpartId,
                    seed.EventId, seed.EventHash, seed.Fingerprint, packet.Fingerprint,
                    requestFingerprint, regime, currentCap, historyCap, families, affect,
                    relationship, intensity, confidence, uncertainty, why, fingerprint, true));
        }

        public static IReadOnlyList<KeyValuePair<string, int>> Scale(
            IEnumerable<KeyValuePair<string, int>> values,
            int capBps)
        {
            if (capBps < 0 || capBps > GroundedCompoundAppraisalSource.MaximumBps)
                throw new ArgumentOutOfRangeException(nameof(capBps));
            var nonzero = (values ?? throw new ArgumentNullException(nameof(values)))
                .Where(value => value.Value != 0)
                .Select(value => new KeyValuePair<string, int>(
                    RetrievalCanonical.Token(value.Key, nameof(values)), value.Value))
                .OrderBy(value => value.Key, StringComparer.Ordinal).ToList();
            if (nonzero.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != nonzero.Count ||
                nonzero.Any(value => Math.Abs(value.Value) > GroundedCompoundAppraisalSource.MaximumBps))
                throw new ArgumentException("Appraisal evidence is invalid.", nameof(values));
            if (nonzero.Count == 0 || capBps == 0)
                return new ReadOnlyCollection<KeyValuePair<string, int>>(new List<KeyValuePair<string, int>>());
            var total = nonzero.Sum(value => Math.Abs(value.Value));
            if (total <= capBps)
                return new ReadOnlyCollection<KeyValuePair<string, int>>(nonzero);
            var rows = new List<Allocation>();
            var used = 0;
            foreach (var pair in nonzero)
            {
                var numerator = (long)Math.Abs(pair.Value) * capBps;
                var amount = (int)(numerator / total);
                rows.Add(new Allocation(pair.Key, pair.Value > 0 ? amount : -amount,
                    numerator % total, pair.Value > 0 ? 1 : -1));
                used += amount;
            }
            var remaining = capBps - used;
            foreach (var row in rows.OrderByDescending(value => value.Remainder)
                         .ThenBy(value => value.Name, StringComparer.Ordinal).Take(remaining))
                row.Amount += row.Sign;
            var result = rows.Where(value => value.Amount != 0)
                .OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => new KeyValuePair<string, int>(value.Name, value.Amount)).ToList();
            if (result.Sum(value => Math.Abs(value.Value)) > capBps)
                throw new InvalidOperationException("Shared appraisal budget exceeded.");
            return new ReadOnlyCollection<KeyValuePair<string, int>>(result);
        }

        private static GroundedAppraisalRegime SelectRegime(
            GroundedCurrentEventAppraisalSeed seed,
            GroundedDevelopmentalContextPacket packet,
            out int currentCap,
            out int historyCap)
        {
            var currentPeak = seed.FamilyEvidenceBps.Select(value => Math.Abs(value.Value))
                .Concat(seed.AffectEvidenceBps.Select(value => Math.Abs(value.Value)))
                .Concat(seed.RelationshipEvidenceBps.Select(value => Math.Abs(value.Value)))
                .DefaultIfEmpty(0).Max();
            if (packet.Items.Count == 0 || packet.Dimensions.Count == 0)
                return Set(GroundedAppraisalRegime.NoHistory, 10_000, 0, out currentCap, out historyCap);
            if (currentPeak >= 8_500)
                return Set(GroundedAppraisalRegime.OverwhelmingCurrent, 8_000, 2_000, out currentCap, out historyCap);
            var current = seed.AffectEvidenceBps.ToDictionary(
                value => "affect." + value.Key, value => value.Value, StringComparer.Ordinal);
            foreach (var value in seed.RelationshipEvidenceBps)
                current["relationship." + value.Key] = value.Value;
            var aligned = 0;
            var contradictions = 0;
            foreach (var dimension in packet.Dimensions)
            {
                current.TryGetValue(dimension.Dimension, out var currentValue);
                if (currentValue != 0 && dimension.NetEvidenceBps != 0 &&
                    Math.Sign(currentValue) == Math.Sign(dimension.NetEvidenceBps))
                    aligned += dimension.SourceCount;
                if (currentValue != 0 && dimension.NetEvidenceBps != 0 &&
                    Math.Sign(currentValue) != Math.Sign(dimension.NetEvidenceBps))
                    contradictions++;
                contradictions += dimension.ContradictionCount;
            }
            if (aligned >= 2)
                return Set(GroundedAppraisalRegime.RepeatedPattern, 4_500, 5_500, out currentCap, out historyCap);
            if (contradictions > 0 || packet.UncertaintyCodes.Contains(
                    "CONTRADICTORY_HISTORY_PRESERVED", StringComparer.Ordinal))
                return Set(GroundedAppraisalRegime.ContradictoryHistory, 6_500, 3_500, out currentCap, out historyCap);
            return Set(GroundedAppraisalRegime.Balanced, 6_000, 4_000, out currentCap, out historyCap);
        }

        private static GroundedAppraisalRegime Set(
            GroundedAppraisalRegime regime, int current, int history,
            out int currentCap, out int historyCap)
        {
            currentCap = current;
            historyCap = history;
            return regime;
        }

        private static Dictionary<GroundedAppraisalFamily, List<int>> FamiliesFromHistory(
            IReadOnlyDictionary<string, int> history)
        {
            var result = new Dictionary<GroundedAppraisalFamily, List<int>>();
            foreach (var pair in history)
            {
                if (pair.Key == "affect.valence")
                    Add(pair.Value > 0 ? GroundedAppraisalFamily.Wellbeing : GroundedAppraisalFamily.Distress, Math.Abs(pair.Value));
                else if (pair.Key == "affect.threat" && pair.Value > 0) Add(GroundedAppraisalFamily.Fear, pair.Value);
                else if (pair.Key == "affect.anger" && pair.Value > 0) Add(GroundedAppraisalFamily.Anger, pair.Value);
                else if (pair.Key == "affect.shame" && pair.Value > 0) Add(GroundedAppraisalFamily.Shame, pair.Value);
                else if (pair.Key == "affect.relief" && pair.Value > 0) Add(GroundedAppraisalFamily.Relief, pair.Value);
                else if (pair.Key == "relationship.trust")
                    Add(pair.Value > 0 ? GroundedAppraisalFamily.Trust : GroundedAppraisalFamily.Suspicion, Math.Abs(pair.Value));
                else if (pair.Key == "relationship.resentment" && pair.Value > 0) Add(GroundedAppraisalFamily.Resentment, pair.Value);
                else if (pair.Key == "relationship.affection")
                    Add(pair.Value > 0 ? GroundedAppraisalFamily.Affection : GroundedAppraisalFamily.Heartbreak, Math.Abs(pair.Value));
                else if (pair.Key == "relationship.betrayal" && pair.Value > 0) Add(GroundedAppraisalFamily.Betrayal, pair.Value);
                else if (pair.Key == "relationship.respect" && pair.Value > 0) Add(GroundedAppraisalFamily.Respect, pair.Value);
            }
            return result;

            void Add(GroundedAppraisalFamily family, int value)
            {
                if (value <= 0) return;
                if (!result.TryGetValue(family, out var values))
                {
                    values = new List<int>();
                    result.Add(family, values);
                }
                values.Add(value);
            }
        }

        private static IReadOnlyList<GroundedAppraisalDimensionProposal> SynthesizeDimensions(
            IReadOnlyDictionary<string, int> current,
            IReadOnlyDictionary<string, int> history,
            string prefix,
            int limit,
            bool allowHistoryWithoutCurrent)
        {
            var names = current.Keys.Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
                .Concat(history.Keys.Where(value => value.StartsWith(prefix, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
            var result = new List<GroundedAppraisalDimensionProposal>();
            foreach (var name in names)
            {
                current.TryGetValue(name, out var currentValue);
                history.TryGetValue(name, out var historyValue);
                if (!allowHistoryWithoutCurrent && currentValue == 0) continue;
                var proposed = Math.Max(-10_000, Math.Min(10_000, currentValue + historyValue));
                if (proposed == 0 && currentValue == 0) continue;
                var contradictory = currentValue != 0 && historyValue != 0 && Math.Sign(currentValue) != Math.Sign(historyValue);
                var total = Math.Abs(currentValue) + Math.Abs(historyValue);
                var confidence = total == 0 ? 0 : (int)((long)Math.Abs(proposed) * 10_000L / total);
                result.Add(new GroundedAppraisalDimensionProposal(
                    name.Substring(prefix.Length), currentValue, historyValue,
                    proposed, contradictory, confidence));
            }
            return new ReadOnlyCollection<GroundedAppraisalDimensionProposal>(result
                .OrderByDescending(value => Math.Abs(value.ProposedBps))
                .ThenBy(value => value.Dimension, StringComparer.Ordinal)
                .Take(limit).ToList());
        }

        private static IReadOnlyList<GroundedCharacterWhyContribution> BuildWhy(
            GroundedCurrentEventAppraisalSeed seed,
            GroundedDevelopmentalContextPacket packet,
            GroundedAppraisalRegime regime,
            int historyCap,
            ISet<string> uncertainty)
        {
            var why = new List<GroundedCharacterWhyContribution>();
            var rule = seed.RuleIds[0];
            foreach (var value in seed.FamilyEvidenceBps)
                why.Add(new GroundedCharacterWhyContribution(
                    GroundedWhySource.CurrentEvent, rule,
                    "family." + GroundedCompoundAppraisalSource.FamilyToken(value.Key),
                    value.Value, seed.RootEventIds));
            foreach (var value in seed.AffectEvidenceBps)
                why.Add(new GroundedCharacterWhyContribution(
                    GroundedWhySource.CurrentEvent, rule, "affect." + value.Key,
                    value.Value, seed.RootEventIds));
            foreach (var value in seed.RelationshipEvidenceBps)
                why.Add(new GroundedCharacterWhyContribution(
                    GroundedWhySource.CurrentEvent, rule, "relationship." + value.Key,
                    value.Value, seed.RootEventIds));
            foreach (var item in packet.Items)
                foreach (var value in item.AllocatedDimensionsBps)
                    why.Add(new GroundedCharacterWhyContribution(
                        GroundedWhySource.HistoricalContext,
                        item.ReasonCodes.Count == 0 ? "HISTORY_CONTEXT" : item.ReasonCodes[0],
                        value.Dimension, value.ValueBps, item.RootEventIds));
            why.Add(new GroundedCharacterWhyContribution(
                GroundedWhySource.Policy,
                "APPRAISAL_REGIME_" + GroundedCompoundAppraisalSource.RegimeToken(regime),
                "policy.context_weight", historyCap, Array.Empty<string>()));
            why = why.OrderBy(value => GroundedCompoundAppraisalSource.WhySourceToken(value.Source), StringComparer.Ordinal)
                .ThenBy(value => value.Subject, StringComparer.Ordinal)
                .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                .ThenBy(value => string.Join("\n", value.RootEventIds), StringComparer.Ordinal).ToList();
            if (why.Count > GroundedCompoundAppraisalSource.MaximumWhyContributions)
            {
                why = why.OrderByDescending(value => Math.Abs(value.SignedBps))
                    .ThenBy(value => GroundedCompoundAppraisalSource.WhySourceToken(value.Source), StringComparer.Ordinal)
                    .ThenBy(value => value.Subject, StringComparer.Ordinal)
                    .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                    .Take(GroundedCompoundAppraisalSource.MaximumWhyContributions)
                    .OrderBy(value => GroundedCompoundAppraisalSource.WhySourceToken(value.Source), StringComparer.Ordinal)
                    .ThenBy(value => value.Subject, StringComparer.Ordinal)
                    .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                    .ThenBy(value => string.Join("\n", value.RootEventIds), StringComparer.Ordinal).ToList();
                uncertainty.Add("WHY_TRACE_TRUNCATED");
            }
            return new ReadOnlyCollection<GroundedCharacterWhyContribution>(why);
        }

        private static GroundedAppraisalFamily ParseFamily(string token)
        {
            foreach (GroundedAppraisalFamily family in Enum.GetValues(typeof(GroundedAppraisalFamily)))
                if (string.Equals(GroundedCompoundAppraisalSource.FamilyToken(family), token, StringComparison.OrdinalIgnoreCase))
                    return family;
            throw new ArgumentException("Unknown appraisal family token.", nameof(token));
        }

        private sealed class Allocation
        {
            internal Allocation(string name, int amount, long remainder, int sign)
            {
                Name = name;
                Amount = amount;
                Remainder = remainder;
                Sign = sign;
            }

            internal string Name { get; }
            internal int Amount { get; set; }
            internal long Remainder { get; }
            internal int Sign { get; }
        }
    }
}
