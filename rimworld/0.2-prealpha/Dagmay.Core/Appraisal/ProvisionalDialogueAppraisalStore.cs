using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Presentation;

namespace Dagmay.Core.Appraisal
{
    internal sealed class ProvisionalSeenFilter
    {
        public const int ByteCount = 1 << 20;
        public const int HashCount = 8;
        private const int BitCount = ByteCount * 8;
        private readonly byte[] _bits = new byte[ByteCount];

        public long Count { get; private set; }

        public bool Contains(byte[] key)
        {
            foreach (var position in Positions(key))
            {
                if ((_bits[position >> 3] & (1 << (position & 7))) == 0) return false;
            }
            return true;
        }

        public void Add(byte[] key)
        {
            foreach (var position in Positions(key))
                _bits[position >> 3] |= (byte)(1 << (position & 7));
            Count++;
        }

        public string Digest
        {
            get
            {
                using (var algorithm = SHA256.Create())
                {
                    return string.Concat(algorithm.ComputeHash(_bits)
                        .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
                }
            }
        }

        private static IEnumerable<int> Positions(byte[] key)
        {
            if (key is null || key.Length != 32)
                throw new ArgumentException("Seen-filter keys must contain exactly 32 bytes.", nameof(key));
            for (var index = 0; index < HashCount; index++)
            {
                var offset = index * 4;
                var value = ((uint)key[offset] << 24) |
                            ((uint)key[offset + 1] << 16) |
                            ((uint)key[offset + 2] << 8) |
                            key[offset + 3];
                yield return (int)(value % BitCount);
            }
        }
    }

    public sealed class ProvisionalDialogueAppraisalStore
    {
        public const string GateDigest = "c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819";
        public const decimal PerUtteranceAffectBound = 0.15m;
        public const decimal PerUtteranceRelationshipBound = 0.08m;
        public const decimal ConversationAffectBound = 0.35m;
        public const decimal ConversationRelationshipBound = 0.20m;
        public const int MaximumActivePerOwner = 32;
        public const long LifetimeTicks = 15_000;
        public const int MaximumCompletedRecent = 64;
        public const int FilterBytes = ProvisionalSeenFilter.ByteCount;

        private readonly string _sessionId;
        private readonly Dictionary<string, ProvisionalDialogueAppraisal> _items =
            new Dictionary<string, ProvisionalDialogueAppraisal>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _activeKeyToId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly ProvisionalSeenFilter _seenFilter = new ProvisionalSeenFilter();
        private readonly Dictionary<string, HashSet<string>> _activeByOwner =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Tuple<long, string>> _lastReceiptOrderByOwner =
            new Dictionary<string, Tuple<long, string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _terminalCounts =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, DurableApplicationPacket> _packets =
            new Dictionary<string, DurableApplicationPacket>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProvisionalDialogueAppraisal> _completedRecent =
            new Dictionary<string, ProvisionalDialogueAppraisal>(StringComparer.Ordinal);
        private readonly Queue<string> _completedOrder = new Queue<string>();
        private readonly ProvisionalSeenFilter _completedFilter = new ProvisionalSeenFilter();
        private string _terminalChain = new string('0', 64);
        private string _completedChain = new string('0', 64);
        private long _completedCount;

        public ProvisionalDialogueAppraisalStore(string sessionId)
        {
            _sessionId = ProvisionalAppraisalCanonical.Text(sessionId, nameof(sessionId));
        }

        public int ActiveCount(string? ownerId = null)
        {
            if (ownerId is null) return _activeByOwner.Values.Sum(value => value.Count);
            return _activeByOwner.TryGetValue(ownerId, out var ids) ? ids.Count : 0;
        }

        public int FullLiveRecordCount => _items.Count;
        public int PendingPacketCount => _packets.Count;
        public long SeenCount => _seenFilter.Count;
        public string SeenFilterDigest => _seenFilter.Digest;
        public string TerminalChain => _terminalChain;
        public long CompletedCount => _completedCount;
        public int CompletedRecentCount => _completedRecent.Count;
        public long CompletedFilterCount => _completedFilter.Count;
        public string CompletedFilterDigest => _completedFilter.Digest;
        public string CompletedChain => _completedChain;

        public ProvisionalDialogueAppraisal Prepare(ProvisionalDialogueAppraisalInput input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            var receipt = input.Receipt;
            if (!string.Equals(receipt.SessionId, _sessionId, StringComparison.Ordinal))
                throw new ArgumentException("Foreign or stale session receipt.", nameof(input));
            if (receipt.Outcome != ObservedDisplayOutcome.OBSERVED_SUCCESS)
                throw new ArgumentException("Release attempt is not observed success.", nameof(input));
            if (!string.Equals(receipt.SourceContract, ObservedDisplayReceipt.SourceContractValue, StringComparison.Ordinal) ||
                !string.Equals(receipt.SourceGateDigest, ObservedDisplayReceipt.SourceGateDigestValue, StringComparison.Ordinal))
                throw new ArgumentException("Foreign display-receipt source contract.", nameof(input));
            if (!string.Equals(receipt.ReceiptFingerprint, DisplayReceiptFingerprint(receipt), StringComparison.Ordinal))
                throw new ArgumentException("Display receipt fingerprint mismatch.", nameof(input));
            if (!receipt.AudienceIds.Contains(input.PerspectiveOwnerId, StringComparer.Ordinal))
                throw new ArgumentException("A non-witness cannot receive a provisional appraisal.", nameof(input));
            if (string.Equals(input.PerspectiveOwnerId, receipt.SpeakerId, StringComparison.Ordinal) &&
                input.Cues.All(cue => !cue.DirectToOwner))
                throw new ArgumentException("Speaker self-appraisal lacks explicit perspective attribution.", nameof(input));

            var supporting = ValidateKnowledge(input);
            var activeKey = input.PerspectiveOwnerId + "\u001f" + receipt.UtteranceId;
            if (_activeKeyToId.TryGetValue(activeKey, out var activeId)) return Require(activeId);
            var seenKey = Sha256Bytes(activeKey);
            if (_seenFilter.Contains(seenKey))
                throw new ArgumentException("Duplicate receipt already reached terminal state.", nameof(input));
            if (_lastReceiptOrderByOwner.TryGetValue(input.PerspectiveOwnerId, out var previous) &&
                CompareOrder(receipt.DisplayedTick, receipt.ReceiptId, previous.Item1, previous.Item2) <= 0)
                throw new ArgumentException("Out-of-order or stale observed receipt.", nameof(input));

            var computed = Compute(input);
            var fields = new List<KeyValuePair<string, string>>
            {
                ProvisionalAppraisalCanonical.Pair("schema", "mosaic.0.3d.provisional-dialogue-appraisal.v1"),
                ProvisionalAppraisalCanonical.Pair("owner_id", input.PerspectiveOwnerId),
                ProvisionalAppraisalCanonical.Pair("receipt_fingerprint", receipt.ReceiptFingerprint),
                ProvisionalAppraisalCanonical.Pair("affect_fingerprint", input.CanonicalAffectFingerprint),
                ProvisionalAppraisalCanonical.Pair("affect_version", input.CanonicalAffectVersion.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("relationship_fingerprint", input.RelationshipFingerprint ?? "null"),
                ProvisionalAppraisalCanonical.Pair("relationship_version", input.RelationshipVersion?.ToString(CultureInfo.InvariantCulture) ?? "null")
            };
            fields.AddRange(input.Cues.Select(value => ProvisionalAppraisalCanonical.Pair("cue", value.CanonicalKey)));
            fields.AddRange(supporting.Select(value => ProvisionalAppraisalCanonical.Pair("supporting_evidence_id", value)));
            var appraisalId = ProvisionalAppraisalCanonical.HashFields(fields);
            var item = new ProvisionalDialogueAppraisal(
                appraisalId,
                input.PerspectiveOwnerId,
                receipt.SessionId,
                receipt.ReceiptId,
                receipt.UtteranceId,
                receipt.ConversationId,
                receipt.SpeakerId,
                receipt.DisplayedTick,
                checked(receipt.DisplayedTick + LifetimeTicks),
                receipt.CheckpointGeneration,
                computed.Confidence,
                computed.Affect,
                computed.Relationship,
                computed.Biases,
                Presentation(computed.Confidence, computed.Affect, computed.Relationship, input.TraitProfile),
                supporting,
                computed.Reasons,
                input.CanonicalAffectFingerprint,
                input.CanonicalAffectVersion,
                input.RelationshipFingerprint,
                input.RelationshipVersion,
                ProvisionalAppraisalState.Prepared);
            _items.Add(item.AppraisalId, item);
            _activeKeyToId.Add(activeKey, item.AppraisalId);
            _seenFilter.Add(seenKey);
            _lastReceiptOrderByOwner[item.PerspectiveOwnerId] = Tuple.Create(receipt.DisplayedTick, receipt.ReceiptId);
            OwnerSet(item.PerspectiveOwnerId).Add(item.AppraisalId);
            return item;
        }

        public ProvisionalDialogueAppraisal Activate(string appraisalId, long currentTick)
        {
            var item = Require(appraisalId);
            if (item.State == ProvisionalAppraisalState.Active) return item;
            if (item.State != ProvisionalAppraisalState.Prepared)
                throw new ArgumentException("Only a prepared appraisal may activate.", nameof(appraisalId));
            if (currentTick < item.CreatedTick || currentTick >= item.ExpiresTick)
                throw new ArgumentOutOfRangeException(nameof(currentTick));
            item = item.WithState(ProvisionalAppraisalState.Active);
            _items[item.AppraisalId] = item;
            EnforceCapacity(item.PerspectiveOwnerId, currentTick);
            return _items.TryGetValue(item.AppraisalId, out var retained) ? retained : item;
        }

        public int Expire(long currentTick)
        {
            var expired = _items.Values
                .Where(item => (item.State == ProvisionalAppraisalState.Prepared ||
                                item.State == ProvisionalAppraisalState.Active) &&
                               currentTick >= item.ExpiresTick)
                .OrderBy(item => item.AppraisalId, StringComparer.Ordinal)
                .ToArray();
            foreach (var item in expired) SetTerminal(item, ProvisionalAppraisalState.Expired);
            return expired.Length;
        }

        public int CheckpointAdvanced(long newGeneration)
        {
            if (newGeneration < 0) throw new ArgumentOutOfRangeException(nameof(newGeneration));
            var discarded = _items.Values
                .Where(item => item.CheckpointGeneration != newGeneration)
                .OrderBy(item => item.AppraisalId, StringComparer.Ordinal)
                .ToArray();
            foreach (var item in discarded)
            {
                if (item.PromotionPacketId is not null) _packets.Remove(item.PromotionPacketId);
                SetTerminal(item, ProvisionalAppraisalState.Discarded);
            }
            return discarded.Length;
        }

        public ProvisionalDialogueAppraisal DiscardForAdmissionFailure(string appraisalId)
        {
            var item = Require(appraisalId);
            if (item.PromotionPacketId is not null) _packets.Remove(item.PromotionPacketId);
            return SetTerminal(item, ProvisionalAppraisalState.Discarded);
        }

        public ProvisionalOverlay EffectiveOverlay(string ownerId, string conversationId, long currentTick)
        {
            ProvisionalAppraisalCanonical.Text(ownerId, nameof(ownerId));
            ProvisionalAppraisalCanonical.Text(conversationId, nameof(conversationId));
            Expire(currentTick);
            var affect = ProvisionalAffectDelta.Zero;
            var relationship = ProvisionalRelationshipDelta.Zero;
            var biases = new SortedSet<string>(StringComparer.Ordinal);
            var items = _items.Values
                .Where(item =>
                    string.Equals(item.PerspectiveOwnerId, ownerId, StringComparison.Ordinal) &&
                    string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal) &&
                    (item.State == ProvisionalAppraisalState.Active ||
                     item.State == ProvisionalAppraisalState.PromotionPending))
                .OrderBy(item => item.CreatedTick)
                .ThenBy(item => item.AppraisalId, StringComparer.Ordinal);
            foreach (var item in items)
            {
                var factor = Decay(item, currentTick);
                affect = affect.Plus(item.AffectDelta.Scale(factor));
                relationship = relationship.Plus(item.RelationshipDelta.Scale(factor));
                biases.UnionWith(item.Biases);
            }
            return new ProvisionalOverlay(
                affect.Clamp(ConversationAffectBound),
                relationship.Clamp(ConversationRelationshipBound, ConversationRelationshipBound),
                new ReadOnlyCollection<string>(biases.ToArray()));
        }

        public DurableApplicationPacket ProposePromotion(
            string appraisalId,
            string admittedDialogueEventId,
            long checkpointGeneration,
            ProvisionalAffectDelta durableAffectDelta,
            ProvisionalRelationshipDelta durableRelationshipDelta,
            string sourceReceiptId)
        {
            var item = Require(appraisalId);
            if (item.State != ProvisionalAppraisalState.Active &&
                item.State != ProvisionalAppraisalState.PromotionPending)
                throw new ArgumentException("Appraisal is not active for promotion.", nameof(appraisalId));
            if (checkpointGeneration != item.CheckpointGeneration)
                throw new ArgumentException("Checkpoint generation mismatch.", nameof(checkpointGeneration));
            if (durableAffectDelta is null) throw new ArgumentNullException(nameof(durableAffectDelta));
            if (durableRelationshipDelta is null) throw new ArgumentNullException(nameof(durableRelationshipDelta));
            if (durableAffectDelta.MaximumAbsolute > PerUtteranceAffectBound)
                throw new ArgumentOutOfRangeException(nameof(durableAffectDelta));
            if (durableRelationshipDelta.MaximumAbsolute > PerUtteranceRelationshipBound)
                throw new ArgumentOutOfRangeException(nameof(durableRelationshipDelta));
            if (!durableRelationshipDelta.IsZero &&
                (item.RelationshipFingerprint is null || item.RelationshipVersion is null))
                throw new ArgumentException("Relationship proposal requires an existing canonical relationship state.", nameof(durableRelationshipDelta));

            var fields = new List<KeyValuePair<string, string>>
            {
                ProvisionalAppraisalCanonical.Pair("schema", "mosaic.0.3d.durable-application-packet.v1"),
                ProvisionalAppraisalCanonical.Pair("appraisal_id", item.AppraisalId),
                ProvisionalAppraisalCanonical.Pair("event_id", ProvisionalAppraisalCanonical.Text(admittedDialogueEventId, nameof(admittedDialogueEventId))),
                ProvisionalAppraisalCanonical.Pair("checkpoint_generation", checkpointGeneration.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("source_receipt_id", ProvisionalAppraisalCanonical.Text(sourceReceiptId, nameof(sourceReceiptId)))
            };
            fields.AddRange(durableAffectDelta.Fields("affect"));
            fields.AddRange(durableRelationshipDelta.Fields("relationship"));
            var packetId = ProvisionalAppraisalCanonical.HashFields(fields);
            if (_packets.TryGetValue(packetId, out var existing)) return existing;
            if (item.PromotionPacketId is not null)
                throw new ArgumentException("Conflicting promotion packet already pending.", nameof(appraisalId));
            var packet = new DurableApplicationPacket(
                packetId, item, admittedDialogueEventId, durableAffectDelta, durableRelationshipDelta, sourceReceiptId);
            _packets.Add(packet.PacketId, packet);
            _items[item.AppraisalId] = item.WithState(ProvisionalAppraisalState.PromotionPending, packet.PacketId);
            return packet;
        }

        internal ProvisionalDialogueAppraisal RequirePromotionPendingPacket(
            DurableApplicationPacket packet)
        {
            if (packet is null) throw new ArgumentNullException(nameof(packet));
            if (!_packets.TryGetValue(packet.PacketId, out var registered) ||
                !ReferenceEquals(registered, packet))
                throw new ArgumentException("Packet is not registered by this v39 appraisal store.", nameof(packet));
            var item = Require(packet.AppraisalId);
            if (item.State != ProvisionalAppraisalState.PromotionPending ||
                !string.Equals(item.PromotionPacketId, packet.PacketId, StringComparison.Ordinal))
                throw new ArgumentException("The v39 appraisal is not promotion-pending.", nameof(packet));
            return item;
        }

        public ProvisionalDialogueAppraisal ObserveApplicationReceipt(CanonicalApplicationReceipt receipt)
        {
            if (receipt is null) throw new ArgumentNullException(nameof(receipt));
            if (!string.Equals(receipt.SourceContract, CanonicalApplicationReceipt.SourceContractValue, StringComparison.Ordinal) ||
                !string.Equals(receipt.ComputeFingerprint(), receipt.ReceiptFingerprint, StringComparison.Ordinal))
                throw new ArgumentException("Forged canonical application receipt.", nameof(receipt));
            if (_completedRecent.TryGetValue(receipt.PacketId, out var recent)) return recent;
            var completedKey = HexBytes(receipt.PacketId);
            if (_completedFilter.Contains(completedKey))
                throw new ArgumentException("Duplicate successful receipt already compacted.", nameof(receipt));
            if (!_packets.TryGetValue(receipt.PacketId, out var packet))
                throw new ArgumentException("Foreign application receipt.", nameof(receipt));
            if (!string.Equals(receipt.PerspectiveOwnerId, packet.PerspectiveOwnerId, StringComparison.Ordinal) ||
                !string.Equals(receipt.AdmittedDialogueEventId, packet.AdmittedDialogueEventId, StringComparison.Ordinal))
                throw new ArgumentException("Application receipt identity mismatch.", nameof(receipt));
            if (receipt.CheckpointGeneration != packet.ExpectedCheckpointGeneration)
                throw new ArgumentException("Application receipt checkpoint mismatch.", nameof(receipt));
            var item = Require(packet.AppraisalId);
            if (item.State != ProvisionalAppraisalState.PromotionPending ||
                !string.Equals(item.PromotionPacketId, packet.PacketId, StringComparison.Ordinal))
                throw new ArgumentException("Application receipt does not match pending state.", nameof(receipt));

            if (!receipt.Success)
            {
                var active = item.WithState(ProvisionalAppraisalState.Active);
                _items[active.AppraisalId] = active;
                _packets.Remove(packet.PacketId);
                return active;
            }

            ValidateSuccessfulApplication(packet, receipt);
            var promoted = SetTerminal(item, ProvisionalAppraisalState.Promoted);
            _packets.Remove(packet.PacketId);
            _completedRecent.Add(packet.PacketId, promoted);
            _completedOrder.Enqueue(packet.PacketId);
            _completedFilter.Add(completedKey);
            _completedCount++;
            _completedChain = ProvisionalAppraisalCanonical.HashFields(new[]
            {
                ProvisionalAppraisalCanonical.Pair("previous", _completedChain),
                ProvisionalAppraisalCanonical.Pair("packet_id", packet.PacketId),
                ProvisionalAppraisalCanonical.Pair("appraisal_id", promoted.AppraisalId),
                ProvisionalAppraisalCanonical.Pair("state", promoted.State.ToString())
            });
            while (_completedRecent.Count > MaximumCompletedRecent)
                _completedRecent.Remove(_completedOrder.Dequeue());
            return promoted;
        }

        public string StateDigest()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                ProvisionalAppraisalCanonical.Pair("schema", "mosaic.0.3d.provisional-dialogue-appraisal.v1"),
                ProvisionalAppraisalCanonical.Pair("session_id", _sessionId),
                ProvisionalAppraisalCanonical.Pair("seen_count", _seenFilter.Count.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("seen_filter_digest", _seenFilter.Digest),
                ProvisionalAppraisalCanonical.Pair("terminal_chain", _terminalChain),
                ProvisionalAppraisalCanonical.Pair("completed_count", _completedCount.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("completed_filter_digest", _completedFilter.Digest),
                ProvisionalAppraisalCanonical.Pair("completed_chain", _completedChain)
            };
            foreach (var item in _items.Values.OrderBy(value => value.AppraisalId, StringComparer.Ordinal))
            {
                fields.Add(ProvisionalAppraisalCanonical.Pair("item", string.Join("|", new[]
                {
                    item.AppraisalId,
                    item.State.ToString(),
                    item.PromotionPacketId ?? "null"
                })));
            }
            foreach (var packet in _packets.Values.OrderBy(value => value.PacketId, StringComparer.Ordinal))
                fields.Add(ProvisionalAppraisalCanonical.Pair("packet", packet.PacketId));
            foreach (var pair in _terminalCounts.OrderBy(value => value.Key, StringComparer.Ordinal))
                fields.Add(ProvisionalAppraisalCanonical.Pair("terminal." + pair.Key, pair.Value.ToString(CultureInfo.InvariantCulture)));
            return ProvisionalAppraisalCanonical.HashFields(fields);
        }

        public ProvisionalAppraisalDiagnostics DiagnosticProjection()
        {
            var counts = new SortedDictionary<string, long>(_terminalCounts, StringComparer.Ordinal);
            foreach (var item in _items.Values)
            {
                var name = item.State.ToString();
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
            }
            return new ProvisionalAppraisalDiagnostics(
                ProvisionalAppraisalCanonical.HashFields(new[]
                {
                    ProvisionalAppraisalCanonical.Pair("session_id", _sessionId)
                }),
                new ReadOnlyDictionary<string, long>(counts),
                ActiveCount(),
                _packets.Count,
                _completedCount,
                _completedRecent.Count,
                ProvisionalSeenFilter.ByteCount,
                StateDigest());
        }

        private static IReadOnlyList<string> ValidateKnowledge(ProvisionalDialogueAppraisalInput input)
        {
            var byId = input.Knowledge.ToDictionary(value => value.EvidenceId, StringComparer.Ordinal);
            var referenced = input.Cues.SelectMany(value => value.SourceEvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (var evidenceId in referenced)
            {
                if (!byId.TryGetValue(evidenceId, out var evidence))
                    throw new ArgumentException("Cue references missing knowledge evidence.", nameof(input));
                if (!string.Equals(evidence.OwnerId, input.PerspectiveOwnerId, StringComparison.Ordinal))
                    throw new ArgumentException("Foreign or private knowledge cannot enter appraisal.", nameof(input));
                if (!evidence.Admitted)
                    throw new ArgumentException("Unadmitted knowledge cannot enter appraisal.", nameof(input));
                if (evidence.ObservedTick > input.Receipt.DisplayedTick)
                    throw new ArgumentException("Future knowledge cannot enter appraisal.", nameof(input));
            }
            return new ReadOnlyCollection<string>(referenced);
        }

        private static string DisplayReceiptFingerprint(ObservedDisplayReceipt receipt)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                ProvisionalAppraisalCanonical.Pair("session_id", receipt.SessionId),
                ProvisionalAppraisalCanonical.Pair("release_attempt_id", receipt.ReleaseAttemptId),
                ProvisionalAppraisalCanonical.Pair("receipt_id", receipt.ReceiptId),
                ProvisionalAppraisalCanonical.Pair("utterance_id", receipt.UtteranceId),
                ProvisionalAppraisalCanonical.Pair("conversation_id", receipt.ConversationId),
                ProvisionalAppraisalCanonical.Pair("speaker_id", receipt.SpeakerId),
                ProvisionalAppraisalCanonical.Pair("recipient_id", receipt.RecipientId ?? "null")
            };
            fields.AddRange(receipt.AudienceIds.Select(value =>
                ProvisionalAppraisalCanonical.Pair("audience_id", value)));
            fields.Add(ProvisionalAppraisalCanonical.Pair("exact_text_hash", receipt.ExactTextHash));
            fields.Add(ProvisionalAppraisalCanonical.Pair("displayed_tick", receipt.DisplayedTick.ToString(CultureInfo.InvariantCulture)));
            fields.Add(ProvisionalAppraisalCanonical.Pair("checkpoint_generation", receipt.CheckpointGeneration.ToString(CultureInfo.InvariantCulture)));
            fields.Add(ProvisionalAppraisalCanonical.Pair("outcome", receipt.Outcome.ToString()));
            fields.Add(ProvisionalAppraisalCanonical.Pair("source_contract", receipt.SourceContract));
            fields.Add(ProvisionalAppraisalCanonical.Pair("source_gate_digest", receipt.SourceGateDigest));
            return ProvisionalAppraisalCanonical.HashFields(fields);
        }

        private static ComputedAppraisal Compute(ProvisionalDialogueAppraisalInput input)
        {
            var affect = ProvisionalAffectDelta.Zero;
            var relationship = ProvisionalRelationshipDelta.Zero;
            var biases = new SortedSet<string>(StringComparer.Ordinal);
            var reasons = new SortedSet<string>(StringComparer.Ordinal);
            decimal totalWeight = 0m;
            var active = 0;
            foreach (var cue in input.Cues)
            {
                var weight = CueWeight(cue);
                if (weight == 0m)
                {
                    reasons.Add("QUOTED_OR_NONATTRIBUTABLE_CUE_SUPPRESSED");
                    continue;
                }
                var target = CueTarget(cue.CueType);
                var factor = ProvisionalAppraisalCanonical.Bound(weight * input.TraitProfile.ResponseFactor, 0m, 1.25m);
                var cueAffect = target.Item1.Scale(factor);
                if (cue.CueType == ProvisionalCueType.Threat)
                {
                    cueAffect = new ProvisionalAffectDelta(
                        cueAffect.Valence, cueAffect.Arousal,
                        cueAffect.Threat * (0.75m + input.TraitProfile.ThreatVigilance * 0.50m),
                        cueAffect.Agency, cueAffect.Attachment, cueAffect.Certainty, cueAffect.SocialStanding);
                }
                if (cue.CueType == ProvisionalCueType.Affection ||
                    cue.CueType == ProvisionalCueType.Disclosure ||
                    cue.CueType == ProvisionalCueType.Gratitude)
                {
                    cueAffect = new ProvisionalAffectDelta(
                        cueAffect.Valence, cueAffect.Arousal, cueAffect.Threat, cueAffect.Agency,
                        cueAffect.Attachment * (0.75m + input.TraitProfile.AttachmentSensitivity * 0.50m),
                        cueAffect.Certainty, cueAffect.SocialStanding);
                }
                affect = affect.Plus(cueAffect);
                relationship = relationship.Plus(target.Item2.Scale(factor));
                biases.UnionWith(target.Item3);
                reasons.Add("CUE_" + ToContractToken(cue.CueType));
                totalWeight += weight;
                active++;
            }
            if (reasons.Count == 0) reasons.Add("CUE_NEUTRAL");
            return new ComputedAppraisal(
                affect.Clamp(PerUtteranceAffectBound),
                relationship.Clamp(PerUtteranceRelationshipBound, PerUtteranceRelationshipBound),
                new ReadOnlyCollection<string>(biases.ToArray()),
                active == 0 ? 0m : ProvisionalAppraisalCanonical.Bound(totalWeight / active, 0m, 1m),
                new ReadOnlyCollection<string>(reasons.ToArray()));
        }

        private static decimal CueWeight(ProvisionalSpeechCue cue)
        {
            if (cue.QuoteDepth > 0) return 0m;
            var weight = cue.Confidence;
            if (!cue.DirectToOwner &&
                (cue.CueType == ProvisionalCueType.Threat ||
                 cue.CueType == ProvisionalCueType.Insult ||
                 cue.CueType == ProvisionalCueType.Accusation ||
                 cue.CueType == ProvisionalCueType.Blame))
                weight *= 0.35m;
            if (cue.Factuality == ProvisionalFactuality.ClaimOnly) weight *= 0.70m;
            return ProvisionalAppraisalCanonical.Bound(weight, 0m, 1m);
        }

        private static Tuple<ProvisionalAffectDelta, ProvisionalRelationshipDelta, string[]> CueTarget(ProvisionalCueType cue)
        {
            switch (cue)
            {
                case ProvisionalCueType.Gratitude: return Tuple.Create(new ProvisionalAffectDelta(valence: 0.09m, attachment: 0.06m, certainty: 0.02m), new ProvisionalRelationshipDelta(trust: 0.04m, affection: 0.04m), new[] { "ATTENTION", "WARMTH" });
                case ProvisionalCueType.Praise: return Tuple.Create(new ProvisionalAffectDelta(valence: 0.08m, agency: 0.03m, socialStanding: 0.05m), new ProvisionalRelationshipDelta(trust: 0.02m, affection: 0.03m), new[] { "WARMTH" });
                case ProvisionalCueType.Apology: return Tuple.Create(new ProvisionalAffectDelta(valence: 0.03m, arousal: -0.03m, threat: -0.03m, certainty: 0.01m), new ProvisionalRelationshipDelta(trust: 0.03m, affection: 0.01m), new[] { "ATTENTION", "REPAIR_SEEKING" });
                case ProvisionalCueType.RepairAttempt: return Tuple.Create(new ProvisionalAffectDelta(valence: 0.02m, threat: -0.02m, attachment: 0.03m), new ProvisionalRelationshipDelta(trust: 0.025m, affection: 0.01m), new[] { "REPAIR_SEEKING" });
                case ProvisionalCueType.Request: return Tuple.Create(new ProvisionalAffectDelta(arousal: 0.02m, agency: -0.01m, certainty: -0.01m), ProvisionalRelationshipDelta.Zero, new[] { "ATTENTION" });
                case ProvisionalCueType.Refusal: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.03m, arousal: 0.02m, agency: -0.02m), new ProvisionalRelationshipDelta(resentment: 0.015m), new[] { "GUARDEDNESS" });
                case ProvisionalCueType.Accusation: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.07m, arousal: 0.08m, threat: 0.07m, certainty: -0.03m, socialStanding: -0.03m), new ProvisionalRelationshipDelta(trust: -0.04m, resentment: 0.05m), new[] { "DEFENSIVENESS", "GUARDEDNESS" });
                case ProvisionalCueType.Blame: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.06m, arousal: 0.06m, threat: 0.04m, agency: -0.03m), new ProvisionalRelationshipDelta(trust: -0.03m, resentment: 0.04m), new[] { "DEFENSIVENESS" });
                case ProvisionalCueType.Threat: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.08m, arousal: 0.10m, threat: 0.12m, certainty: -0.04m), new ProvisionalRelationshipDelta(trust: -0.05m, fear: 0.07m, resentment: 0.03m), new[] { "GUARDEDNESS", "WITHDRAWAL" });
                case ProvisionalCueType.Insult: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.08m, arousal: 0.06m, threat: 0.03m, socialStanding: -0.06m), new ProvisionalRelationshipDelta(trust: -0.025m, resentment: 0.05m), new[] { "DEFENSIVENESS", "GUARDEDNESS" });
                case ProvisionalCueType.Disclosure: return Tuple.Create(new ProvisionalAffectDelta(arousal: 0.02m, attachment: 0.04m, certainty: -0.02m), new ProvisionalRelationshipDelta(trust: 0.02m, affection: 0.02m), new[] { "ATTENTION", "REASSURANCE" });
                case ProvisionalCueType.Affection: return Tuple.Create(new ProvisionalAffectDelta(valence: 0.10m, arousal: 0.03m, attachment: 0.10m), new ProvisionalRelationshipDelta(trust: 0.03m, affection: 0.06m), new[] { "REASSURANCE", "WARMTH" });
                case ProvisionalCueType.Fear: return Tuple.Create(new ProvisionalAffectDelta(valence: -0.03m, arousal: 0.06m, threat: 0.05m, certainty: -0.03m), new ProvisionalRelationshipDelta(fear: 0.02m), new[] { "ATTENTION", "REASSURANCE" });
                case ProvisionalCueType.Uncertainty: return Tuple.Create(new ProvisionalAffectDelta(arousal: 0.01m, certainty: -0.05m), ProvisionalRelationshipDelta.Zero, new[] { "UNCERTAINTY" });
                default: return Tuple.Create(ProvisionalAffectDelta.Zero, ProvisionalRelationshipDelta.Zero, Array.Empty<string>());
            }
        }

        private static string ToContractToken(ProvisionalCueType cue)
        {
            var name = cue.ToString();
            var builder = new StringBuilder();
            foreach (var character in name)
            {
                if (char.IsUpper(character) && builder.Length > 0) builder.Append('_');
                builder.Append(char.ToUpperInvariant(character));
            }
            return builder.ToString();
        }

        private static ProvisionalPresentationMode Presentation(
            decimal confidence,
            ProvisionalAffectDelta affect,
            ProvisionalRelationshipDelta relationship,
            ProvisionalTraitProfile traits)
        {
            var intensity = Math.Max(affect.MaximumAbsolute / 0.03m, relationship.MaximumAbsolute / 0.016m);
            var effective = intensity * (0.70m + traits.Expressiveness * 0.60m) * confidence;
            if (confidence < 0.35m || effective < 1.50m) return ProvisionalPresentationMode.None;
            return effective < 3.50m ? ProvisionalPresentationMode.Icon : ProvisionalPresentationMode.Thought;
        }

        private void EnforceCapacity(string ownerId, long currentTick)
        {
            var ids = OwnerSet(ownerId);
            if (ids.Count <= MaximumActivePerOwner) return;
            var victims = ids.Select(id => _items[id])
                .OrderBy(item => currentTick >= item.ExpiresTick ? 0 : 1)
                .ThenBy(item => item.Confidence)
                .ThenBy(item => item.CreatedTick)
                .ThenBy(item => item.AppraisalId, StringComparer.Ordinal)
                .Take(ids.Count - MaximumActivePerOwner)
                .ToArray();
            foreach (var item in victims)
                SetTerminal(item, currentTick >= item.ExpiresTick
                    ? ProvisionalAppraisalState.Expired
                    : ProvisionalAppraisalState.Discarded);
        }

        private ProvisionalDialogueAppraisal SetTerminal(
            ProvisionalDialogueAppraisal item,
            ProvisionalAppraisalState state)
        {
            var terminal = item.WithState(state);
            _items.Remove(item.AppraisalId);
            OwnerSet(item.PerspectiveOwnerId).Remove(item.AppraisalId);
            _activeKeyToId.Remove(item.PerspectiveOwnerId + "\u001f" + item.UtteranceId);
            var name = state.ToString();
            _terminalCounts[name] = _terminalCounts.TryGetValue(name, out var count) ? count + 1 : 1;
            _terminalChain = ProvisionalAppraisalCanonical.HashFields(new[]
            {
                ProvisionalAppraisalCanonical.Pair("previous", _terminalChain),
                ProvisionalAppraisalCanonical.Pair("appraisal_id", item.AppraisalId),
                ProvisionalAppraisalCanonical.Pair("state", name),
                ProvisionalAppraisalCanonical.Pair("created_tick", item.CreatedTick.ToString(CultureInfo.InvariantCulture))
            });
            return terminal;
        }

        private static void ValidateSuccessfulApplication(
            DurableApplicationPacket packet,
            CanonicalApplicationReceipt receipt)
        {
            if (!packet.ProposedAffectDelta.IsZero)
            {
                if (receipt.ResultingAffectVersion <= packet.ExpectedAffectVersion)
                    throw new ArgumentException("Successful receipt must advance targeted affect version.", nameof(receipt));
            }
            else if (receipt.ResultingAffectVersion != packet.ExpectedAffectVersion ||
                     !string.Equals(receipt.ResultingAffectFingerprint, packet.ExpectedAffectFingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException("Successful receipt must preserve untargeted affect state.", nameof(receipt));
            }

            if (!packet.ProposedRelationshipDelta.IsZero)
            {
                if (packet.ExpectedRelationshipVersion is null ||
                    packet.ExpectedRelationshipFingerprint is null ||
                    receipt.ResultingRelationshipVersion is null ||
                    receipt.ResultingRelationshipVersion <= packet.ExpectedRelationshipVersion)
                    throw new ArgumentException("Successful receipt must advance targeted relationship version.", nameof(receipt));
            }
            else if (packet.ExpectedRelationshipVersion is null)
            {
                if (receipt.ResultingRelationshipVersion is not null ||
                    receipt.ResultingRelationshipFingerprint is not null)
                    throw new ArgumentException("Successful receipt must preserve absent relationship state.", nameof(receipt));
            }
            else if (receipt.ResultingRelationshipVersion != packet.ExpectedRelationshipVersion ||
                     !string.Equals(receipt.ResultingRelationshipFingerprint, packet.ExpectedRelationshipFingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException("Successful receipt must preserve untargeted relationship state.", nameof(receipt));
            }
        }

        private ProvisionalDialogueAppraisal Require(string appraisalId)
        {
            ProvisionalAppraisalCanonical.LowerHex(appraisalId, nameof(appraisalId));
            if (!_items.TryGetValue(appraisalId, out var item))
                throw new ArgumentException("Unknown appraisal.", nameof(appraisalId));
            return item;
        }

        private HashSet<string> OwnerSet(string ownerId)
        {
            if (!_activeByOwner.TryGetValue(ownerId, out var ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                _activeByOwner.Add(ownerId, ids);
            }
            return ids;
        }

        private static decimal Decay(ProvisionalDialogueAppraisal item, long currentTick)
        {
            if (currentTick <= item.CreatedTick) return 1m;
            if (currentTick >= item.ExpiresTick) return 0m;
            return ProvisionalAppraisalCanonical.Bound(
                1m - (decimal)(currentTick - item.CreatedTick) / (item.ExpiresTick - item.CreatedTick),
                0m,
                1m);
        }

        private static int CompareOrder(long tick, string receiptId, long previousTick, string previousReceiptId)
        {
            var tickComparison = tick.CompareTo(previousTick);
            return tickComparison != 0
                ? tickComparison
                : string.Compare(receiptId, previousReceiptId, StringComparison.Ordinal);
        }

        private static byte[] Sha256Bytes(string value)
        {
            using (var algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static byte[] HexBytes(string value)
        {
            ProvisionalAppraisalCanonical.LowerHex(value, nameof(value));
            var bytes = new byte[32];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        private sealed class ComputedAppraisal
        {
            public ComputedAppraisal(
                ProvisionalAffectDelta affect,
                ProvisionalRelationshipDelta relationship,
                IReadOnlyList<string> biases,
                decimal confidence,
                IReadOnlyList<string> reasons)
            {
                Affect = affect;
                Relationship = relationship;
                Biases = biases;
                Confidence = confidence;
                Reasons = reasons;
            }

            public ProvisionalAffectDelta Affect { get; }
            public ProvisionalRelationshipDelta Relationship { get; }
            public IReadOnlyList<string> Biases { get; }
            public decimal Confidence { get; }
            public IReadOnlyList<string> Reasons { get; }
        }
    }
}
