from __future__ import annotations
from dataclasses import dataclass
from typing import List,Set
import statistics
from .core import stable_unit_float

@dataclass(frozen=True)
class MicroEvent:
    index:int;context:str;goal:str;participant:str;prediction_error:float;true_episode:int

def generate_stream(seed,episodes=24):
    stream=[];index=0
    for ep in range(episodes):
        length=4+int(stable_unit_float("seg-len",seed,ep)*8)
        context=f"C{ep%4}";goal=f"G{(ep//2)%3}";participant=f"P{(ep*3)%5}"
        for j in range(length):
            pe=.15+.2*stable_unit_float("seg-pe",seed,ep,j)
            if j==0 and ep>0:pe=.85+.1*stable_unit_float("seg-boundary",seed,ep)
            stream.append(MicroEvent(index,context,goal,participant,pe,ep));index+=1
    return stream

def true_boundaries(stream):
    return {i for i in range(1,len(stream)) if stream[i].true_episode!=stream[i-1].true_episode}

def fixed_window(stream,window=7):return set(range(window,len(stream),window))

def context_rule(stream):
    out=set()
    for i in range(1,len(stream)):
        a,b=stream[i-1],stream[i]
        if a.context!=b.context or a.goal!=b.goal or a.participant!=b.participant:out.add(i)
    return out

def prediction_error_rule(stream,threshold=.72):
    return {i for i,e in enumerate(stream) if i>0 and e.prediction_error>=threshold}

def hybrid_rule(stream):return context_rule(stream)|prediction_error_rule(stream,.80)

def f1(pred:Set[int],truth:Set[int]):
    if not pred and not truth:return 1.0
    tp=len(pred&truth);precision=tp/len(pred) if pred else 0;recall=tp/len(truth) if truth else 0
    return 0 if precision+recall==0 else 2*precision*recall/(precision+recall)

def run_segmentation_lab(seed_count=64):
    methods={"fixed_window":fixed_window,"context_rule":context_rule,"prediction_error":prediction_error_rule,"hybrid":hybrid_rule}
    scores={m:[] for m in methods}
    for seed in range(1,seed_count+1):
        stream=generate_stream(seed);truth=true_boundaries(stream)
        for name,fn in methods.items():scores[name].append(f1(fn(stream),truth))
    return {"experiment_id":"SL-SEGMENTATION-LAB-001","seed_count":seed_count,
            "mean_boundary_f1":{n:statistics.mean(v) for n,v in scores.items()},
            "winner_selected_for_production":None,
            "interpretation_warning":"Synthetic boundary ground truth is not human event-perception ground truth."}
