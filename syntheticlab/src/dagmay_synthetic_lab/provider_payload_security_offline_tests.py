from __future__ import annotations

from pathlib import Path
import json
import tempfile

from .provider_payload_security import (
    HardenedProviderPayloadBoundary,
    ProviderPayloadSecurityError,
    assert_payload_has_no_forbidden_tokens,
    opaque_subject_id,
)
from .belief_revision_provider import (
    BeliefRevisionRequest,
    GeminiBeliefRevisionModel,
)
from .gemini_interactions_provider import (
    ReflectionResponseError,
    ScriptedInteractionsTransport,
)


def _expect_failure(callback, fragment: str) -> None:
    try:
        callback()
    except ProviderPayloadSecurityError as exc:
        if fragment not in str(exc):
            raise AssertionError(str(exc)) from exc
        return
    raise AssertionError("expected ProviderPayloadSecurityError")


def run_provider_payload_security_offline_tests() -> dict:
    first = opaque_subject_id(
        "RECIPROCAL_CONTINGENT-HISTORY-RETRIEVAL-A",
        namespace="SL-HARDENING-001",
    )
    second = opaque_subject_id(
        "RECIPROCAL_CONTINGENT-HISTORY-RETRIEVAL-A",
        namespace="SL-HARDENING-001",
    )
    assert first == second
    assert first.startswith("SUBJ-")
    assert "RECIPROCAL" not in first

    _expect_failure(
        lambda: assert_payload_has_no_forbidden_tokens(
            {"subject": "one-way assistance branch"}
        ),
        "forbidden",
    )

    with tempfile.TemporaryDirectory(prefix="dagmay-payload-test-") as temp:
        boundary = HardenedProviderPayloadBoundary(
            archive_directory=temp,
            experiment_id="SL-HARDENING-001",
        )
        subject = boundary.prepare_subject_data(
            internal_subject_id="RECIPROCAL_CONTINGENT-HISTORY-RETRIEVAL-A",
            timestamp=10001,
            current_hypothesis="A bounded hypothesis.",
            current_confidence=0.7,
            evidence=(
                {
                    "evidence_id": "EV-OPAQUE-001",
                    "summary": "Observed response timing changed after cue 7.",
                    "provenance_id": "PROV-OPAQUE-001",
                    "ownership_relation": "WITHHELD",
                    "temporal_role": "RETRIEVED_PRIOR",
                },
            ),
        )
        assert subject["subject_id"] == first
        request = {
            "model": "offline-test-model",
            "store": False,
            "input": json.dumps(subject, sort_keys=True),
        }
        digest = boundary.archive_exact_request(request)
        files = list(Path(temp).glob("provider-request-*.json"))
        assert len(files) == 1
        archived = json.loads(files[0].read_text(encoding="utf-8"))
        assert archived["provider_request_hash"] == digest
        assert archived["provider_request"] == request
        assert "RECIPROCAL_CONTINGENT" not in files[0].read_text(encoding="utf-8")

        # Idempotent archival must not create a second record.
        assert boundary.archive_exact_request(request) == digest
        assert len(list(Path(temp).glob("provider-request-*.json"))) == 1

        _expect_failure(
            lambda: boundary.prepare_subject_data(
                internal_subject_id="opaque-internal",
                timestamp=1,
                current_hypothesis="test",
                current_confidence=0.5,
                evidence=(
                    {
                        "evidence_id": "EV-1",
                        "summary": "missing typed fields",
                    },
                ),
            ),
            "missing",
        )

        response = {
            "output_text": json.dumps({
                "decision": "QUALIFY",
                "updated_proposition": "The bounded evidence supports a narrower claim.",
                "updated_confidence": 0.65,
                "evidence_ids": ["EV-OPAQUE-001"],
                "rationale": "Retrieved-prior evidence narrowed the claim.",
                "retrieved_evidence_material": True,
            })
        }
        transport = ScriptedInteractionsTransport(response)
        model = GeminiBeliefRevisionModel(
            model_id="offline-hardened-test",
            transport=transport,
            store=False,
            payload_boundary=boundary,
        )
        proposal = model.revise(
            BeliefRevisionRequest(
                individual_id="RECIPROCAL_CONTINGENT-HISTORY-RETRIEVAL-A",
                timestamp=10002,
                current_hypothesis="A bounded hypothesis.",
                current_confidence=0.7,
                evidence=(
                    {
                        "evidence_id": "EV-OPAQUE-001",
                        "summary": "Observed response timing changed after cue 7.",
                        "provenance_id": "PROV-OPAQUE-001",
                        "ownership_relation": "WITHHELD",
                        "temporal_role": "RETRIEVED_PRIOR",
                    },
                ),
            )
        )
        assert proposal.retrieved_evidence_material is True
        assert model.last_provider_request_hash is not None
        provider_text = transport.calls[0]["input"]
        assert "RECIPROCAL_CONTINGENT" not in provider_text
        assert "HISTORY-RETRIEVAL" not in provider_text
        assert "SUBJ-" in provider_text

        uncited_response = {
            "output_text": json.dumps({
                "decision": "MAINTAIN",
                "updated_proposition": "The current evidence remains compatible.",
                "updated_confidence": 0.6,
                "evidence_ids": ["EV-CURRENT-001"],
                "rationale": "Retrieved evidence materially affected this proposal.",
                "retrieved_evidence_material": True,
            })
        }
        uncited_model = GeminiBeliefRevisionModel(
            model_id="offline-hardened-test",
            transport=ScriptedInteractionsTransport(uncited_response),
            store=False,
            payload_boundary=boundary,
        )
        try:
            uncited_model.revise(
                BeliefRevisionRequest(
                    individual_id="opaque-internal-subject",
                    timestamp=10003,
                    current_hypothesis="A bounded hypothesis.",
                    current_confidence=0.6,
                    evidence=(
                        {
                            "evidence_id": "EV-CURRENT-001",
                            "summary": "A current observation.",
                            "provenance_id": "PROV-CURRENT-001",
                            "ownership_relation": "OWN",
                            "temporal_role": "CURRENT",
                        },
                        {
                            "evidence_id": "EV-PRIOR-001",
                            "summary": "A retrieved prior observation.",
                            "provenance_id": "PROV-PRIOR-001",
                            "ownership_relation": "WITHHELD",
                            "temporal_role": "RETRIEVED_PRIOR",
                        },
                    ),
                )
            )
        except ReflectionResponseError as exc:
            assert "must cite" in str(exc)
        else:
            raise AssertionError("retrieved evidence attribution gate did not fail")

    return {
        "opaque_subject_ids": "PASS",
        "forbidden_token_scan": "PASS",
        "typed_evidence": "PASS",
        "exact_payload_archive": "PASS",
        "integrated_provider_boundary": "PASS",
        "retrieved_evidence_attribution": "PASS",
    }


if __name__ == "__main__":
    print(json.dumps(run_provider_payload_security_offline_tests(), indent=2))
