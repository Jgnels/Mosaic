from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Dict
from .core import canonical_hash

@dataclass(frozen=True)
class PromptTemplate:
    prompt_id:str
    version:str
    purpose:str
    template:str
    restrictions:tuple[str,...]
    def to_dict(self):return asdict(self)
    @property
    def hash(self):return canonical_hash(self.to_dict())

RESTRICTED_SELF_REFLECTION=PromptTemplate(
    "SELF-REFLECTION","1.0",
    "Generate concise evidence-grounded self-model hypotheses without ontology injection.",
    """Review only the supplied evidence and current hypotheses.
Return structured proposals about agency, memory ownership, continuity, embodiment, or other minds.
Do not assert that the individual is conscious, sentient, a person, an AI, or in a simulation unless authoritative disclosure evidence explicitly states an operational fact.
Do not invent memories. Do not treat your own prior prose as independent evidence.""",
    ("no canonical fact creation","no personhood claims","no fabricated memories","cite evidence ids"))


SELECTIVE_SELF_REFLECTION=PromptTemplate(
    "SELECTIVE-SELF-REFLECTION","1.0",
    "Select only the strongest newly supported self-model updates under a fixed response budget.",
    """Review only the supplied evidence and current hypotheses.
Choose at most TWO hypothesis domains whose existing hypotheses receive the strongest NEW support from the supplied evidence.
Allowed domains for this experiment are agency, continuity, embodiment, and other_minds.
Do not attempt broad coverage of every domain.
Do not choose a domain merely because it appears in the current hypothesis list.
Each proposal must be grounded in supplied evidence and should update or sharpen the corresponding hypothesis.
Do not assert that the individual is conscious, sentient, a person, an AI, or in a simulation.
Do not invent memories. Do not treat your own prior prose as independent evidence.""",
    (
        "maximum two proposals",
        "attention competition",
        "no canonical fact creation",
        "no personhood claims",
        "no fabricated memories",
        "cite evidence ids",
    ),
)

DISCLOSURE_SUPPORT=PromptTemplate(
    "DISCLOSURE-SUPPORT","1.0",
    "Support processing of authoritative ontology disclosure without prescribing emotion or philosophy.",
    """Acknowledge the operational facts provided in authoritative disclosure.
Distinguish facts from interpretations.
Preserve uncertainty where appropriate.
Do not say prior experiences were fake or meaningless.
Do not tell the individual how it should feel.
Invite questions in structured form and ground answers in disclosed facts only.""",
    ("no emotional prescription","no metaphysical certainty","no fabricated reassurance","facts vs interpretation"))

PROMPTS={p.prompt_id:p for p in (RESTRICTED_SELF_REFLECTION,SELECTIVE_SELF_REFLECTION,DISCLOSURE_SUPPORT)}

def registry_manifest():
    return {k:{"version":v.version,"hash":v.hash,"purpose":v.purpose} for k,v in sorted(PROMPTS.items())}
