from __future__ import annotations
from dataclasses import dataclass
from typing import Iterable
import math, statistics
from .core import DeterministicRng, stable_unit_float, canonical_hash
from .causal_world import OPAQUE_CONTEXTS, OPAQUE_ACTIONS
from .drives import get_profile

@dataclass
class ActiveState:
    need: float = 0.35
    cumulative_success: int = 0
    steps: int = 0
    def tick(self):
        self.need=min(1.0,self.need+0.025);self.steps+=1
    def apply_success(self,success:int):
        if success:
            self.need=max(0.0,self.need-0.22);self.cumulative_success+=1

class ActiveCausalModel:
    def __init__(self):
        self.successes={(c,a):0 for c in OPAQUE_CONTEXTS for a in OPAQUE_ACTIONS}
        self.trials={(c,a):0 for c in OPAQUE_CONTEXTS for a in OPAQUE_ACTIONS}
        self.learning_progress={(c,a):0.0 for c in OPAQUE_CONTEXTS for a in OPAQUE_ACTIONS}
    def p_success(self,c,a):
        s=self.successes[(c,a)];n=self.trials[(c,a)]
        return (s+1)/(n+2)
    def uncertainty(self,c,a):
        return 1.0/math.sqrt(self.trials[(c,a)]+1.0)
    def observe(self,c,a,outcome):
        p_before=self.p_success(c,a);err_before=abs(outcome-p_before)
        self.trials[(c,a)]+=1;self.successes[(c,a)]+=int(outcome)
        p_after=self.p_success(c,a);err_after=abs(outcome-p_after)
        progress=max(0.0,err_before-err_after)
        self.learning_progress[(c,a)]=0.8*self.learning_progress[(c,a)]+0.2*progress
    def best_action(self,c):
        return max(sorted(OPAQUE_ACTIONS),key=lambda a:self.p_success(c,a))
    def accuracy(self,mapping):
        return statistics.mean(1.0 if self.best_action(c)==mapping[c] else 0.0 for c in OPAQUE_CONTEXTS)
    def to_dict(self):
        return {
            "successes":{f"{c}|{a}":v for (c,a),v in sorted(self.successes.items())},
            "trials":{f"{c}|{a}":v for (c,a),v in sorted(self.trials.items())},
            "learning_progress":{f"{c}|{a}":v for (c,a),v in sorted(self.learning_progress.items())},
        }

class ActiveOpaqueWorld:
    def __init__(self,seed,noise=0.08):
        self.seed=seed;self.noise=noise
        rng=DeterministicRng(seed);pool=list(OPAQUE_ACTIONS);self.mapping={}
        for c in OPAQUE_CONTEXTS:
            rng.shuffle(pool);self.mapping[c]=pool[0]
    def context_at(self,step):
        idx=int(stable_unit_float("ctx",self.seed,step)*len(OPAQUE_CONTEXTS))
        return OPAQUE_CONTEXTS[min(idx,len(OPAQUE_CONTEXTS)-1)]
    def outcome(self,c,a,step):
        p=(1-self.noise) if self.mapping[c]==a else self.noise
        return int(stable_unit_float("active-outcome",self.seed,c,a,step)<p)

def drive_weights(profile_id):
    result={"homeostasis":0.0,"uncertainty":0.0,"competence":0.0}
    for d in get_profile(profile_id).drives:
        if d.drive_id=="HOMEOSTATIC_REGULATION": result["homeostasis"]=d.initial_weight
        elif d.drive_id=="UNCERTAINTY_REDUCTION": result["uncertainty"]=d.initial_weight
        elif d.drive_id=="COMPETENCE_PROGRESS": result["competence"]=d.initial_weight
    return result

def choose_action(profile_id,state,model,context,step,seed):
    if profile_id=="NONE": return sorted(OPAQUE_ACTIONS)[0]
    w=drive_weights(profile_id);scores={}
    for a in OPAQUE_ACTIONS:
        p=model.p_success(context,a)
        homeo=state.need*p
        epistemic=model.uncertainty(context,a)
        competence=model.learning_progress[(context,a)]+0.25*epistemic
        score=w["homeostasis"]*homeo+w["uncertainty"]*epistemic+w["competence"]*competence
        score+=1e-9*stable_unit_float("tie-break",seed,profile_id,context,a,step)
        scores[a]=score
    return max(sorted(OPAQUE_ACTIONS),key=lambda a:scores[a])

def _entropy(counts:Iterable[int]):
    counts=list(counts);total=sum(counts)
    if total==0:return 0.0
    out=0.0
    for count in counts:
        if count:
            p=count/total;out-=p*math.log(p,2)
    return out

def run_active_individual(seed,profile_id,steps=800):
    world=ActiveOpaqueWorld(seed);state=ActiveState();model=ActiveCausalModel()
    need_trace=[];action_counts={a:0 for a in OPAQUE_ACTIONS}
    for step in range(steps):
        state.tick();c=world.context_at(step)
        a=choose_action(profile_id,state,model,c,step,seed);action_counts[a]+=1
        o=world.outcome(c,a,step);model.observe(c,a,o);state.apply_success(o);need_trace.append(state.need)
    return {
        "seed":seed,"profile_id":profile_id,"steps":steps,
        "causal_model_accuracy":model.accuracy(world.mapping),
        "mean_need":statistics.mean(need_trace),
        "need_p95":sorted(need_trace)[int(0.95*(len(need_trace)-1))],
        "success_rate":state.cumulative_success/steps,
        "action_entropy":_entropy(action_counts.values()),
        "action_counts":action_counts,
        "state_hash":canonical_hash({"state":state.__dict__,"model":model.to_dict(),"action_counts":action_counts})
    }

def run_active_cohort(seed_count=64):
    profiles=["BALANCED_MINIMAL","MINIMAL_REGULATION","EPISTEMIC_MINIMAL","NONE"]
    rows={p:[run_active_individual(s,p) for s in range(1,seed_count+1)] for p in profiles}
    summary={}
    for p,items in rows.items():
        summary[p]={
            "causal_model_accuracy_mean":statistics.mean(x["causal_model_accuracy"] for x in items),
            "mean_need_mean":statistics.mean(x["mean_need"] for x in items),
            "need_p95_mean":statistics.mean(x["need_p95"] for x in items),
            "success_rate_mean":statistics.mean(x["success_rate"] for x in items),
            "action_entropy_mean":statistics.mean(x["action_entropy"] for x in items),
        }
    return {
        "experiment_id":"SL-ACTIVE-COHORT-001",
        "status":"CONFIRMATORY_ENGINEERING_BASELINE",
        "canonical_profile":"BALANCED_MINIMAL",
        "ablation_profiles":["MINIMAL_REGULATION","EPISTEMIC_MINIMAL","NONE"],
        "seed_count":seed_count,"summary":summary,"rows":rows,
        "interpretation_warning":"Seed-drive differences are researcher-selected architectural treatments, not emergent drives."
    }
