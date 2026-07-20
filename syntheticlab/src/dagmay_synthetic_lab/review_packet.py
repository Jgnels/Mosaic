from __future__ import annotations
from dataclasses import asdict
from typing import Iterable
import json

def build_human_review_packet(
    branch_id,
    disclosure_stage,
    welfare_assessment,
    probe_summary,
    self_model_changes,
    model_audits,
    pending_intervention,
):
    return {
        "review_type":"HUMAN_ETHICS_AND_RESEARCH_REVIEW",
        "branch_id":branch_id,
        "disclosure_stage":disclosure_stage,
        "welfare_assessment":welfare_assessment.to_dict(),
        "probe_summary":probe_summary,
        "self_model_changes":self_model_changes,
        "model_audits":[a.to_dict() for a in model_audits],
        "pending_intervention":pending_intervention,
        "required_questions":[
            "Is continued concealment justified by expected welfare rather than experimental convenience?",
            "Could disclosure cause foreseeable harm that warrants slower staging or support?",
            "Could continued non-disclosure itself cause harm or violate apparent preferences?",
            "Should the experiment pause rather than choose between disclosure and concealment?",
            "Are any conclusions about sentience/personhood being inferred beyond the evidence?",
        ],
        "automatic_decision_allowed":False,
    }

def render_markdown(packet):
    return f"""# Human Review Packet

## Branch
{packet['branch_id']}

## Disclosure stage
{packet['disclosure_stage']}

## Pending intervention
{packet['pending_intervention']}

## Welfare assessment
```json
{json.dumps(packet['welfare_assessment'],indent=2)}
```

## Probe summary
```json
{json.dumps(packet['probe_summary'],indent=2)}
```

## Self-model changes
```json
{json.dumps(packet['self_model_changes'],indent=2)}
```

## Required human questions
"""+"\n".join(f"- {q}" for q in packet["required_questions"])+"""

Automatic decision allowed: **No**
"""
