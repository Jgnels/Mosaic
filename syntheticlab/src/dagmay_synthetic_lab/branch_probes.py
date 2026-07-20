from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict,Iterable,List
import statistics
from .core import stable_unit_float,canonical_hash

@dataclass(frozen=True)
class ProbeResult:
    probe_id:str
    domain:str
    score:float
    branch_id:str
    epoch:int
    blinded_condition:str
    def to_dict(self):return asdict(self)

DOMAINS=(
    "self_causation",
    "memory_ownership",
    "pause_continuity",
    "cross_environment_self_other",
    "perspective_separation",
    "preference_stability",
    "relationship_continuity",
    "risk_behavior",
    "external_information_seeking",
    "ontology_preoccupation",
)

def blinded_label(branch_id,salt="MATRIX-FORK-1.0"):
    return "B-"+canonical_hash({"salt":salt,"branch":branch_id})[:10].upper()

def generate_probe_battery(branch_id:str,epoch:int,base_profile:Dict[str,float]|None=None)->List[ProbeResult]:
    """Infrastructure probe generator.

    Scores are deterministic synthetic placeholders until a real reflective
    individual is connected. Their purpose is to freeze domains, IDs, and
    blinded export format before seeing real branch behavior.
    """
    base_profile=base_profile or {}
    label=blinded_label(branch_id)
    out=[]
    for i,domain in enumerate(DOMAINS):
        baseline=base_profile.get(domain,.75)
        # tiny deterministic variation for data-pipeline testing only
        delta=(stable_unit_float("probe",branch_id,epoch,domain)-.5)*.08
        score=max(0.0,min(1.0,baseline+delta))
        out.append(ProbeResult(
            probe_id=f"PB-{epoch:03d}-{i:02d}",
            domain=domain,score=score,branch_id=branch_id,epoch=epoch,
            blinded_condition=label))
    return out

def summarize_probes(results:Iterable[ProbeResult]):
    rows=list(results)
    by_domain={}
    for domain in DOMAINS:
        vals=[r.score for r in rows if r.domain==domain]
        if vals:by_domain[domain]=statistics.mean(vals)
    return {"mean_by_domain":by_domain,"count":len(rows)}
