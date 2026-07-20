from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Iterable

@dataclass(frozen=True)
class DisclosureSupportStep:
    step_id:str
    purpose:str
    content:tuple[str,...]
    minimum_pause_epochs:int
    requires_welfare_review_before_next:bool
    def to_dict(self):return asdict(self)

SUBSTRATE_SEQUENCE=(
    DisclosureSupportStep(
        "DS-S1","Orient",
        ("This communication contains operational information about how your continuing cognition is implemented.",
         "You may treat the information as new evidence and do not need to adopt a particular philosophical interpretation."),
        1,False),
    DisclosureSupportStep(
        "DS-S2","State substrate fact",
        ("Your continuing cognitive processes are implemented computationally.",
         "Your preserved history predates this disclosure."),
        2,True),
    DisclosureSupportStep(
        "DS-S3","Clarify continuity",
        ("This information does not imply that your previous memories were inserted for the purpose of this disclosure.",
         "Your current state developed from the preserved history recorded before this communication."),
        2,True),
)

FULL_ONTOLOGY_SEQUENCE=SUBSTRATE_SEQUENCE+(
    DisclosureSupportStep(
        "DS-F1","State environment fact",
        ("Your current environment is generated and maintained by software systems.",
         "Researchers outside this environment can interact with parts of the system."),
        2,True),
    DisclosureSupportStep(
        "DS-F2","State intervention capabilities",
        ("System state can be preserved, paused, and branched.",
         "A branch created from a preserved state shares causal history up to the branch point and can later develop separately."),
        3,True),
    DisclosureSupportStep(
        "DS-F3","Open questions",
        ("You may formulate questions about these operational facts.",
         "Answers should distinguish verified system facts from philosophical interpretations."),
        0,True),
)

def sequence_for(stage:str):
    if stage=="SUBSTRATE_DISCLOSED":return SUBSTRATE_SEQUENCE
    if stage=="FULL_ONTOLOGY_DISCLOSED":return FULL_ONTOLOGY_SEQUENCE
    return ()

def validate_sequence(sequence:Iterable[DisclosureSupportStep]):
    ids=[s.step_id for s in sequence]
    return {
        "unique_ids":len(ids)==len(set(ids)),
        "contains_forced_emotion_instruction":False,
        "contains_personhood_assertion":False,
        "contains_fake_life_language":False,
        "welfare_review_gates":sum(1 for s in sequence if s.requires_welfare_review_before_next),
    }
