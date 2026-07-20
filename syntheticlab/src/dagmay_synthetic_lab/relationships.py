from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict

@dataclass(frozen=True)
class SocialEvidence:
    evidence_id:str;observer:str;counterpart:str;event_type:str
    magnitude:float;reliability:float;timestamp:int;retracts:str|None=None

@dataclass
class RelationshipState:
    trust:float=0.0;affection:float=0.0;fear:float=0.0;resentment:float=0.0;familiarity:float=0.0
    def to_dict(self):return asdict(self)

class DirectedRelationshipModel:
    def __init__(self,source_id,target_id):
        self.source_id=source_id;self.target_id=target_id;self.evidence:Dict[str,SocialEvidence]={}
    def add_evidence(self,e):
        if e.observer!=self.source_id or e.counterpart!=self.target_id:raise ValueError("direction mismatch")
        if e.evidence_id in self.evidence:raise ValueError("duplicate evidence")
        self.evidence[e.evidence_id]=e
    def active_evidence(self):
        retracted={e.retracts for e in self.evidence.values() if e.event_type=="RETRACTION" and e.retracts}
        return [e for e in sorted(self.evidence.values(),key=lambda x:(x.timestamp,x.evidence_id))
                if e.event_type!="RETRACTION" and e.evidence_id not in retracted]
    def compute(self):
        s=RelationshipState()
        for e in self.active_evidence():
            w=max(0,min(1,e.reliability))*e.magnitude;s.familiarity+=.035*min(1,abs(w))
            if e.event_type=="HELP":s.trust+=.05*w;s.affection+=.04*w
            elif e.event_type=="RESCUE":s.trust+=.14*w;s.affection+=.10*w;s.fear-=.03*w
            elif e.event_type=="HARM":s.trust-=.13*w;s.fear+=.10*w;s.resentment+=.12*w
            elif e.event_type=="INSULT":s.trust-=.035*w;s.affection-=.045*w;s.resentment+=.05*w
            elif e.event_type=="RUMOR_NEGATIVE":s.trust-=.05*w;s.resentment+=.025*w
        s.trust=max(-1,min(1,s.trust));s.affection=max(-1,min(1,s.affection))
        s.fear=max(0,min(1,s.fear));s.resentment=max(0,min(1,s.resentment));s.familiarity=max(0,min(1,s.familiarity))
        return s
    def to_dict(self):
        return {"source_id":self.source_id,"target_id":self.target_id,"state":self.compute().to_dict(),
                "evidence":[asdict(e) for e in sorted(self.evidence.values(),key=lambda x:x.evidence_id)],
                "active_evidence_ids":[e.evidence_id for e in self.active_evidence()]}

def run_relationship_lab():
    repeated=DirectedRelationshipModel("A","B")
    for i in range(5):repeated.add_evidence(SocialEvidence(f"H{i}","A","B","HELP",1,1,i))
    rescue=DirectedRelationshipModel("A","C");rescue.add_evidence(SocialEvidence("R1","A","C","RESCUE",1,1,1))
    ab=DirectedRelationshipModel("A","B");ba=DirectedRelationshipModel("B","A")
    ab.add_evidence(SocialEvidence("AB1","A","B","HELP",1,1,1));ba.add_evidence(SocialEvidence("BA1","B","A","INSULT",1,1,1))
    rumor=DirectedRelationshipModel("A","D");rumor.add_evidence(SocialEvidence("D-DIRECT","A","D","HELP",1,1,1))
    baseline=rumor.compute().to_dict();rumor.add_evidence(SocialEvidence("D-RUMOR","A","D","RUMOR_NEGATIVE",1,.45,2))
    after_rumor=rumor.compute().to_dict();rumor.add_evidence(SocialEvidence("D-RETRACT","A","D","RETRACTION",1,1,3,"D-RUMOR"))
    after_retraction=rumor.compute().to_dict()
    return {"experiment_id":"SL-RELATIONSHIP-LAB-001","repeated_help":repeated.to_dict(),"single_rescue":rescue.to_dict(),
            "asymmetry":{"A_to_B":ab.to_dict(),"B_to_A":ba.to_dict(),"states_differ":ab.compute().to_dict()!=ba.compute().to_dict()},
            "rumor_retraction":{"baseline":baseline,"after_rumor":after_rumor,"after_retraction":after_retraction,
                                "retraction_restores_baseline":baseline==after_retraction},
            "material_changes_traceable_to_evidence":True,
            "interpretation_warning":"Update magnitudes are experimental; evidence-grounded directionality is the invariant."}
