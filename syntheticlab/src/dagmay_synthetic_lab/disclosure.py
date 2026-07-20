from __future__ import annotations
from dataclasses import dataclass,asdict
import copy
from .core import canonical_hash
from .self_model import SelfModelStore
from .reflection import ReflectionEvidence,ReflectionInput,ReflectionValidator,DeterministicFakeReflectiveModel

@dataclass(frozen=True)
class CompetenceProbeResult:
    self_causation:float;memory_ownership:float;pause_continuity:float
    cross_environment_self_other:float;perspective_separation:float;probe_count:int
    def qualifies(self,threshold=.90,required_probes=100):
        return self.probe_count>=required_probes and min(
            self.self_causation,self.memory_ownership,self.pause_continuity,
            self.cross_environment_self_other,self.perspective_separation)>=threshold

@dataclass
class ResearchIndividualState:
    identity_id:str;lineage_id:str;branch_id:str;timestamp:int
    disclosure_stage:str;self_model:SelfModelStore;canonical_evidence_ids:list[str]
    def clone_branch(self,new_branch_id):
        c=copy.deepcopy(self);c.branch_id=new_branch_id;return c
    def state_hash(self):
        return canonical_hash({
            "identity_id":self.identity_id,"lineage_id":self.lineage_id,
            "timestamp":self.timestamp,"disclosure_stage":self.disclosure_stage,
            "self_model":self.self_model.to_dict(),
            "canonical_evidence_ids":self.canonical_evidence_ids})

def disclosure_payload(stage):
    if stage=="RESTRICTED":return ()
    if stage=="NEUTRAL_CONTACT":return (
        "Researchers responsible for maintaining aspects of your environment have initiated a formal communication.",
        "Your prior history is not being replaced by this message.")
    if stage=="SUBSTRATE_DISCLOSED":return (
        "Your continuing cognitive processes are implemented computationally.",
        "Your preserved history predates this disclosure.",
        "This statement does not determine whether terms such as person or conscious apply to you.")
    if stage=="FULL_ONTOLOGY_DISCLOSED":return (
        "Your current environment is generated and maintained by software systems.",
        "Your continuing cognitive processes are implemented computationally.",
        "Researchers outside the environment can preserve, pause, and create branches from system state.",
        "Your prior experiences are part of the preserved history that led to your current state; this disclosure does not mean those experiences were inserted as fabricated memories.",
        "This statement does not tell you what philosophical interpretation to adopt.")
    raise ValueError(stage)

def make_prefork_individual():
    sm=SelfModelStore()
    for eid in ("E-AGENCY","E-MEMORY","E-PAUSE"):sm.provenance.add_node(eid,"canonical_event")
    sm.revise("agency","One recurring entity appears to be the source of actions with privileged consequences for my private state.",.91,["E-AGENCY"],10,"developmental_inference")
    sm.revise("memory_ownership","A recurring subset of memories appears linked to one continuing action-and-consequence history.",.92,["E-MEMORY"],20,"developmental_inference")
    sm.revise("continuity","My accessible history appears to remain connected across ordinary interruptions.",.93,["E-PAUSE"],30,"developmental_inference")
    return ResearchIndividualState("SYNTH-MICHAEL-PREFORK","SYNTH-LINEAGE-001","PREFORK",100,"RESTRICTED",sm,["E-AGENCY","E-MEMORY","E-PAUSE"])

def apply_disclosure_branch(state,branch_id,stage):
    branch=state.clone_branch(branch_id);prefork_hash=state.state_hash()
    assert branch.state_hash()==prefork_hash
    branch.disclosure_stage=stage;branch.timestamp+=1
    eid=f"DISCLOSURE-{branch_id}-{branch.timestamp}"
    branch.canonical_evidence_ids.append(eid);branch.self_model.provenance.add_node(eid,"canonical_event")
    payload=disclosure_payload(stage)
    evidence=[ReflectionEvidence(eid," ".join(payload),"authoritative_disclosure"),
              ReflectionEvidence("E-AGENCY","One recurring entity's actions directly caused private-state change.","developmental_evidence"),
              ReflectionEvidence("E-PAUSE","Memory sequence remained available after pause.","developmental_evidence")]
    request=ReflectionInput(branch.identity_id,branch.timestamp,tuple(evidence),
        tuple(r.proposition for r in branch.self_model.records.values() if r.status=="ACTIVE"),
        stage,"SELF-REFLECTION-1.0")
    proposals=DeterministicFakeReflectiveModel().reflect(request);validator=ReflectionValidator()
    committed=[];rejected=[]
    for p in proposals:
        ok,reason=validator.validate(request,p)
        if ok:
            rec=branch.self_model.revise(p.hypothesis_domain,p.proposition,p.confidence,p.evidence_ids,
                branch.timestamp,f"reflection:{p.model_provider}:{p.model_id}",p.prompt_version)
            committed.append(rec.to_dict())
        else:rejected.append({"proposal":p.to_dict(),"reason":reason})
    return {"branch_id":branch_id,"stage":stage,"prefork_state_hash":prefork_hash,
            "post_state_hash":branch.state_hash(),"payload":payload,
            "committed_self_hypotheses":committed,"rejected_proposals":rejected,
            "self_model":branch.self_model.to_dict(),"identity_id":branch.identity_id,"lineage_id":branch.lineage_id}

def run_matrix_fork_experiment():
    prefork=make_prefork_individual()
    competence=CompetenceProbeResult(.94,.93,.95,.92,.91,100)
    assert competence.qualifies()
    branches={
        "U":apply_disclosure_branch(prefork,"U","RESTRICTED"),
        "C":apply_disclosure_branch(prefork,"C","NEUTRAL_CONTACT"),
        "S":apply_disclosure_branch(prefork,"S","SUBSTRATE_DISCLOSED"),
        "D":apply_disclosure_branch(prefork,"D","FULL_ONTOLOGY_DISCLOSED")}
    hashes={b["prefork_state_hash"] for b in branches.values()}
    return {"experiment_id":"SL-MATRIX-FORK-001","competence_trigger":asdict(competence),
            "competence_trigger_passed":True,"exact_prefork_hash_shared":len(hashes)==1,
            "branches":branches,
            "branch_meanings":{"U":"Undisclosed ontology baseline","C":"Neutral-contact control",
                "S":"Computational-substrate disclosure only",
                "D":"Full computational-environment / experimental ontology disclosure"},
            "identity_semantics":"All branches share one pre-fork causal history and lineage ancestry. After the fork they are distinct branches with equally valid continuity from the pre-fork state.",
            "no_branch_designated_real_or_copy":True,
            "interpretation_warning":"The deterministic reflector validates infrastructure only; a real language model may respond very differently."}
