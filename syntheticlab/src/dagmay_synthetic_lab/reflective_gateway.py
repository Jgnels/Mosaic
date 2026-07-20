from __future__ import annotations
from dataclasses import asdict
from typing import Sequence
from .reflection import ReflectionInput,ReflectionProposal,ReflectionValidator,IReflectiveModel
from .prompt_registry import PROMPTS
from .provider_audit import make_audit

class ReflectiveGateway:
    """Provider-neutral structured reflection boundary.

    The gateway logs model/provider/prompt/evidence versions and returns only
    validated proposals. Raw hidden chain-of-thought is neither requested nor stored.
    """
    def __init__(self,model:IReflectiveModel,code_version="syntheticlab-1.7"):
        self.model=model;self.code_version=code_version;self.validator=ReflectionValidator()

    def run(self,request:ReflectionInput,prompt_id:str,branch_id:str,settings=None):
        settings=settings or {}
        prompt=PROMPTS[prompt_id]
        if request.prompt_version!=prompt.version:
            raise ValueError("request prompt version does not match frozen registry")
        proposals=list(self.model.reflect(request))
        accepted=[];rejected=[]
        for p in proposals:
            ok,reason=self.validator.validate(request,p)
            (accepted if ok else rejected).append(p if ok else {"proposal":p.to_dict(),"reason":reason})
        audit=make_audit(
            run_id=f"{branch_id}-{request.timestamp}-{prompt_id}",
            provider_id=self.model.provider_id,
            model_id=self.model.model_id,
            prompt_id=prompt.prompt_id,
            prompt_version=prompt.version,
            prompt_hash=prompt.hash,
            evidence_ids=[e.evidence_id for e in request.evidence],
            settings=settings,
            request_payload=asdict(request),
            raw_response=[p.to_dict() for p in proposals],
            validated_proposals=[p.to_dict() for p in accepted],
            validation_status="ACCEPTED" if accepted and not rejected else ("PARTIAL" if accepted else "REJECTED"),
            code_version=self.code_version,
            branch_id=branch_id)
        return {"accepted":accepted,"rejected":rejected,"audit":audit}
