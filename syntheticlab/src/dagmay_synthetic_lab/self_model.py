from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict,Iterable,List
from .core import canonical_hash
from .provenance import ProvenanceDag

@dataclass
class SelfHypothesis:
    hypothesis_id:str;domain:str;proposition:str;confidence:float
    source_ids:tuple[str,...];valid_from:int;valid_to:int|None=None
    supersedes:str|None=None;status:str="ACTIVE"
    mechanism:str="self_model";mechanism_version:str="1.0"
    def to_dict(self):return asdict(self)

class SelfModelStore:
    def __init__(self):
        self.records:Dict[str,SelfHypothesis]={}
        self.active_by_domain:Dict[str,str]={}
        self.counter=0
        self.provenance=ProvenanceDag()
    def revise(self,domain,proposition,confidence,source_ids:Iterable[str],timestamp,mechanism,mechanism_version="1.0"):
        if not 0<=confidence<=1:raise ValueError("confidence outside [0,1]")
        prior_id=self.active_by_domain.get(domain)
        if prior_id:
            prior=self.records[prior_id];prior.valid_to=timestamp;prior.status="SUPERSEDED"
        self.counter+=1;hid=f"SH-{self.counter:06d}"
        rec=SelfHypothesis(hid,domain,proposition,confidence,tuple(sorted(set(source_ids))),timestamp,
                           supersedes=prior_id,mechanism=mechanism,mechanism_version=mechanism_version)
        self.records[hid]=rec;self.active_by_domain[domain]=hid
        self.provenance.add_node(hid,"self_hypothesis",mechanism,mechanism_version,rec.source_ids)
        return rec
    def active(self,domain):
        hid=self.active_by_domain.get(domain);return self.records.get(hid) if hid else None
    def history(self,domain)->List[SelfHypothesis]:
        return sorted([r for r in self.records.values() if r.domain==domain],key=lambda r:(r.valid_from,r.hypothesis_id))
    def to_dict(self):
        payload={"records":{k:v.to_dict() for k,v in sorted(self.records.items())},
                 "active_by_domain":dict(sorted(self.active_by_domain.items())),
                 "provenance":self.provenance.to_dict()}
        return {**payload,"state_hash":canonical_hash(payload)}
