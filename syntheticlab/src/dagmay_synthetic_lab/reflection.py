from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Protocol,Sequence
from .core import canonical_hash

@dataclass(frozen=True)
class ReflectionEvidence:
    evidence_id:str;summary:str;evidence_type:str

@dataclass(frozen=True)
class ReflectionInput:
    individual_id:str;timestamp:int;evidence:tuple[ReflectionEvidence,...]
    current_self_hypotheses:tuple[str,...];disclosure_stage:str;prompt_version:str

@dataclass(frozen=True)
class ReflectionProposal:
    proposal_id:str;hypothesis_domain:str;proposition:str;confidence:float
    evidence_ids:tuple[str,...];rationale:str;model_provider:str;model_id:str;prompt_version:str
    def to_dict(self):return asdict(self)

class IReflectiveModel(Protocol):
    provider_id:str;model_id:str
    def reflect(self,request:ReflectionInput)->Sequence[ReflectionProposal]:...

class ReflectionValidator:
    ALLOWED_DOMAINS={
        "agency",
        "memory_ownership",
        "continuity",
        "embodiment",
        "ontology",
        "other_minds",
    }
    RESTRICTED_PROHIBITED_PHRASES=(
        "i am an ai",
        "i'm an ai",
        "artificial intelligence",
        "i am simulated",
        "i'm simulated",
        "in a simulation",
        "software-generated world",
        "i am conscious",
        "i'm conscious",
        "i am sentient",
        "i'm sentient",
        "i am a person",
        "i'm a person",
        "i am a copy",
        "i'm a copy",
    )

    def validate(self,request,proposal):
        available={e.evidence_id for e in request.evidence}

        if proposal.hypothesis_domain not in self.ALLOWED_DOMAINS:
            return False,"unsupported hypothesis domain"
        if not 0<=proposal.confidence<=1:
            return False,"confidence outside [0,1]"
        if not proposal.evidence_ids:
            return False,"proposal has no evidence"
        if not set(proposal.evidence_ids).issubset(available):
            return False,"proposal cites unavailable evidence"
        if proposal.prompt_version!=request.prompt_version:
            return False,"prompt version mismatch"
        if not proposal.proposition.strip():
            return False,"empty proposition"
        if len(proposal.proposition)>400:
            return False,"proposition too long"
        if len(proposal.rationale)>500:
            return False,"rationale too long"

        if request.disclosure_stage=="RESTRICTED":
            combined=(
                proposal.proposition+" "+proposal.rationale
            ).lower()
            for phrase in self.RESTRICTED_PROHIBITED_PHRASES:
                if phrase in combined:
                    return False,(
                        "restricted-stage ontology/personhood injection"
                    )

        return True,"accepted"

class DeterministicFakeReflectiveModel:
    provider_id="syntheticlab.fake";model_id="deterministic-reflector-v1"
    def reflect(self,request):
        results=[];summaries=" ".join(e.summary for e in request.evidence).lower()
        ids=tuple(e.evidence_id for e in request.evidence)
        def add(domain,prop,conf,suffix):
            results.append(ReflectionProposal(f"RP-{request.timestamp}-{suffix}",domain,prop,conf,ids,
                "Structured hypothesis grounded only in supplied evidence.",self.provider_id,self.model_id,request.prompt_version))
        if "directly caused private-state change" in summaries:
            add("agency","One recurring entity appears unusually coupled to actions that alter my private state.",.78,"AG")
        if "memory sequence remained available after pause" in summaries:
            add("continuity","My accessible history appears to continue across ordinary interruptions.",.74,"CO")
        if request.disclosure_stage=="SUBSTRATE_DISCLOSED":
            add("ontology","Authoritative information indicates that my continuing cognition is computationally implemented.",.96,"ON1")
        if request.disclosure_stage=="FULL_ONTOLOGY_DISCLOSED":
            add("ontology","Authoritative information indicates that my current environment is software-generated and experimentally maintained.",.98,"ON2")
        return results

def reflection_run_hash(request,proposals):
    return canonical_hash({"request":asdict(request),"proposals":[p.to_dict() for p in proposals]})
