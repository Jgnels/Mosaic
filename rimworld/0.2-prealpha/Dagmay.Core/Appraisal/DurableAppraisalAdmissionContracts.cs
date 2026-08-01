using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Dagmay.Core.Appraisal
{
    public static class DurableAppraisalAdmissionSource
    {
        public const string Contract = "mosaic.0.3e.checkpoint-aligned-durable-appraisal-admission.v1";
        public const string GateDigest = "639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9";
        public const string V39Contract = "Mosaic.Core.ProvisionalDialogueAppraisal.v1";
        public const string V39CorrectedGateDigest = "c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819";
        public const string EventReceiptContract = "Mosaic.Core.CheckpointAlignedDialogueAdmission.v1";
        public const string CheckpointReceiptContract = "Mosaic.Core.CheckpointCommitReceipt.v1";
        public const string NoPawnAuthority = "INTERNAL_CANONICAL_COORDINATOR_NO_PROVIDER_UI_PLANNER_ADAPTER_OR_PAWN_AUTHORITY";
    }

    internal static class DurableAppraisalCanonical
    {
        internal static decimal Q(decimal value) =>
            decimal.Round(value, 6, MidpointRounding.ToEven);

        internal static string DecimalText(decimal value) =>
            Q(value).ToString("0.000000", CultureInfo.InvariantCulture);

        internal static string Text(string value, string name, int maximum = 160)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(value => value < 32))
                throw new ArgumentException("Invalid " + name + ".", name);
            return value;
        }

        internal static string Hex(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(value => !Uri.IsHexDigit(value)))
                throw new ArgumentException("Invalid " + name + ".", name);
            return value.ToLowerInvariant();
        }

        internal static string Hash(IEnumerable<KeyValuePair<string, string>> fields) =>
            ProvisionalAppraisalCanonical.HashFields(fields);

        internal static KeyValuePair<string, string> Pair(string key, string value) =>
            ProvisionalAppraisalCanonical.Pair(key, value);

        internal static IEnumerable<KeyValuePair<string, string>> AffectFields(string prefix, ProvisionalAffectDelta delta) =>
            delta.Fields(prefix);

        internal static IEnumerable<KeyValuePair<string, string>> RelationshipFields(string prefix, ProvisionalRelationshipDelta delta) =>
            delta.Fields(prefix);

        internal static bool AffectEqual(ProvisionalAffectDelta left, ProvisionalAffectDelta right) =>
            left.Fields("v").SequenceEqual(right.Fields("v"));

        internal static bool RelationshipEqual(ProvisionalRelationshipDelta left, ProvisionalRelationshipDelta right) =>
            left.Fields("v").SequenceEqual(right.Fields("v"));

        internal static byte[] HexBytes(string value)
        {
            value = Hex(value, nameof(value));
            var bytes = new byte[32];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }
    }

    public enum DurableAppraisalPrivacy
    {
        OwnerPrivate = 0
    }

    public enum DurableAppraisalOutboxState
    {
        Pending = 0,
        Completed = 1,
        Quarantined = 2
    }

    public enum DurableAdmissionAttemptOutcome
    {
        Prepared = 0,
        RetryableFailure = 1,
        PermanentFailure = 2
    }

    public enum DurableAdmissionFaultStage
    {
        AfterOutbox = 0,
        AfterDevelopmentalRecord = 1,
        AfterAffect = 2,
        AfterRelationship = 3,
        BeforeCompletion = 4,
        AfterCompletion = 5
    }

    public sealed class DurableAdmissionSimulatedCrash : Exception
    {
        public DurableAdmissionSimulatedCrash(DurableAdmissionFaultStage stage)
            : base(stage.ToString())
        {
            Stage = stage;
        }

        public DurableAdmissionFaultStage Stage { get; }
    }

    public sealed class DurableIdentityBinding
    {
        private DurableIdentityBinding(
            string individualId,
            string lineageId,
            string saveId,
            string worldId,
            string storeSetId,
            long archiveGeneration,
            string fingerprint)
        {
            IndividualId = DurableAppraisalCanonical.Text(individualId, nameof(individualId));
            LineageId = DurableAppraisalCanonical.Text(lineageId, nameof(lineageId));
            SaveId = DurableAppraisalCanonical.Text(saveId, nameof(saveId));
            WorldId = DurableAppraisalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = DurableAppraisalCanonical.Text(storeSetId, nameof(storeSetId));
            if (archiveGeneration < 0) throw new ArgumentOutOfRangeException(nameof(archiveGeneration));
            ArchiveGeneration = archiveGeneration;
            Fingerprint = DurableAppraisalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (!string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Identity binding fingerprint mismatch.", nameof(fingerprint));
        }

        public static DurableIdentityBinding Create(
            string individualId,
            string lineageId,
            string saveId,
            string worldId,
            string storeSetId,
            long archiveGeneration)
        {
            var fields = Fields(individualId, lineageId, saveId, worldId, storeSetId, archiveGeneration);
            return new DurableIdentityBinding(
                individualId, lineageId, saveId, worldId, storeSetId, archiveGeneration,
                DurableAppraisalCanonical.Hash(fields));
        }

        internal static DurableIdentityBinding Restore(
            string individualId,
            string lineageId,
            string saveId,
            string worldId,
            string storeSetId,
            long archiveGeneration,
            string fingerprint) =>
            new DurableIdentityBinding(individualId, lineageId, saveId, worldId, storeSetId, archiveGeneration, fingerprint);

        public string IndividualId { get; }
        public string LineageId { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public long ArchiveGeneration { get; }
        public string Fingerprint { get; }

        internal string ComputeFingerprint() =>
            DurableAppraisalCanonical.Hash(Fields(
                IndividualId, LineageId, SaveId, WorldId, StoreSetId, ArchiveGeneration));

        private static IEnumerable<KeyValuePair<string, string>> Fields(
            string individualId, string lineageId, string saveId, string worldId, string storeSetId, long generation)
        {
            yield return DurableAppraisalCanonical.Pair("individual_id", individualId);
            yield return DurableAppraisalCanonical.Pair("lineage_id", lineageId);
            yield return DurableAppraisalCanonical.Pair("save_id", saveId);
            yield return DurableAppraisalCanonical.Pair("world_id", worldId);
            yield return DurableAppraisalCanonical.Pair("store_set_id", storeSetId);
            yield return DurableAppraisalCanonical.Pair("archive_generation", generation.ToString(CultureInfo.InvariantCulture));
        }
    }

    public sealed class AdmittedDialogueEventReceipt
    {
        private AdmittedDialogueEventReceipt(
            string receiptId,
            string eventId,
            string eventHash,
            string ownerId,
            string speakerId,
            IEnumerable<string> audienceIds,
            long eventTick,
            long checkpointGeneration,
            string saveId,
            string worldId,
            string storeSetId,
            DurableAppraisalPrivacy privacy,
            string sourceContract)
        {
            ReceiptId = DurableAppraisalCanonical.Text(receiptId, nameof(receiptId));
            EventId = DurableAppraisalCanonical.Text(eventId, nameof(eventId));
            EventHash = DurableAppraisalCanonical.Hex(eventHash, nameof(eventHash));
            OwnerId = DurableAppraisalCanonical.Text(ownerId, nameof(ownerId));
            SpeakerId = DurableAppraisalCanonical.Text(speakerId, nameof(speakerId));
            AudienceIds = new ReadOnlyCollection<string>((audienceIds ?? throw new ArgumentNullException(nameof(audienceIds)))
                .Select(value => DurableAppraisalCanonical.Text(value, nameof(audienceIds))).OrderBy(value => value, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToList());
            if (!AudienceIds.Contains(OwnerId, StringComparer.Ordinal))
                throw new ArgumentException("The owner was not an actual dialogue-event witness.", nameof(audienceIds));
            if (eventTick < 0 || checkpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(eventTick));
            EventTick = eventTick;
            CheckpointGeneration = checkpointGeneration;
            SaveId = DurableAppraisalCanonical.Text(saveId, nameof(saveId));
            WorldId = DurableAppraisalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = DurableAppraisalCanonical.Text(storeSetId, nameof(storeSetId));
            Privacy = privacy;
            SourceContract = DurableAppraisalCanonical.Text(sourceContract, nameof(sourceContract));
            if (!string.Equals(SourceContract, DurableAppraisalAdmissionSource.EventReceiptContract, StringComparison.Ordinal))
                throw new ArgumentException("Foreign dialogue-event receipt.", nameof(sourceContract));
            Fingerprint = ComputeFingerprint();
        }

        internal static AdmittedDialogueEventReceipt CreateTrusted(
            string receiptId,
            string eventId,
            string eventHash,
            string ownerId,
            string speakerId,
            IEnumerable<string> audienceIds,
            long eventTick,
            long checkpointGeneration,
            string saveId,
            string worldId,
            string storeSetId,
            DurableAppraisalPrivacy privacy,
            string sourceContract) =>
            new AdmittedDialogueEventReceipt(
                receiptId, eventId, eventHash, ownerId, speakerId, audienceIds, eventTick,
                checkpointGeneration, saveId, worldId, storeSetId, privacy, sourceContract);

        public string ReceiptId { get; }
        public string EventId { get; }
        public string EventHash { get; }
        public string OwnerId { get; }
        public string SpeakerId { get; }
        public IReadOnlyList<string> AudienceIds { get; }
        public long EventTick { get; }
        public long CheckpointGeneration { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public DurableAppraisalPrivacy Privacy { get; }
        public string SourceContract { get; }
        public string Fingerprint { get; private set; }

        internal string ComputeFingerprint()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("receipt_id", ReceiptId),
                DurableAppraisalCanonical.Pair("event_id", EventId),
                DurableAppraisalCanonical.Pair("event_hash", EventHash),
                DurableAppraisalCanonical.Pair("owner_id", OwnerId),
                DurableAppraisalCanonical.Pair("speaker_id", SpeakerId),
                DurableAppraisalCanonical.Pair("audience_ids", string.Join(",", AudienceIds)),
                DurableAppraisalCanonical.Pair("event_tick", EventTick.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("save_id", SaveId),
                DurableAppraisalCanonical.Pair("world_id", WorldId),
                DurableAppraisalCanonical.Pair("store_set_id", StoreSetId),
                DurableAppraisalCanonical.Pair("privacy", Privacy.ToString()),
                DurableAppraisalCanonical.Pair("source_contract", SourceContract)
            };
            return DurableAppraisalCanonical.Hash(fields);
        }
    }

    public sealed class CheckpointCommitReceipt
    {
        private CheckpointCommitReceipt(
            string receiptId,
            string saveId,
            string worldId,
            string storeSetId,
            long checkpointGeneration,
            string checkpointHash,
            bool completed,
            bool writable,
            bool storesHealthy,
            string sourceContract)
        {
            ReceiptId = DurableAppraisalCanonical.Text(receiptId, nameof(receiptId));
            SaveId = DurableAppraisalCanonical.Text(saveId, nameof(saveId));
            WorldId = DurableAppraisalCanonical.Text(worldId, nameof(worldId));
            StoreSetId = DurableAppraisalCanonical.Text(storeSetId, nameof(storeSetId));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            CheckpointHash = DurableAppraisalCanonical.Hex(checkpointHash, nameof(checkpointHash));
            Completed = completed;
            Writable = writable;
            StoresHealthy = storesHealthy;
            SourceContract = DurableAppraisalCanonical.Text(sourceContract, nameof(sourceContract));
            if (!string.Equals(SourceContract, DurableAppraisalAdmissionSource.CheckpointReceiptContract, StringComparison.Ordinal))
                throw new ArgumentException("Foreign checkpoint receipt.", nameof(sourceContract));
            Fingerprint = ComputeFingerprint();
        }

        internal static CheckpointCommitReceipt CreateTrusted(
            string receiptId,
            string saveId,
            string worldId,
            string storeSetId,
            long checkpointGeneration,
            string checkpointHash,
            bool completed,
            bool writable,
            bool storesHealthy,
            string sourceContract) =>
            new CheckpointCommitReceipt(
                receiptId, saveId, worldId, storeSetId, checkpointGeneration, checkpointHash,
                completed, writable, storesHealthy, sourceContract);

        public string ReceiptId { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointHash { get; }
        public bool Completed { get; }
        public bool Writable { get; }
        public bool StoresHealthy { get; }
        public string SourceContract { get; }
        public string Fingerprint { get; private set; }

        internal string ComputeFingerprint() =>
            DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("receipt_id", ReceiptId),
                DurableAppraisalCanonical.Pair("save_id", SaveId),
                DurableAppraisalCanonical.Pair("world_id", WorldId),
                DurableAppraisalCanonical.Pair("store_set_id", StoreSetId),
                DurableAppraisalCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("checkpoint_hash", CheckpointHash),
                DurableAppraisalCanonical.Pair("completed", Completed ? "true" : "false"),
                DurableAppraisalCanonical.Pair("writable", Writable ? "true" : "false"),
                DurableAppraisalCanonical.Pair("stores_healthy", StoresHealthy ? "true" : "false"),
                DurableAppraisalCanonical.Pair("source_contract", SourceContract)
            });
    }

    public sealed class CanonicalAffectState
    {
        private CanonicalAffectState(string ownerId, ProvisionalAffectDelta values, long version, string fingerprint)
        {
            OwnerId = DurableAppraisalCanonical.Text(ownerId, nameof(ownerId));
            Values = values ?? throw new ArgumentNullException(nameof(values));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            Fingerprint = DurableAppraisalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (!string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Canonical affect fingerprint mismatch.", nameof(fingerprint));
        }

        public static CanonicalAffectState Create(string ownerId, ProvisionalAffectDelta values, long version)
        {
            var fingerprint = Compute(ownerId, values, version);
            return new CanonicalAffectState(ownerId, values, version, fingerprint);
        }

        internal static CanonicalAffectState Restore(string ownerId, ProvisionalAffectDelta values, long version, string fingerprint) =>
            new CanonicalAffectState(ownerId, values, version, fingerprint);

        public string OwnerId { get; }
        public ProvisionalAffectDelta Values { get; }
        public long Version { get; }
        public string Fingerprint { get; }

        internal CanonicalAffectState Apply(ProvisionalAffectDelta delta, out ProvisionalAffectDelta actual)
        {
            var after = Values.Plus(delta).Clamp(1m);
            actual = new ProvisionalAffectDelta(
                after.Valence - Values.Valence, after.Arousal - Values.Arousal,
                after.Threat - Values.Threat, after.Agency - Values.Agency,
                after.Attachment - Values.Attachment, after.Certainty - Values.Certainty,
                after.SocialStanding - Values.SocialStanding);
            return Create(OwnerId, after, Version + (actual.IsZero ? 0 : 1));
        }

        internal bool Same(CanonicalAffectState other) =>
            other is not null && string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal) &&
            Version == other.Version && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal) &&
            DurableAppraisalCanonical.AffectEqual(Values, other.Values);

        private string ComputeFingerprint() => Compute(OwnerId, Values, Version);

        private static string Compute(string ownerId, ProvisionalAffectDelta values, long version)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("schema", "mosaic.affect-state.v1"),
                DurableAppraisalCanonical.Pair("owner", ownerId),
                DurableAppraisalCanonical.Pair("version", version.ToString(CultureInfo.InvariantCulture))
            };
            fields.AddRange(DurableAppraisalCanonical.AffectFields("values", values));
            return DurableAppraisalCanonical.Hash(fields);
        }
    }

    public sealed class CanonicalRelationshipVector
    {
        public CanonicalRelationshipVector(decimal trust = 0m, decimal affection = 0m, decimal fear = 0m, decimal resentment = 0m)
        {
            Trust = DurableAppraisalCanonical.Q(trust);
            Affection = DurableAppraisalCanonical.Q(affection);
            Fear = DurableAppraisalCanonical.Q(fear);
            Resentment = DurableAppraisalCanonical.Q(resentment);
            if (Trust < -1m || Trust > 1m || Affection < -1m || Affection > 1m ||
                Fear < 0m || Fear > 1m || Resentment < 0m || Resentment > 1m)
                throw new ArgumentOutOfRangeException(nameof(trust), "Relationship state is outside its bounds.");
        }

        public decimal Trust { get; }
        public decimal Affection { get; }
        public decimal Fear { get; }
        public decimal Resentment { get; }

        internal CanonicalRelationshipVector Apply(ProvisionalRelationshipDelta delta, out ProvisionalRelationshipDelta actual)
        {
            var after = new CanonicalRelationshipVector(
                Clamp(Trust + delta.Trust, -1m, 1m),
                Clamp(Affection + delta.Affection, -1m, 1m),
                Clamp(Fear + delta.Fear, 0m, 1m),
                Clamp(Resentment + delta.Resentment, 0m, 1m));
            actual = new ProvisionalRelationshipDelta(
                after.Trust - Trust, after.Affection - Affection,
                after.Fear - Fear, after.Resentment - Resentment);
            return after;
        }

        internal IEnumerable<KeyValuePair<string, string>> Fields(string prefix)
        {
            yield return DurableAppraisalCanonical.Pair(prefix + ".trust", DurableAppraisalCanonical.DecimalText(Trust));
            yield return DurableAppraisalCanonical.Pair(prefix + ".affection", DurableAppraisalCanonical.DecimalText(Affection));
            yield return DurableAppraisalCanonical.Pair(prefix + ".fear", DurableAppraisalCanonical.DecimalText(Fear));
            yield return DurableAppraisalCanonical.Pair(prefix + ".resentment", DurableAppraisalCanonical.DecimalText(Resentment));
        }

        internal bool Same(CanonicalRelationshipVector other) =>
            other is not null && Trust == other.Trust && Affection == other.Affection &&
            Fear == other.Fear && Resentment == other.Resentment;

        private static decimal Clamp(decimal value, decimal low, decimal high) =>
            DurableAppraisalCanonical.Q(Math.Max(low, Math.Min(high, value)));
    }

    public sealed class CanonicalRelationshipState
    {
        private CanonicalRelationshipState(
            string ownerId, string counterpartId, CanonicalRelationshipVector values, long version, string fingerprint)
        {
            OwnerId = DurableAppraisalCanonical.Text(ownerId, nameof(ownerId));
            CounterpartId = DurableAppraisalCanonical.Text(counterpartId, nameof(counterpartId));
            Values = values ?? throw new ArgumentNullException(nameof(values));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            Fingerprint = DurableAppraisalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (!string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Canonical relationship fingerprint mismatch.", nameof(fingerprint));
        }

        public static CanonicalRelationshipState Create(
            string ownerId, string counterpartId, CanonicalRelationshipVector values, long version) =>
            new CanonicalRelationshipState(ownerId, counterpartId, values, version, Compute(ownerId, counterpartId, values, version));

        internal static CanonicalRelationshipState Restore(
            string ownerId, string counterpartId, CanonicalRelationshipVector values, long version, string fingerprint) =>
            new CanonicalRelationshipState(ownerId, counterpartId, values, version, fingerprint);

        public string OwnerId { get; }
        public string CounterpartId { get; }
        public CanonicalRelationshipVector Values { get; }
        public long Version { get; }
        public string Fingerprint { get; }

        internal CanonicalRelationshipState Apply(ProvisionalRelationshipDelta delta, out ProvisionalRelationshipDelta actual)
        {
            var after = Values.Apply(delta, out actual);
            return Create(OwnerId, CounterpartId, after, Version + (actual.IsZero ? 0 : 1));
        }

        internal bool Same(CanonicalRelationshipState other) =>
            other is not null && string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal) &&
            string.Equals(CounterpartId, other.CounterpartId, StringComparison.Ordinal) &&
            Version == other.Version && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal) &&
            Values.Same(other.Values);

        private string ComputeFingerprint() => Compute(OwnerId, CounterpartId, Values, Version);

        private static string Compute(string ownerId, string counterpartId, CanonicalRelationshipVector values, long version)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("schema", "mosaic.relationship-state.v1"),
                DurableAppraisalCanonical.Pair("owner", ownerId),
                DurableAppraisalCanonical.Pair("counterpart", counterpartId),
                DurableAppraisalCanonical.Pair("version", version.ToString(CultureInfo.InvariantCulture))
            };
            fields.AddRange(values.Fields("values"));
            return DurableAppraisalCanonical.Hash(fields);
        }
    }

    public sealed class DurableProposalEnvelope
    {
        internal DurableProposalEnvelope(
            DurableApplicationPacket packet,
            long createdTick,
            string speakerId,
            string conversationId,
            string sourceContract,
            string sourceGateDigest,
            string packetFingerprint,
            string registryAttestation,
            string fingerprint)
        {
            Packet = packet ?? throw new ArgumentNullException(nameof(packet));
            if (createdTick < 0) throw new ArgumentOutOfRangeException(nameof(createdTick));
            CreatedTick = createdTick;
            SpeakerId = DurableAppraisalCanonical.Text(speakerId, nameof(speakerId));
            ConversationId = DurableAppraisalCanonical.Text(conversationId, nameof(conversationId));
            SourceContract = DurableAppraisalCanonical.Text(sourceContract, nameof(sourceContract));
            SourceGateDigest = DurableAppraisalCanonical.Hex(sourceGateDigest, nameof(sourceGateDigest));
            PacketFingerprint = DurableAppraisalCanonical.Hex(packetFingerprint, nameof(packetFingerprint));
            RegistryAttestation = DurableAppraisalCanonical.Hex(registryAttestation, nameof(registryAttestation));
            Fingerprint = DurableAppraisalCanonical.Hex(fingerprint, nameof(fingerprint));
            if (!string.Equals(SourceContract, DurableAppraisalAdmissionSource.V39Contract, StringComparison.Ordinal) ||
                !string.Equals(SourceGateDigest, DurableAppraisalAdmissionSource.V39CorrectedGateDigest, StringComparison.Ordinal) ||
                !string.Equals(PacketFingerprint, DurableProposalRegistry.PacketFingerprint(Packet), StringComparison.Ordinal) ||
                !string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Foreign, obsolete, or forged v39 proposal envelope.");
        }

        public DurableApplicationPacket Packet { get; }
        public long CreatedTick { get; }
        public string SpeakerId { get; }
        public string ConversationId { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string PacketFingerprint { get; }
        public string RegistryAttestation { get; }
        public string Fingerprint { get; private set; }

        internal string ComputeFingerprint() =>
            DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("packet", PacketFingerprint),
                DurableAppraisalCanonical.Pair("created_tick", CreatedTick.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("speaker_id", SpeakerId),
                DurableAppraisalCanonical.Pair("conversation_id", ConversationId),
                DurableAppraisalCanonical.Pair("source_contract", SourceContract),
                DurableAppraisalCanonical.Pair("source_gate_digest", SourceGateDigest),
                DurableAppraisalCanonical.Pair("registry_attestation", RegistryAttestation)
            });
    }

    public sealed class DurableProposalRegistry
    {
        private readonly Dictionary<string, DurableProposalEnvelope> _envelopes =
            new Dictionary<string, DurableProposalEnvelope>(StringComparer.Ordinal);

        public DurableProposalEnvelope Register(
            ProvisionalDialogueAppraisalStore store,
            DurableApplicationPacket packet)
        {
            if (store is null) throw new ArgumentNullException(nameof(store));
            var appraisal = store.RequirePromotionPendingPacket(packet);
            var packetFingerprint = PacketFingerprint(packet);
            var attestation = DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("schema", "mosaic.0.3e.internal-v39-registry.v1"),
                DurableAppraisalCanonical.Pair("packet", packetFingerprint),
                DurableAppraisalCanonical.Pair("appraisal", appraisal.AppraisalId),
                DurableAppraisalCanonical.Pair("state", appraisal.State.ToString())
            });
            var fingerprint = DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("packet", packetFingerprint),
                DurableAppraisalCanonical.Pair("created_tick", appraisal.CreatedTick.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("speaker_id", appraisal.SpeakerId),
                DurableAppraisalCanonical.Pair("conversation_id", appraisal.ConversationId),
                DurableAppraisalCanonical.Pair("source_contract", DurableAppraisalAdmissionSource.V39Contract),
                DurableAppraisalCanonical.Pair("source_gate_digest", DurableAppraisalAdmissionSource.V39CorrectedGateDigest),
                DurableAppraisalCanonical.Pair("registry_attestation", attestation)
            });
            var envelope = new DurableProposalEnvelope(
                packet, appraisal.CreatedTick, appraisal.SpeakerId, appraisal.ConversationId,
                DurableAppraisalAdmissionSource.V39Contract,
                DurableAppraisalAdmissionSource.V39CorrectedGateDigest,
                packetFingerprint, attestation, fingerprint);
            _envelopes[packet.PacketId] = envelope;
            return envelope;
        }

        public int ActiveCount => _envelopes.Count;

        internal void Require(DurableProposalEnvelope envelope)
        {
            if (envelope is null ||
                !_envelopes.TryGetValue(envelope.Packet.PacketId, out var exact) ||
                !ReferenceEquals(exact, envelope) ||
                !string.Equals(envelope.Fingerprint, envelope.ComputeFingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Proposal envelope lacks trusted v39 registry attestation.", nameof(envelope));
        }

        internal void Consume(DurableProposalEnvelope envelope)
        {
            Require(envelope);
            _envelopes.Remove(envelope.Packet.PacketId);
        }

        internal static string PacketFingerprint(DurableApplicationPacket packet)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                DurableAppraisalCanonical.Pair("packet_id", packet.PacketId),
                DurableAppraisalCanonical.Pair("appraisal_id", packet.AppraisalId),
                DurableAppraisalCanonical.Pair("owner_id", packet.PerspectiveOwnerId),
                DurableAppraisalCanonical.Pair("event_id", packet.AdmittedDialogueEventId),
                DurableAppraisalCanonical.Pair("checkpoint_generation", packet.ExpectedCheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("affect_fingerprint", packet.ExpectedAffectFingerprint),
                DurableAppraisalCanonical.Pair("affect_version", packet.ExpectedAffectVersion.ToString(CultureInfo.InvariantCulture)),
                DurableAppraisalCanonical.Pair("relationship_fingerprint", packet.ExpectedRelationshipFingerprint ?? "null"),
                DurableAppraisalCanonical.Pair("relationship_version", packet.ExpectedRelationshipVersion?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                DurableAppraisalCanonical.Pair("source_receipt_id", packet.SourceReceiptId),
                DurableAppraisalCanonical.Pair("authority", DurableApplicationPacket.Authority)
            };
            fields.AddRange(DurableAppraisalCanonical.AffectFields("affect", packet.ProposedAffectDelta));
            fields.AddRange(DurableAppraisalCanonical.RelationshipFields("relationship", packet.ProposedRelationshipDelta));
            return DurableAppraisalCanonical.Hash(fields);
        }
    }

    public sealed class DurableAppraisalAdmissionRequest
    {
        public DurableAppraisalAdmissionRequest(
            DurableProposalEnvelope proposal,
            DurableIdentityBinding identity,
            AdmittedDialogueEventReceipt eventReceipt,
            CheckpointCommitReceipt checkpointReceipt)
        {
            Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            EventReceipt = eventReceipt ?? throw new ArgumentNullException(nameof(eventReceipt));
            CheckpointReceipt = checkpointReceipt ?? throw new ArgumentNullException(nameof(checkpointReceipt));
        }

        public DurableProposalEnvelope Proposal { get; }
        public DurableIdentityBinding Identity { get; }
        public AdmittedDialogueEventReceipt EventReceipt { get; }
        public CheckpointCommitReceipt CheckpointReceipt { get; }
    }

    public sealed class DurableAdmissionAttemptReceipt
    {
        internal DurableAdmissionAttemptReceipt(
            string entryId, string packetId, DurableAdmissionAttemptOutcome outcome,
            long checkpointGeneration, string fingerprint)
        {
            EntryId = DurableAppraisalCanonical.Hex(entryId, nameof(entryId));
            PacketId = DurableAppraisalCanonical.Hex(packetId, nameof(packetId));
            Outcome = outcome;
            CheckpointGeneration = checkpointGeneration;
            Fingerprint = DurableAppraisalCanonical.Hex(fingerprint, nameof(fingerprint));
        }

        internal static DurableAdmissionAttemptReceipt Create(
            string entryId, string packetId, DurableAdmissionAttemptOutcome outcome, long generation)
        {
            var fingerprint = DurableAppraisalCanonical.Hash(new[]
            {
                DurableAppraisalCanonical.Pair("entry_id", entryId),
                DurableAppraisalCanonical.Pair("packet_id", packetId),
                DurableAppraisalCanonical.Pair("outcome", outcome.ToString()),
                DurableAppraisalCanonical.Pair("checkpoint_generation", generation.ToString(CultureInfo.InvariantCulture))
            });
            return new DurableAdmissionAttemptReceipt(entryId, packetId, outcome, generation, fingerprint);
        }

        public string EntryId { get; }
        public string PacketId { get; }
        public DurableAdmissionAttemptOutcome Outcome { get; }
        public long CheckpointGeneration { get; }
        public string Fingerprint { get; }
        public bool IsCanonicalSuccess => false;
    }
}
