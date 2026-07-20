from __future__ import annotations
from dataclasses import dataclass
from typing import List
import statistics
from .core import DeterministicRng,stable_unit_float

@dataclass(frozen=True)
class Transition:
    regime:int;context:str;action:str;outcome:int;significance:float

class EWMAAgent:
    def __init__(self,alpha=.15):
        self.alpha=alpha;self.q={(c,a):.5 for c in ("X1","X2") for a in ("A1","A2")}
    def observe(self,t):
        k=(t.context,t.action);self.q[k]+=self.alpha*(t.outcome-self.q[k])
    def choose(self,c):return max(("A1","A2"),key=lambda a:self.q[(c,a)])

def make_stream(seed,n_per_regime=120):
    rng=DeterministicRng(seed^0x4444);out=[]
    mappings=[{"X1":"A1","X2":"A2"},{"X1":"A2","X2":"A1"},{"X1":"A1","X2":"A2"}]
    for regime,mapping in enumerate(mappings):
        for i in range(n_per_regime):
            c=("X1","X2")[rng.randrange(2)];a=("A1","A2")[rng.randrange(2)]
            p=.9 if a==mapping[c] else .1;o=int(stable_unit_float("replay",seed,regime,c,a,i)<p)
            out.append(Transition(regime,c,a,o,abs(o-.5)))
    return out

def replay_buffer(policy,seen,size,seed):
    if policy=="none" or not seen:return []
    if policy=="recent":return seen[-size:]
    if policy=="significant":return sorted(seen,key=lambda t:t.significance,reverse=True)[:size]
    if policy=="reservoir":
        rng=DeterministicRng(seed^0x9999);buf=[]
        for i,item in enumerate(seen):
            if i<size:buf.append(item)
            else:
                j=rng.randrange(i+1)
                if j<size:buf[j]=item
        return buf
    if policy=="dual":
        half=max(1,size//2);candidates=seen[-half:]+sorted(seen,key=lambda t:t.significance,reverse=True)[:half]
        out=[];keys=set()
        for t in candidates:
            k=(t.regime,t.context,t.action,t.outcome)
            if k not in keys:out.append(t);keys.add(k)
        return out[:size]
    raise ValueError(policy)

def run_policy(seed,policy):
    stream=make_stream(seed);agent=EWMAAgent();seen=[];current=-1
    for t in stream:
        if current!=-1 and t.regime!=current:
            for r in replay_buffer(policy,seen,40,seed+t.regime):agent.observe(r)
        current=t.regime;agent.observe(t);seen.append(t)
    final={"X1":"A1","X2":"A2"};middle={"X1":"A2","X2":"A1"}
    return {"final_accuracy":statistics.mean(1.0 if agent.choose(c)==a else 0.0 for c,a in final.items()),
            "middle_regime_retention":statistics.mean(1.0 if agent.choose(c)==a else 0.0 for c,a in middle.items())}

def run_replay_lab(seed_count=64):
    policies=("none","recent","significant","reservoir","dual")
    rows={p:[run_policy(s,p) for s in range(1,seed_count+1)] for p in policies}
    return {"experiment_id":"SL-REPLAY-LAB-001","seed_count":seed_count,
            "summary":{p:{"final_accuracy_mean":statistics.mean(x["final_accuracy"] for x in items),
                          "middle_regime_retention_mean":statistics.mean(x["middle_regime_retention"] for x in items)}
                       for p,items in rows.items()},
            "winner_selected_for_production":None,
            "interpretation_warning":"Replay policy is a developmental intervention with a stability/plasticity tradeoff."}
