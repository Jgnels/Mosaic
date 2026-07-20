from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict,Iterable
from .core import canonical_hash

@dataclass(frozen=True)
class SkillRecord:
    skill_id:str;name:str;version:int;source_episode_ids:tuple[str,...]
    preconditions:tuple[str,...];postconditions:tuple[str,...]
    executor_binding:str;validation_status:str;supersedes:str|None=None
    def to_dict(self):return asdict(self)

class ProceduralSkillStore:
    def __init__(self):
        self.records:Dict[str,SkillRecord]={};self.active_by_name={};self.counter=0
    def add_version(self,name,source_episode_ids,preconditions,postconditions,executor_binding,validation_status):
        prior_id=self.active_by_name.get(name);prior=self.records.get(prior_id) if prior_id else None
        version=1 if prior is None else prior.version+1;self.counter+=1;sid=f"SK-{self.counter:06d}"
        rec=SkillRecord(sid,name,version,tuple(sorted(set(source_episode_ids))),tuple(preconditions),tuple(postconditions),
                        executor_binding,validation_status,prior_id)
        self.records[sid]=rec;self.active_by_name[name]=sid;return rec
    def active(self,name):
        sid=self.active_by_name.get(name);return self.records.get(sid) if sid else None
    def to_dict(self):
        payload={"records":{k:v.to_dict() for k,v in sorted(self.records.items())},
                 "active_by_name":dict(sorted(self.active_by_name.items()))}
        return {**payload,"hash":canonical_hash(payload)}

def run_skill_store_lab():
    store=ProceduralSkillStore()
    v1=store.add_version("OPAQUE_SEQUENCE_A",["EP1","EP2"],["HAS_X"],["STATE_Y"],"ValidatedExecutor.SequenceA.v1","VALIDATED")
    v2=store.add_version("OPAQUE_SEQUENCE_A",["EP1","EP2","EP7"],["HAS_X","CONTEXT_Z"],["STATE_Y"],"ValidatedExecutor.SequenceA.v2","VALIDATED")
    return {"experiment_id":"SL-SKILL-STORE-001","v1":v1.to_dict(),"v2":v2.to_dict(),
            "old_version_preserved":v1.skill_id in store.records,
            "active_is_v2":store.active("OPAQUE_SEQUENCE_A").skill_id==v2.skill_id,
            "supersession_valid":v2.supersedes==v1.skill_id,
            "arbitrary_code_execution_allowed":False,"store":store.to_dict()}
