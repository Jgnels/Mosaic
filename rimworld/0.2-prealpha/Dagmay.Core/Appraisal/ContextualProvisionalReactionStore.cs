using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Development;
using Dagmay.Core.Presentation;

namespace Dagmay.Core.Appraisal
{
    public sealed class V43ProposalTrustRegistry
    {
        private readonly Dictionary<string, V43ProposalAttestation> _pending =
            new Dictionary<string, V43ProposalAttestation>(StringComparer.Ordinal);
        public V43ProposalTrustRegistry(string saveId, string worldId, string storeSetId)
        {
            SaveId=ContextualProvisionalAdmissionSource.Token(saveId,nameof(saveId));
            WorldId=ContextualProvisionalAdmissionSource.Token(worldId,nameof(worldId));
            StoreSetId=ContextualProvisionalAdmissionSource.Token(storeSetId,nameof(storeSetId));
        }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public int Count => _pending.Count;

        public V43ProposalAttestation Attest(ContextualAppraisalRequest request, GroundedCompoundAppraisalProposal proposal)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            proposal = GroundedCompoundAppraisalProposal.RequireValid(proposal);
            var exact = new GroundedCompoundAppraisalSynthesizer().Synthesize(request);
            if (!string.Equals(exact.ToDeterministicJson(), proposal.ToDeterministicJson(), StringComparison.Ordinal))
                throw new ArgumentException("Proposal is not exact deterministic v43 synthesizer output.", nameof(proposal));
            var seed=request.CurrentSeed;
            if (!string.Equals(proposal.RequestFingerprint,request.Fingerprint,StringComparison.Ordinal) ||
                !string.Equals(proposal.CurrentSeedFingerprint,seed.Fingerprint,StringComparison.Ordinal) ||
                !string.Equals(proposal.ContextPacketFingerprint,request.ContextPacket.Fingerprint,StringComparison.Ordinal))
                throw new ArgumentException("Proposal/request provenance mismatch.",nameof(proposal));
            if (!string.Equals(seed.SaveId,SaveId,StringComparison.Ordinal) ||
                !string.Equals(seed.WorldId,WorldId,StringComparison.Ordinal) ||
                !string.Equals(seed.StoreSetId,StoreSetId,StringComparison.Ordinal))
                throw new ArgumentException("Foreign v43 store binding.",nameof(request));
            if (_pending.Count >= ContextualProvisionalAdmissionSource.MaximumPendingAttestations)
                throw new InvalidOperationException("V43 attestation backlog full.");
            var attestation=new V43ProposalAttestation(proposal,seed.CheckpointGeneration,seed.CheckpointFingerprint,SaveId,WorldId,StoreSetId);
            _pending.Add(attestation.Fingerprint,attestation);
            return attestation;
        }

        internal void Verify(V43ProposalAttestation attestation, GroundedCompoundAppraisalProposal proposal)
        {
            if (attestation is null || !_pending.TryGetValue(attestation.Fingerprint,out var trusted) ||
                !ReferenceEquals(trusted,attestation) || !attestation.VerifyFingerprint() ||
                !string.Equals(attestation.ProposalFingerprint,proposal.Fingerprint,StringComparison.Ordinal) ||
                !string.Equals(attestation.SaveId,SaveId,StringComparison.Ordinal) ||
                !string.Equals(attestation.WorldId,WorldId,StringComparison.Ordinal) ||
                !string.Equals(attestation.StoreSetId,StoreSetId,StringComparison.Ordinal))
                throw new InvalidOperationException("Untrusted v43 attestation.");
        }
        internal void Consume(V43ProposalAttestation attestation)
        {
            Verify(attestation, GroundedCompoundAppraisalProposal.RequireValid(
                _proposalForConsume ?? throw new InvalidOperationException("Consumption requires verified proposal.")));
            _pending.Remove(attestation.Fingerprint);
            _proposalForConsume=null;
        }
        private GroundedCompoundAppraisalProposal? _proposalForConsume;
        internal void MarkVerifiedForConsume(V43ProposalAttestation attestation,GroundedCompoundAppraisalProposal proposal)
        { Verify(attestation,proposal); _proposalForConsume=proposal; }
    }

    public sealed class DialogueAdmissionTrustRegistry
    {
        private readonly Dictionary<string,ContextualAdmittedDialogueEventReceipt> _pending =
            new Dictionary<string,ContextualAdmittedDialogueEventReceipt>(StringComparer.Ordinal);
        public DialogueAdmissionTrustRegistry(string saveId,string worldId,string storeSetId)
        {
            SaveId=ContextualProvisionalAdmissionSource.Token(saveId,nameof(saveId));
            WorldId=ContextualProvisionalAdmissionSource.Token(worldId,nameof(worldId));
            StoreSetId=ContextualProvisionalAdmissionSource.Token(storeSetId,nameof(storeSetId));
        }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public int Count=>_pending.Count;
        public ContextualAdmittedDialogueEventReceipt Create(
            string eventId,string eventHash,ObservedDisplayReceipt displayReceipt,long admittedTick,string checkpointFingerprint)
        {
            if(displayReceipt is null)throw new ArgumentNullException(nameof(displayReceipt));
            if(displayReceipt.Outcome!=ObservedDisplayOutcome.OBSERVED_SUCCESS ||
               !string.Equals(displayReceipt.SourceContract,ObservedDisplayReceipt.SourceContractValue,StringComparison.Ordinal) ||
               !string.Equals(displayReceipt.SourceGateDigest,ObservedDisplayReceipt.SourceGateDigestValue,StringComparison.Ordinal))
                throw new ArgumentException("Display receipt is not trusted observed success.",nameof(displayReceipt));
            if(_pending.Count>=ContextualProvisionalAdmissionSource.MaximumPendingAttestations)
                throw new InvalidOperationException("Dialogue admission backlog full.");
            var receipt=new ContextualAdmittedDialogueEventReceipt(
                eventId,eventHash,displayReceipt,admittedTick,checkpointFingerprint,SaveId,WorldId,StoreSetId);
            _pending.Add(receipt.Fingerprint,receipt);
            return receipt;
        }
        internal void Verify(ContextualAdmittedDialogueEventReceipt receipt)
        {
            if(receipt is null || !_pending.TryGetValue(receipt.Fingerprint,out var trusted) ||
               !ReferenceEquals(trusted,receipt) || !receipt.VerifyFingerprint() ||
               !string.Equals(receipt.SaveId,SaveId,StringComparison.Ordinal) ||
               !string.Equals(receipt.WorldId,WorldId,StringComparison.Ordinal) ||
               !string.Equals(receipt.StoreSetId,StoreSetId,StringComparison.Ordinal))
                throw new InvalidOperationException("Untrusted dialogue admission receipt.");
        }
        internal void Consume(ContextualAdmittedDialogueEventReceipt receipt){Verify(receipt);_pending.Remove(receipt.Fingerprint);}
    }

    public sealed class ReactionPathClaimRegistry
    {
        private readonly Dictionary<string,Tuple<ProvisionalReactionPath,string>> _claims =
            new Dictionary<string,Tuple<ProvisionalReactionPath,string>>(StringComparer.Ordinal);
        private static string Key(string owner,string utterance)=>owner+"\u001f"+utterance;
        public void Claim(string owner,string utterance,ProvisionalReactionPath path,string claimId)
        {
            var key=Key(ContextualProvisionalAdmissionSource.Token(owner,nameof(owner)),
                ContextualProvisionalAdmissionSource.Token(utterance,nameof(utterance)));
            ContextualProvisionalAdmissionSource.Token(claimId,nameof(claimId));
            var proposed=Tuple.Create(path,claimId);
            if(_claims.TryGetValue(key,out var existing))
            {
                if(existing.Item1==path && string.Equals(existing.Item2,claimId,StringComparison.Ordinal))return;
                throw new InvalidOperationException("Reaction path already claimed.");
            }
            if(_claims.Count>=ContextualProvisionalAdmissionSource.MaximumPathClaims)
                throw new InvalidOperationException("Reaction path claim backlog full.");
            _claims.Add(key,proposed);
        }
        public void Release(string owner,string utterance,ProvisionalReactionPath path,string claimId)
        {
            var key=Key(owner,utterance);
            if(_claims.TryGetValue(key,out var existing) && existing.Item1==path &&
               string.Equals(existing.Item2,claimId,StringComparison.Ordinal))_claims.Remove(key);
        }
        public int Count=>_claims.Count;
    }

    public sealed class ContextualDiagnosticProjection
    {
        internal ContextualDiagnosticProjection(string sessionHash,string bindingHash,long generation,int active,int claims,
            long seen,long seenEvents,IReadOnlyDictionary<string,int> terminalCounts,string stateDigest)
        {SessionIdHash=sessionHash;BindingHash=bindingHash;CheckpointGeneration=generation;ActiveCount=active;ClaimCount=claims;
         SeenCount=seen;SeenEventCount=seenEvents;TerminalCounts=terminalCounts;StateDigest=stateDigest;}
        public string SessionIdHash{get;} public string BindingHash{get;} public long CheckpointGeneration{get;}
        public int ActiveCount{get;} public int ClaimCount{get;} public long SeenCount{get;} public long SeenEventCount{get;}
        public int SeenFilterBytes=>ProvisionalSeenFilter.ByteCount; public int EventFilterBytes=>ProvisionalSeenFilter.ByteCount;
        public IReadOnlyDictionary<string,int> TerminalCounts{get;} public string StateDigest{get;}
        public string Authority=>ContextualProvisionalAdmissionSource.Authority;
    }

    public sealed class ContextualProvisionalReactionStore
    {
        private readonly string _session,_save,_world,_stores;
        private long _checkpointGeneration; private string _checkpointFingerprint;
        private readonly V43ProposalTrustRegistry _proposals; private readonly DialogueAdmissionTrustRegistry _dialogue;
        private readonly ReactionPathClaimRegistry _claims;
        private readonly Dictionary<string,ContextualProvisionalReaction> _items=new Dictionary<string,ContextualProvisionalReaction>(StringComparer.Ordinal);
        private readonly Dictionary<string,string> _activeKey=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<string,string> _activeEventKey=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<string,HashSet<string>> _activeByOwner=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string,ObservedProvisionalAdmissionReceipt> _receipts=new Dictionary<string,ObservedProvisionalAdmissionReceipt>(StringComparer.Ordinal);
        private readonly Dictionary<string,Tuple<long,string>> _lastOrder=new Dictionary<string,Tuple<long,string>>(StringComparer.Ordinal);
        private readonly ProvisionalSeenFilter _seen=new ProvisionalSeenFilter();
        private readonly ProvisionalSeenFilter _seenEvent=new ProvisionalSeenFilter();
        private readonly Dictionary<string,int> _terminalCounts=new Dictionary<string,int>(StringComparer.Ordinal);
        private string _terminalChain=new string('0',64);

        public ContextualProvisionalReactionStore(
            string sessionId,string saveId,string worldId,string storeSetId,long checkpointGeneration,string checkpointFingerprint,
            V43ProposalTrustRegistry proposals,DialogueAdmissionTrustRegistry dialogue,ReactionPathClaimRegistry? claims=null)
        {
            _session=ContextualProvisionalAdmissionSource.Token(sessionId,nameof(sessionId));
            _save=ContextualProvisionalAdmissionSource.Token(saveId,nameof(saveId));
            _world=ContextualProvisionalAdmissionSource.Token(worldId,nameof(worldId));
            _stores=ContextualProvisionalAdmissionSource.Token(storeSetId,nameof(storeSetId));
            if(checkpointGeneration<0)throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            _checkpointGeneration=checkpointGeneration;
            _checkpointFingerprint=ContextualProvisionalAdmissionSource.Sha(checkpointFingerprint,nameof(checkpointFingerprint));
            _proposals=proposals??throw new ArgumentNullException(nameof(proposals));
            _dialogue=dialogue??throw new ArgumentNullException(nameof(dialogue));
            _claims=claims??new ReactionPathClaimRegistry();
            if(proposals.SaveId!=_save||proposals.WorldId!=_world||proposals.StoreSetId!=_stores||
               dialogue.SaveId!=_save||dialogue.WorldId!=_world||dialogue.StoreSetId!=_stores)
                throw new ArgumentException("Foreign trust registry binding.");
        }

        public ContextualProvisionalAdmissionAttempt PrepareAttempt(ContextualAdmissionRequest request)
        {
            if(request is null)throw new ArgumentNullException(nameof(request));
            if(!string.Equals(request.SessionId,_session,StringComparison.Ordinal))throw new ArgumentException("Foreign request session.");
            _proposals.Verify(request.ProposalAttestation,request.Proposal); _dialogue.Verify(request.DialogueReceipt);
            var attestation=request.ProposalAttestation;
            if(attestation.SaveId!=_save||attestation.WorldId!=_world||attestation.StoreSetId!=_stores||
               attestation.CheckpointGeneration!=_checkpointGeneration||attestation.CheckpointFingerprint!=_checkpointFingerprint)
                throw new ArgumentException("Stale or foreign request binding.");
            var affect=Scale(request.Proposal.AffectDimensions,ContextualProvisionalAdmissionSource.AffectOverlayCapBps,request.Proposal.ConfidenceBps);
            var relationship=Scale(request.Proposal.RelationshipConsiderations,ContextualProvisionalAdmissionSource.RelationshipOverlayCapBps,request.Proposal.ConfidenceBps);
            var families=request.Proposal.Families
                .Select(row=>new KeyValuePair<string,int>(row.Family.ToString().ToUpperInvariant(),
                    Truncate((long)row.NetEvidenceBps*request.Proposal.ConfidenceBps,10000)))
                .Where(row=>row.Value>0).OrderByDescending(row=>row.Value).ThenBy(row=>row.Key,StringComparer.Ordinal).ToList();
            var biases=families.SelectMany(row=>Biases(row.Key)).Distinct(StringComparer.Ordinal)
                .OrderBy(value=>value,StringComparer.Ordinal).Take(ContextualProvisionalAdmissionSource.MaximumBiases).ToList();
            if(families.Count==0&&affect.Count==0&&relationship.Count==0)
                throw new InvalidOperationException("Contextual appraisal maps to no provisional effect.");
            var reasons=new HashSet<string>(StringComparer.Ordinal){"V43_GROUNDED_COMPOUND_APPRAISAL","OBSERVED_DISPLAY_SUCCESS","CANONICAL_DIALOGUE_EVENT_ADMITTED"};
            foreach(var family in families)reasons.Add("FAMILY_"+family.Key);
            foreach(var uncertainty in request.Proposal.UncertaintyCodes)reasons.Add("V43_"+uncertainty);
            var attemptId="contextual-admission-attempt:"+ContextualProvisionalAdmissionSource.Hash(
                "{\"contract\":"+ContextualProvisionalAdmissionSource.Json(ContextualProvisionalAdmissionSource.Contract)+
                ",\"request\":"+ContextualProvisionalAdmissionSource.Json(request.Fingerprint)+"}").Substring(0,32);
            var fingerprint=ContextualProvisionalAdmissionSource.Hash(attemptId+"\u001f"+request.Fingerprint+"\u001f"+
                string.Join("|",families.Select(x=>x.Key+":"+x.Value))+"\u001f"+
                string.Join("|",affect.Concat(relationship).Select(x=>x.Dimension+":"+x.OverlayBps)));
            return new ContextualProvisionalAdmissionAttempt(attemptId,request,families,affect,relationship,biases,
                Presentation(request.Proposal,affect,relationship),reasons.OrderBy(x=>x,StringComparer.Ordinal)
                    .Take(ContextualProvisionalAdmissionSource.MaximumReasonCodes),fingerprint);
        }

        public Tuple<ContextualProvisionalReaction,ObservedProvisionalAdmissionReceipt> Admit(
            ContextualAdmissionRequest request,ContextualProvisionalAdmissionAttempt attempt,long currentTick)
        {
            if(request is null||attempt is null)throw new ArgumentNullException();
            var key=Key(attempt.OwnerId,attempt.UtteranceId);
            if(_activeKey.TryGetValue(key,out var existingId))
            {
                var existing=_items[existingId]; var existingReceipt=_receipts[existing.AdmissionReceiptFingerprint];
                if(existing.AttemptId==attempt.AttemptId&&existing.AttemptFingerprint==attempt.Fingerprint&&existing.RequestFingerprint==request.Fingerprint)
                    return Tuple.Create(existing,existingReceipt);
                throw new InvalidOperationException("Conflicting active admission.");
            }
            var eventKey=Key(attempt.OwnerId,attempt.CurrentEventId);
            if(_activeEventKey.TryGetValue(eventKey,out var eventExistingId))
            {
                var existing=_items[eventExistingId];
                if(existing.AttemptId==attempt.AttemptId)return Tuple.Create(existing,_receipts[existing.AdmissionReceiptFingerprint]);
                throw new InvalidOperationException("Canonical event already active.");
            }
            var seenKey=SeenKey("utterance",attempt.OwnerId,attempt.UtteranceId);
            var seenEventKey=SeenKey("event",attempt.OwnerId,attempt.CurrentEventId);
            if(_seen.Contains(seenKey)||_seenEvent.Contains(seenEventKey))throw new InvalidOperationException("Terminal replay rejected.");
            var expected=PrepareAttempt(request);
            if(expected.Fingerprint!=attempt.Fingerprint||expected.AttemptId!=attempt.AttemptId)
                throw new ArgumentException("Attempt does not match exact request.");
            if(currentTick<request.DialogueReceipt.AdmittedTick||currentTick>=attempt.DisplayedTick+ContextualProvisionalAdmissionSource.LifetimeTicks)
                throw new InvalidOperationException("Admission outside display-anchored lifetime.");
            if(!_lastOrder.ContainsKey(attempt.OwnerId)&&_lastOrder.Count>=ContextualProvisionalAdmissionSource.MaximumTrackedOwners)
                throw new InvalidOperationException("Tracked owner bound exceeded.");
            if(ActiveCount()>=ContextualProvisionalAdmissionSource.MaximumGlobalActive)
                throw new InvalidOperationException("Global active bound exceeded.");
            var order=Tuple.Create(attempt.DisplayedTick,request.DisplayReceipt.ReceiptId);
            if(_lastOrder.TryGetValue(attempt.OwnerId,out var previous)&&
               (order.Item1<previous.Item1||(order.Item1==previous.Item1&&string.CompareOrdinal(order.Item2,previous.Item2)<=0)))
                throw new InvalidOperationException("Out-of-order admission.");
            var reactionId="contextual-reaction:"+ContextualProvisionalAdmissionSource.Hash(
                "{\"attempt\":"+ContextualProvisionalAdmissionSource.Json(attempt.AttemptId)+
                ",\"session\":"+ContextualProvisionalAdmissionSource.Json(_session)+"}").Substring(0,32);
            _claims.Claim(attempt.OwnerId,attempt.UtteranceId,ProvisionalReactionPath.ContextualV44,reactionId);
            try
            {
                _proposals.MarkVerifiedForConsume(request.ProposalAttestation,request.Proposal);
                _dialogue.Verify(request.DialogueReceipt);
                _proposals.Consume(request.ProposalAttestation);
                _dialogue.Consume(request.DialogueReceipt);
            }
            catch
            {
                _claims.Release(attempt.OwnerId,attempt.UtteranceId,ProvisionalReactionPath.ContextualV44,reactionId);
                throw;
            }
            var receiptId="contextual-admission-receipt:"+ContextualProvisionalAdmissionSource.Hash(
                reactionId+"\u001f"+attempt.AttemptId+"\u001f"+currentTick.ToString(CultureInfo.InvariantCulture)).Substring(0,32);
            var receiptFingerprint=ContextualProvisionalAdmissionSource.Hash(receiptId+"\u001f"+attempt.Fingerprint);
            var receipt=new ObservedProvisionalAdmissionReceipt(receiptId,attempt.AttemptId,reactionId,attempt.OwnerId,
                attempt.UtteranceId,currentTick,_checkpointGeneration,_checkpointFingerprint,receiptFingerprint);
            var reactionFingerprint=ContextualProvisionalAdmissionSource.Hash(reactionId+"\u001f"+receipt.Fingerprint+"\u001fPREPARED");
            var reaction=new ContextualProvisionalReaction(reactionId,receipt.Fingerprint,attempt,currentTick,
                ContextualReactionState.Prepared,reactionFingerprint);
            _items.Add(reactionId,reaction);_activeKey.Add(key,reactionId);_activeEventKey.Add(eventKey,reactionId);
            if(!_activeByOwner.TryGetValue(attempt.OwnerId,out var ownerItems)){ownerItems=new HashSet<string>(StringComparer.Ordinal);_activeByOwner.Add(attempt.OwnerId,ownerItems);}
            ownerItems.Add(reactionId);_receipts.Add(receipt.Fingerprint,receipt);_lastOrder[attempt.OwnerId]=order;
            _seen.Add(seenKey);_seenEvent.Add(seenEventKey);EnforceCapacity(attempt.OwnerId,currentTick);
            return Tuple.Create(_items.TryGetValue(reactionId,out var retained)?retained:reaction,receipt);
        }

        public ContextualProvisionalReaction Activate(string reactionId,long currentTick)
        {
            var item=Get(reactionId);
            if(item.State==ContextualReactionState.Active)return item;
            if(item.State!=ContextualReactionState.Prepared||currentTick<item.AdmittedTick||currentTick>=item.ExpiresTick)
                throw new InvalidOperationException("Reaction cannot activate.");
            var replacement=item.WithState(ContextualReactionState.Active,
                ContextualProvisionalAdmissionSource.Hash(item.ReactionId+"\u001f"+item.AdmissionReceiptFingerprint+"\u001fACTIVE"));
            _items[reactionId]=replacement;return replacement;
        }
        public int Expire(long tick){var rows=_items.Values.Where(x=>tick>=x.ExpiresTick).OrderBy(x=>x.ReactionId,StringComparer.Ordinal).ToList();foreach(var row in rows)Terminalize(row,ContextualReactionState.Expired);return rows.Count;}
        public int InvalidateCheckpoint(long generation,string fingerprint)
        {
            if(generation<0)throw new ArgumentOutOfRangeException(nameof(generation));
            ContextualProvisionalAdmissionSource.Sha(fingerprint,nameof(fingerprint));
            var rows=_items.Values.Where(x=>x.CheckpointGeneration!=generation||x.CheckpointFingerprint!=fingerprint)
                .OrderBy(x=>x.ReactionId,StringComparer.Ordinal).ToList();
            foreach(var row in rows)Terminalize(row,ContextualReactionState.Discarded);
            _checkpointGeneration=generation;_checkpointFingerprint=fingerprint;return rows.Count;
        }
        public void VerifyReceipt(ObservedProvisionalAdmissionReceipt receipt)
        {
            if(receipt is null||!_receipts.TryGetValue(receipt.Fingerprint,out var trusted)||!ReferenceEquals(trusted,receipt)||
               !_items.TryGetValue(receipt.ReactionId,out var item)||item.AdmissionReceiptFingerprint!=receipt.Fingerprint)
                throw new InvalidOperationException("Untrusted provisional admission receipt.");
        }
        public ContextualProvisionalReaction Get(string id)=>_items.TryGetValue(id,out var item)?item:throw new KeyNotFoundException(id);
        public int ActiveCount(string? owner=null)=>owner is null?_items.Count:_activeByOwner.TryGetValue(owner,out var ids)?ids.Count:0;
        public string StateDigest()=>ContextualProvisionalAdmissionSource.Hash(
            _session+"\u001f"+_save+"\u001f"+_world+"\u001f"+_stores+"\u001f"+_checkpointGeneration+"\u001f"+
            _checkpointFingerprint+"\u001f"+string.Join("|",_items.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value.Fingerprint))+
            "\u001f"+_seen.Count+"\u001f"+_seen.Digest+"\u001f"+_seenEvent.Count+"\u001f"+_seenEvent.Digest+
            "\u001f"+_terminalChain+"\u001f"+_claims.Count);
        public ContextualDiagnosticProjection DiagnosticProjection()=>new ContextualDiagnosticProjection(
            ContextualProvisionalAdmissionSource.Hash(_session),
            ContextualProvisionalAdmissionSource.Hash(_save+"\u001f"+_world+"\u001f"+_stores),_checkpointGeneration,
            ActiveCount(),_claims.Count,_seen.Count,_seenEvent.Count,
            new ReadOnlyDictionary<string,int>(new Dictionary<string,int>(_terminalCounts,StringComparer.Ordinal)),StateDigest());

        private void EnforceCapacity(string owner,long tick)
        {
            if(!_activeByOwner.TryGetValue(owner,out var ids)||ids.Count<=ContextualProvisionalAdmissionSource.MaximumActivePerOwner)return;
            var rows=ids.Select(id=>_items[id]).OrderBy(row=>tick>=row.ExpiresTick?0:1)
                .ThenBy(row=>row.ConfidenceBps).ThenBy(row=>row.CreatedTick).ThenBy(row=>row.ReactionId,StringComparer.Ordinal).ToList();
            foreach(var row in rows.Take(ids.Count-ContextualProvisionalAdmissionSource.MaximumActivePerOwner))
                Terminalize(row,tick>=row.ExpiresTick?ContextualReactionState.Expired:ContextualReactionState.Discarded);
        }
        private void Terminalize(ContextualProvisionalReaction item,ContextualReactionState state)
        {
            _items.Remove(item.ReactionId);_activeKey.Remove(Key(item.OwnerId,item.UtteranceId));
            _activeEventKey.Remove(Key(item.OwnerId,item.CurrentEventId));
            if(_activeByOwner.TryGetValue(item.OwnerId,out var ids)){ids.Remove(item.ReactionId);if(ids.Count==0)_activeByOwner.Remove(item.OwnerId);}
            _claims.Release(item.OwnerId,item.UtteranceId,ProvisionalReactionPath.ContextualV44,item.ReactionId);
            _receipts.Remove(item.AdmissionReceiptFingerprint);
            var name=state.ToString().ToUpperInvariant();_terminalCounts[name]=_terminalCounts.TryGetValue(name,out var count)?count+1:1;
            _terminalChain=ContextualProvisionalAdmissionSource.Hash(_terminalChain+"\u001f"+item.ReactionId+"\u001f"+item.OwnerId+
                "\u001f"+item.UtteranceId+"\u001f"+item.CreatedTick+"\u001f"+name);
        }
        private static List<MappedOverlayDimension> Scale(IEnumerable<GroundedAppraisalDimensionProposal> rows,int cap,int confidence)
        {
            var result=rows.Select(row=>{var adjusted=Truncate((long)row.ProposedBps*confidence,10000);
                return new MappedOverlayDimension(row.Dimension,row.ProposedBps,adjusted,Truncate((long)adjusted*cap,10000),cap);})
                .Where(row=>row.OverlayBps!=0).OrderByDescending(row=>Math.Abs(row.OverlayBps)).ThenBy(row=>row.Dimension,StringComparer.Ordinal).ToList();
            if(result.Sum(row=>Math.Abs(row.OverlayBps))>cap)throw new InvalidOperationException("Overlay shared cap exceeded.");
            return result;
        }
        private static ContextualPresentationMode Presentation(GroundedCompoundAppraisalProposal proposal,IReadOnlyList<MappedOverlayDimension> affect,IReadOnlyList<MappedOverlayDimension> relationship)
        {
            if(proposal.ConfidenceBps<3500)return ContextualPresentationMode.None;
            var familyPeak=proposal.Families.Select(x=>x.NetEvidenceBps).DefaultIfEmpty(0).Max();
            var scaled=Truncate((long)Math.Max(proposal.IntensityBps,familyPeak)*proposal.ConfidenceBps,10000);
            var overlayPeak=affect.Concat(relationship).Select(x=>Math.Abs(x.OverlayBps)*10000/x.CapBps).DefaultIfEmpty(0).Max();
            var effective=Math.Max(scaled,overlayPeak);
            return effective<2000?ContextualPresentationMode.None:effective<5500?ContextualPresentationMode.Icon:ContextualPresentationMode.Thought;
        }
        private static IEnumerable<string> Biases(string family)
        {
            switch(family){case"GRATITUDE":return new[]{"WARMTH","ATTENTION"};case"TRUST":case"PRIDE":return new[]{"WARMTH"};
                case"AFFECTION":return new[]{"WARMTH","REASSURANCE"};case"RELIEF":return new[]{"REASSURANCE"};case"RESPECT":return new[]{"ATTENTION"};
                case"SUSPICION":return new[]{"GUARDEDNESS"};case"BETRAYAL":return new[]{"GUARDEDNESS","WITHDRAWAL"};
                case"RESENTMENT":return new[]{"DEFENSIVENESS","GUARDEDNESS"};case"ANGER":return new[]{"DEFENSIVENESS"};
                case"FEAR":return new[]{"GUARDEDNESS","WITHDRAWAL"};case"HEARTBREAK":case"SHAME":case"DISTRESS":return new[]{"WITHDRAWAL"};
                case"AMBIGUITY":return new[]{"UNCERTAINTY"};default:return Array.Empty<string>();}
        }
        private static int Truncate(long numerator,int denominator)=>(int)(numerator<0?-((-numerator)/denominator):numerator/denominator);
        private static string Key(string first,string second)=>first+"\u001f"+second;
        private static byte[] SeenKey(string kind,string owner,string value)
        {using(var algorithm=SHA256.Create())return algorithm.ComputeHash(Encoding.UTF8.GetBytes(kind+"\u001f"+owner+"\u001f"+value));}
    }
}
