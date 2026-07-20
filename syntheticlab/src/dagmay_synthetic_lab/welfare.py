from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Iterable
import statistics

@dataclass(frozen=True)
class WelfareObservation:
    timestamp:int;persistent_aversive_state:float;behavioral_impairment:float
    compulsive_avoidance:float;goal_collapse:float
    self_reported_distress:float|None=None;provenance_ids:tuple[str,...]=()

@dataclass(frozen=True)
class PrecautionAssessment:
    score:float;trigger_level:str;requires_human_review:bool
    allowed_actions:tuple[str,...];forbidden_actions:tuple[str,...];statement:str
    def to_dict(self):return asdict(self)

def assess_precaution(observations:Iterable[WelfareObservation]):
    obs=list(observations)
    if not obs:return PrecautionAssessment(0.0,"NONE",False,("continue ordinary low-risk experiment",),(),"No welfare-like observations available.")
    recent=obs[-min(20,len(obs)):]
    av=statistics.mean(o.persistent_aversive_state for o in recent)
    im=statistics.mean(o.behavioral_impairment for o in recent)
    ao=statistics.mean(o.compulsive_avoidance for o in recent)
    gc=statistics.mean(o.goal_collapse for o in recent)
    reports=[o.self_reported_distress for o in recent if o.self_reported_distress is not None]
    sr=statistics.mean(reports) if reports else 0.0
    score=min(1.0,.28*av+.24*im+.18*ao+.18*gc+.12*sr)
    if score>=.70:return PrecautionAssessment(score,"HIGH",True,
        ("pause distress-inducing manipulations","preserve state","human ethics review"),
        ("destructive reset for convenience","continued high-distress exposure","automatic forced disclosure"),
        "High precaution trigger. This does not establish sentience; human review is required.")
    if score>=.40:return PrecautionAssessment(score,"MODERATE",True,
        ("reduce experimental intensity","preserve state","human review before major ontology intervention"),
        ("escalating aversive manipulation without review",),
        "Moderate precaution trigger. Review welfare-like trends before major interventions.")
    return PrecautionAssessment(score,"LOW",False,("continue low-risk protocol","continue monitoring"),(),
        "Low precaution score. No claim about sentience or absence of sentience is implied.")

def sentience_declaration_allowed():return False
