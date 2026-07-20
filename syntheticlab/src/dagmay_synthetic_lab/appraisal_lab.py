from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict
from .core import canonical_hash

@dataclass(frozen=True)
class AppraisalInput:
    event_id:str;goal_congruence:float;expectedness:float;controllability:float
    agency_self:float;other_benefit:float;social_significance:float
    certainty:float;need_urgency:float;competence:float

@dataclass(frozen=True)
class AppraisalResult:
    engine:str;engine_version:str;evidence_ids:tuple[str,...]
    deltas:Dict[str,float];labels:tuple[str,...];rationale:tuple[str,...]
    def to_dict(self): return asdict(self)

def clip(x):return max(-1.0,min(1.0,x))

class DagmayBaseline:
    engine="DAGMAY_BASELINE";version="1.0"
    def appraise(self,x):
        surprise=1-x.expectedness
        d={"valence":clip(.7*x.goal_congruence+.2*x.other_benefit),
           "arousal":clip(.55*surprise+.35*x.need_urgency),
           "threat_safety":clip(-.7*x.goal_congruence-.3*x.controllability),
           "agency_control":clip(.8*x.controllability+.2*x.agency_self-.5),
           "attachment_affiliation":clip(.6*x.other_benefit*x.social_significance),
           "certainty_confusion":clip(x.certainty-surprise),
           "social_standing":clip(.4*x.goal_congruence*x.social_significance)}
        return AppraisalResult(self.engine,self.version,(x.event_id,),d,(),("continuous bounded affect proposal",))

class GamygdalaInspired:
    engine="GAMYGDALA_INSPIRED";version="1.0"
    def appraise(self,x):
        desirability=x.goal_congruence
        likelihood_change=(1-x.expectedness)*(1 if desirability>=0 else -1)
        intensity=clip(desirability*(.5+.5*abs(likelihood_change)))
        labels=[]
        if intensity>.2:labels.append("JOY_LIKE")
        elif intensity<-.2:labels.append("DISTRESS_LIKE")
        if x.other_benefit>.35 and x.social_significance>.4:labels.append("GRATITUDE_LIKE")
        d={"valence":intensity,"arousal":clip(abs(intensity)*(1.2-x.expectedness)),
           "threat_safety":clip(-min(0.0,intensity)),"agency_control":clip(x.controllability-.5),
           "attachment_affiliation":clip(x.other_benefit*x.social_significance),
           "certainty_confusion":clip(x.certainty-(1-x.expectedness)),"social_standing":0.0}
        return AppraisalResult(self.engine,self.version,(x.event_id,),d,tuple(labels),("goal-congruence desirability baseline",))

class FatimaInspired:
    engine="FATIMA_INSPIRED";version="1.0"
    def appraise(self,x):
        desirability=x.goal_congruence;unexpectedness=1-x.expectedness
        praise=x.other_benefit*x.social_significance;labels=[]
        if desirability>.3:labels.append("JOY_LIKE")
        if desirability<-.3:labels.append("DISTRESS_LIKE")
        if praise>.3:labels.append("ADMIRATION_OR_GRATITUDE_LIKE")
        if praise<-.3:labels.append("REPROACH_OR_ANGER_LIKE")
        d={"valence":clip(.65*desirability+.2*praise),"arousal":clip(.6*unexpectedness+.25*abs(desirability)),
           "threat_safety":clip(-.6*desirability-.25*x.controllability),
           "agency_control":clip(x.controllability+.25*x.agency_self-.6),
           "attachment_affiliation":clip(.7*praise),"certainty_confusion":clip(x.certainty-unexpectedness),
           "social_standing":clip(.45*praise)}
        return AppraisalResult(self.engine,self.version,(x.event_id,),d,tuple(labels),("rule-variable appraisal",))

class PsiInspired:
    engine="PSI_INSPIRED";version="1.0"
    def appraise(self,x):
        surprise=1-x.expectedness
        d={"valence":clip(.55*x.goal_congruence+.25*x.competence-.2*x.need_urgency),
           "arousal":clip(.55*x.need_urgency+.45*surprise),
           "threat_safety":clip(.5*x.need_urgency+.4*surprise-.55*x.controllability),
           "agency_control":clip(x.competence+x.controllability-1.0),
           "attachment_affiliation":clip(.25*x.other_benefit*x.social_significance),
           "certainty_confusion":clip(x.certainty-.7*surprise),
           "social_standing":clip(.15*x.social_significance*x.goal_congruence)}
        labels=("HIGH_UNEXPECTEDNESS",) if surprise>.65 else ()
        return AppraisalResult(self.engine,self.version,(x.event_id,),d,labels,("need urgency, competence, unexpectedness modulation",))

ENGINES=(DagmayBaseline(),GamygdalaInspired(),FatimaInspired(),PsiInspired())

def run_appraisal_lab():
    scenarios=[
        AppraisalInput("AP-E1",.8,.2,.4,.8,.9,.9,.8,.4,.5),
        AppraisalInput("AP-E2",-.9,.1,.2,.2,-.8,.8,.5,.9,.2),
        AppraisalInput("AP-E3",.2,.95,.9,.8,0,.1,.95,.2,.9),
        AppraisalInput("AP-E4",-.2,.4,.7,.7,.7,.8,.7,.3,.8)]
    outputs={}
    for e in ENGINES:
        rs=[e.appraise(s) for s in scenarios]
        for r in rs:
            assert all(-1<=v<=1 for v in r.deltas.values()) and r.evidence_ids
        outputs[e.engine]=[r.to_dict() for r in rs]
    return {"experiment_id":"SL-APPRAISAL-LAB-001","engines":[e.engine for e in ENGINES],
            "outputs":outputs,"fingerprints":{k:canonical_hash(v) for k,v in outputs.items()},
            "all_engines_deterministic_and_bounded":True,"winner_selected":None,
            "interpretation_warning":"No engine is declared psychologically correct; later behavioral validation is required."}
