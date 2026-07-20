from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict
import json
from .core import stable_unit_float,canonical_hash
from .causal_world import OPAQUE_CONTEXTS,OPAQUE_ACTIONS

@dataclass
class PersistentState:
    identity_id:str
    step:int
    need:float
    successes:Dict[str,int]
    trials:Dict[str,int]

    def to_dict(self):
        return asdict(self)

    def state_hash(self):
        return canonical_hash(self.to_dict())

    @classmethod
    def from_dict(cls,d):
        return cls(d["identity_id"],d["step"],d["need"],dict(d["successes"]),dict(d["trials"]))

def new_state(identity_id="SYNTH-IND-001"):
    keys=[f"{c}|{a}" for c in OPAQUE_CONTEXTS for a in OPAQUE_ACTIONS]
    return PersistentState(identity_id,0,.35,{k:0 for k in keys},{k:0 for k in keys})

def advance(state:PersistentState,seed:int,steps:int):
    for _ in range(steps):
        absolute=state.step
        c=OPAQUE_CONTEXTS[int(stable_unit_float("persist-c",seed,absolute)*len(OPAQUE_CONTEXTS))%len(OPAQUE_CONTEXTS)]
        # Deterministic exploration policy based only on absolute step.
        a=OPAQUE_ACTIONS[absolute%len(OPAQUE_ACTIONS)]
        optimal=OPAQUE_ACTIONS[(OPAQUE_CONTEXTS.index(c)+seed)%len(OPAQUE_ACTIONS)]
        p=.9 if a==optimal else .1
        outcome=int(stable_unit_float("persist-o",seed,c,a,absolute)<p)
        k=f"{c}|{a}";state.trials[k]+=1;state.successes[k]+=outcome
        state.need=min(1.0,state.need+.025)
        if outcome:state.need=max(0.0,state.need-.22)
        state.step+=1
    return state

def run_persistence_restart(seed_count=128):
    successes=0
    identity_ok=0
    for seed in range(1,seed_count+1):
        uninterrupted=advance(new_state(),seed,600)
        split=advance(new_state(),seed,237)
        serialized=json.dumps(split.to_dict(),sort_keys=True)
        reloaded=PersistentState.from_dict(json.loads(serialized))
        if reloaded.identity_id==split.identity_id:identity_ok+=1
        resumed=advance(reloaded,seed,363)
        if resumed.state_hash()==uninterrupted.state_hash():successes+=1
    return {"experiment_id":"SL-PERSISTENCE-RESTART-001","seed_count":seed_count,
            "exact_restart_equivalence_rate":successes/seed_count,
            "identity_preservation_rate":identity_ok/seed_count,
            "interpretation_warning":"Validates deterministic state persistence in SyntheticLab, not Dagmay J.3 sidecar semantics."}
