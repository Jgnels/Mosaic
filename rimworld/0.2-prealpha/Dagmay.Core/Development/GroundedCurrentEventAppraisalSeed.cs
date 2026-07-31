using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Dagmay.Core.Development
{
    public sealed class GroundedCurrentEventAppraisalSeed
    {
        private GroundedCurrentEventAppraisalSeed(
            string ownerId,
            string lineageId,
            string? counterpartId,
            string eventId,
            IEnumerable<string> rootEventIds,
            long eventTick,
            string eventHash,
            string saveId,
            string worldId,
            string storeSetId,
            long checkpointGeneration,
            string checkpointFingerprint,
            string privacy,
            bool admitted,
            bool witnessed,
            bool relationshipRelevant,
            IEnumerable<KeyValuePair<GroundedAppraisalFamily, int>> familyEvidenceBps,
            IEnumerable<KeyValuePair<string, int>> affectEvidenceBps,
            IEnumerable<KeyValuePair<string, int>> relationshipEvidenceBps,
            IEnumerable<string> ruleIds,
            string fingerprint,
            bool verifyFingerprint)
        {
            OwnerId = RetrievalCanonical.Text(ownerId, nameof(ownerId));
            LineageId = RetrievalCanonical.Text(lineageId, nameof(lineageId));
            CounterpartId = RetrievalCanonical.OptionalText(counterpartId, nameof(counterpartId));
            EventId = RetrievalCanonical.Text(eventId, nameof(eventId));
            if (EventId.Any(char.IsControl)) throw new ArgumentException("Control characters are not eligible.", nameof(eventId));
            RootEventIds = RetrievalCanonical.TextSet(rootEventIds, nameof(rootEventIds), 8);
            if (!RootEventIds.Contains(EventId, StringComparer.Ordinal) ||
                RootEventIds.Any(value =>
                    value.StartsWith("developmental-record:", StringComparison.Ordinal) ||
                    value.StartsWith("context-packet:", StringComparison.Ordinal) ||
                    value.StartsWith("appraisal-proposal:", StringComparison.Ordinal)))
                throw new ArgumentException("Current-event root closure is invalid.", nameof(rootEventIds));
            if (eventTick < 0 || checkpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(eventTick));
            EventTick = eventTick;
            EventHash = RetrievalCanonical.Hex(eventHash, nameof(eventHash));
            SaveId = RetrievalCanonical.Text(saveId, nameof(saveId));
            WorldId = RetrievalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = RetrievalCanonical.Text(storeSetId, nameof(storeSetId));
            CheckpointGeneration = checkpointGeneration;
            CheckpointFingerprint = RetrievalCanonical.Hex(checkpointFingerprint, nameof(checkpointFingerprint));
            Privacy = RetrievalCanonical.Token(privacy, nameof(privacy));
            if (!string.Equals(Privacy, "OWNER_PRIVATE", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Privacy, "PUBLIC", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Player-only or unknown privacy is not eligible.", nameof(privacy));
            if (!admitted || !witnessed)
                throw new ArgumentException("Current event must be admitted and directly witnessed.");
            Admitted = admitted;
            Witnessed = witnessed;
            RelationshipRelevant = relationshipRelevant;

            FamilyEvidenceBps = new ReadOnlyCollection<KeyValuePair<GroundedAppraisalFamily, int>>(
                (familyEvidenceBps ?? throw new ArgumentNullException(nameof(familyEvidenceBps)))
                    .OrderBy(value => GroundedCompoundAppraisalSource.FamilyToken(value.Key), StringComparer.Ordinal)
                    .ToList());
            if (FamilyEvidenceBps.Count > 8 ||
                FamilyEvidenceBps.Select(value => value.Key).Distinct().Count() != FamilyEvidenceBps.Count ||
                FamilyEvidenceBps.Any(value => value.Value <= 0 || value.Value > GroundedCompoundAppraisalSource.MaximumBps))
                throw new ArgumentException("Family evidence must be unique positive support.", nameof(familyEvidenceBps));

            AffectEvidenceBps = CanonicalDimensions(affectEvidenceBps, 8, nameof(affectEvidenceBps));
            RelationshipEvidenceBps = CanonicalDimensions(relationshipEvidenceBps, 6, nameof(relationshipEvidenceBps));
            if ((CounterpartId is null || !RelationshipRelevant) && RelationshipEvidenceBps.Count > 0)
                throw new ArgumentException("Relationship evidence requires an explicit relevant counterpart.");
            if (FamilyEvidenceBps.Count == 0 && AffectEvidenceBps.Count == 0 && RelationshipEvidenceBps.Count == 0)
                throw new ArgumentException("Current event has no grounded appraisal evidence.");

            RuleIds = RetrievalCanonical.TokenSet(
                ruleIds ?? throw new ArgumentNullException(nameof(ruleIds)), nameof(ruleIds), 16);
            if (RuleIds.Count == 0) throw new ArgumentException("At least one stable rule ID is required.", nameof(ruleIds));
            SourceContract = GroundedCompoundAppraisalSource.CurrentSeedContract;
            Authority = GroundedCompoundAppraisalSource.CurrentSeedAuthority;
            Fingerprint = RetrievalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (verifyFingerprint && !VerifyFingerprint())
                throw new ArgumentException("Current-event seed fingerprint mismatch.", nameof(fingerprint));
        }

        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string EventId { get; }
        public IReadOnlyList<string> RootEventIds { get; }
        public long EventTick { get; }
        public string EventHash { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public string Privacy { get; }
        public bool Admitted { get; }
        public bool Witnessed { get; }
        public bool RelationshipRelevant { get; }
        public IReadOnlyList<KeyValuePair<GroundedAppraisalFamily, int>> FamilyEvidenceBps { get; }
        public IReadOnlyList<KeyValuePair<string, int>> AffectEvidenceBps { get; }
        public IReadOnlyList<KeyValuePair<string, int>> RelationshipEvidenceBps { get; }
        public IReadOnlyList<string> RuleIds { get; }
        public string SourceContract { get; }
        public string Authority { get; }
        public string Fingerprint { get; }

        public static GroundedCurrentEventAppraisalSeed Create(
            string ownerId,
            string lineageId,
            string? counterpartId,
            string eventId,
            IEnumerable<string> rootEventIds,
            long eventTick,
            string eventHash,
            string saveId,
            string worldId,
            string storeSetId,
            long checkpointGeneration,
            string checkpointFingerprint,
            string privacy,
            bool admitted,
            bool witnessed,
            bool relationshipRelevant,
            IEnumerable<KeyValuePair<GroundedAppraisalFamily, int>> familyEvidenceBps,
            IEnumerable<KeyValuePair<string, int>> affectEvidenceBps,
            IEnumerable<KeyValuePair<string, int>> relationshipEvidenceBps,
            IEnumerable<string> ruleIds)
        {
            var family = (familyEvidenceBps ?? throw new ArgumentNullException(nameof(familyEvidenceBps))).ToArray();
            var affect = (affectEvidenceBps ?? throw new ArgumentNullException(nameof(affectEvidenceBps))).ToArray();
            var relationship = (relationshipEvidenceBps ?? throw new ArgumentNullException(nameof(relationshipEvidenceBps))).ToArray();
            var rules = (ruleIds ?? throw new ArgumentNullException(nameof(ruleIds))).ToArray();
            var roots = (rootEventIds ?? throw new ArgumentNullException(nameof(rootEventIds))).ToArray();
            var provisional = new GroundedCurrentEventAppraisalSeed(
                ownerId, lineageId, counterpartId, eventId, roots, eventTick, eventHash,
                saveId, worldId, storeSetId, checkpointGeneration, checkpointFingerprint,
                privacy, admitted, witnessed, relationshipRelevant, family, affect, relationship,
                rules, new string('0', 64), false);
            var fingerprint = GroundedJson.Hash(provisional.DeterministicJson(false));
            return new GroundedCurrentEventAppraisalSeed(
                ownerId, lineageId, counterpartId, eventId, roots, eventTick, eventHash,
                saveId, worldId, storeSetId, checkpointGeneration, checkpointFingerprint,
                privacy, admitted, witnessed, relationshipRelevant, family, affect, relationship,
                rules, fingerprint, true);
        }

        public string ToDeterministicJson() => DeterministicJson(true);

        public bool VerifyFingerprint() =>
            string.Equals(Fingerprint, GroundedJson.Hash(DeterministicJson(false)), StringComparison.Ordinal);

        public static GroundedCurrentEventAppraisalSeed RequireValid(GroundedCurrentEventAppraisalSeed seed)
        {
            if (seed is null) throw new ArgumentNullException(nameof(seed));
            if (!string.Equals(seed.SourceContract, GroundedCompoundAppraisalSource.CurrentSeedContract, StringComparison.Ordinal) ||
                !string.Equals(seed.Authority, GroundedCompoundAppraisalSource.CurrentSeedAuthority, StringComparison.Ordinal) ||
                !seed.VerifyFingerprint())
                throw new ArgumentException("Current-event appraisal seed failed public verification.", nameof(seed));
            return seed;
        }

        internal string DeterministicJson(bool includeFingerprint)
        {
            var fields = new List<string>
            {
                "\"admitted\":" + (Admitted ? "true" : "false"),
                "\"affect_evidence_bps\":" + GroundedJson.IntPairs(AffectEvidenceBps),
                "\"authority\":" + GroundedJson.String(Authority),
                "\"checkpoint_fingerprint\":" + GroundedJson.String(CheckpointFingerprint),
                "\"checkpoint_generation\":" + GroundedJson.Number(CheckpointGeneration),
                "\"counterpart_id\":" + GroundedJson.OptionalString(CounterpartId),
                "\"event_hash\":" + GroundedJson.String(EventHash),
                "\"event_id\":" + GroundedJson.String(EventId),
                "\"event_tick\":" + GroundedJson.Number(EventTick),
                "\"family_evidence_bps\":" + FamilyPairs(FamilyEvidenceBps)
            };
            if (includeFingerprint) fields.Add("\"fingerprint\":" + GroundedJson.String(Fingerprint));
            fields.Add("\"lineage_id\":" + GroundedJson.String(LineageId));
            fields.Add("\"owner_id\":" + GroundedJson.String(OwnerId));
            fields.Add("\"privacy\":" + GroundedJson.String(Privacy));
            fields.Add("\"relationship_evidence_bps\":" + GroundedJson.IntPairs(RelationshipEvidenceBps));
            fields.Add("\"relationship_relevant\":" + (RelationshipRelevant ? "true" : "false"));
            fields.Add("\"root_event_ids\":" + GroundedJson.Strings(RootEventIds));
            fields.Add("\"rule_ids\":" + GroundedJson.Strings(RuleIds));
            fields.Add("\"save_id\":" + GroundedJson.String(SaveId));
            fields.Add("\"source_contract\":" + GroundedJson.String(SourceContract));
            fields.Add("\"store_set_id\":" + GroundedJson.String(StoreSetId));
            fields.Add("\"world_id\":" + GroundedJson.String(WorldId));
            fields.Add("\"witnessed\":" + (Witnessed ? "true" : "false"));
            return "{" + string.Join(",", fields) + "}";
        }

        private static IReadOnlyList<KeyValuePair<string, int>> CanonicalDimensions(
            IEnumerable<KeyValuePair<string, int>> values,
            int maximum,
            string parameterName)
        {
            var result = (values ?? throw new ArgumentNullException(parameterName))
                .Select(value => new KeyValuePair<string, int>(
                    RetrievalCanonical.Token(value.Key, parameterName), value.Value))
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToList();
            if (result.Count > maximum ||
                result.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count() != result.Count ||
                result.Any(value => value.Value == 0 || Math.Abs(value.Value) > GroundedCompoundAppraisalSource.MaximumBps))
                throw new ArgumentException("Dimension evidence is invalid.", parameterName);
            return new ReadOnlyCollection<KeyValuePair<string, int>>(result);
        }

        private static string FamilyPairs(IEnumerable<KeyValuePair<GroundedAppraisalFamily, int>> values) =>
            "[" + string.Join(",", values.Select(value =>
                "[" + GroundedJson.String(GroundedCompoundAppraisalSource.FamilyToken(value.Key)) + "," +
                GroundedJson.Number(value.Value) + "]")) + "]";
    }

    public sealed class ContextualAppraisalRequest
    {
        public ContextualAppraisalRequest(
            GroundedCurrentEventAppraisalSeed currentSeed,
            GroundedDevelopmentalContextRequest materializationRequest,
            GroundedDevelopmentalContextPacket contextPacket)
        {
            CurrentSeed = GroundedCurrentEventAppraisalSeed.RequireValid(currentSeed);
            MaterializationRequest = materializationRequest ?? throw new ArgumentNullException(nameof(materializationRequest));
            ContextPacket = GroundedDevelopmentalContextPacket.RequireValid(contextPacket);
            var query = MaterializationRequest.RetrievalQuery;
            if (query.Purpose != DevelopmentalContextPurpose.Appraisal)
                throw new ArgumentException("Context packet is not authorized for appraisal.", nameof(materializationRequest));
            if (!string.Equals(ContextPacket.RequestFingerprint, MaterializationRequest.Fingerprint, StringComparison.Ordinal) ||
                !string.Equals(ContextPacket.V41BundleFingerprint, MaterializationRequest.Bundle.Fingerprint, StringComparison.Ordinal))
                throw new ArgumentException("V42 request or bundle binding mismatch.", nameof(contextPacket));
            if (!string.Equals(CurrentSeed.OwnerId, ContextPacket.OwnerId, StringComparison.Ordinal) ||
                !string.Equals(CurrentSeed.LineageId, ContextPacket.LineageId, StringComparison.Ordinal) ||
                !string.Equals(CurrentSeed.CounterpartId, ContextPacket.CounterpartId, StringComparison.Ordinal))
                throw new ArgumentException("Current-event and context identity mismatch.");
            if (!string.Equals(CurrentSeed.SaveId, query.SaveId, StringComparison.Ordinal) ||
                !string.Equals(CurrentSeed.WorldId, query.WorldId, StringComparison.Ordinal) ||
                !string.Equals(CurrentSeed.StoreSetId, query.StoreSetId, StringComparison.Ordinal))
                throw new ArgumentException("Current-event and context store binding mismatch.");
            if (CurrentSeed.CheckpointGeneration != query.CheckpointGeneration ||
                !query.CheckpointAncestry.Contains(CurrentSeed.CheckpointFingerprint, StringComparer.Ordinal))
                throw new ArgumentException("Current-event checkpoint is not in the verified ancestry.");
            if (CurrentSeed.EventTick != MaterializationRequest.CurrentEventTick ||
                !CurrentSeed.RootEventIds.SequenceEqual(MaterializationRequest.CurrentEventRootIds, StringComparer.Ordinal))
                throw new ArgumentException("Current event does not match the materialization request.");
            var roots = new HashSet<string>(CurrentSeed.RootEventIds, StringComparer.Ordinal);
            foreach (var item in ContextPacket.Items)
            {
                if (item.EventTick > CurrentSeed.EventTick)
                    throw new ArgumentException("Future history is not eligible for appraisal.", nameof(contextPacket));
                if (item.RootEventIds.Any(roots.Contains))
                    throw new ArgumentException("Current event cannot be reused as historical evidence.", nameof(contextPacket));
            }
            Fingerprint = GroundedJson.Hash(
                "{" +
                "\"context_packet\":" + GroundedJson.String(ContextPacket.Fingerprint) + "," +
                "\"current_seed\":" + GroundedJson.String(CurrentSeed.Fingerprint) + "," +
                "\"materialization_request\":" + GroundedJson.String(MaterializationRequest.Fingerprint) +
                "}");
        }

        public GroundedCurrentEventAppraisalSeed CurrentSeed { get; }
        public GroundedDevelopmentalContextRequest MaterializationRequest { get; }
        public GroundedDevelopmentalContextPacket ContextPacket { get; }
        public string Fingerprint { get; }
    }
}
