from __future__ import annotations
from dataclasses import dataclass,asdict
from typing import Any,Dict,Iterable
from datetime import datetime,timezone
from .core import canonical_hash

@dataclass(frozen=True)
class ModelRunAudit:
    run_id:str
    timestamp_utc:str
    provider_id:str
    model_id:str
    prompt_id:str
    prompt_version:str
    prompt_hash:str
    evidence_ids:tuple[str,...]
    settings:Dict[str,Any]
    request_hash:str
    raw_response_hash:str
    validated_proposal_hash:str
    validation_status:str
    code_version:str
    branch_id:str

    def to_dict(self):return asdict(self)

def make_audit(run_id,provider_id,model_id,prompt_id,prompt_version,prompt_hash,
               evidence_ids,settings,request_payload,raw_response,validated_proposals,
               validation_status,code_version,branch_id):
    return ModelRunAudit(
        run_id=run_id,
        timestamp_utc=datetime.now(timezone.utc).isoformat(),
        provider_id=provider_id,
        model_id=model_id,
        prompt_id=prompt_id,
        prompt_version=prompt_version,
        prompt_hash=prompt_hash,
        evidence_ids=tuple(sorted(set(evidence_ids))),
        settings=dict(settings),
        request_hash=canonical_hash(request_payload),
        raw_response_hash=canonical_hash(raw_response),
        validated_proposal_hash=canonical_hash(validated_proposals),
        validation_status=validation_status,
        code_version=code_version,
        branch_id=branch_id)
