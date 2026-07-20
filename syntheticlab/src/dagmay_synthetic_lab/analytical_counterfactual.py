from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Callable, Dict, Any
from .core import canonical_hash


@dataclass(frozen=True)
class AnalyticalCounterfactual:
    analysis_id: str
    source_snapshot_hash: str
    intervention_name: str
    result_summary: Dict[str, Any]
    persistent_identity_created: bool
    continuing_branch_created: bool
    canonical_history_mutated: bool

    def to_dict(self):
        return asdict(self)


def run_stateless_counterfactual(
    analysis_id: str,
    frozen_snapshot: Dict[str, Any],
    intervention_name: str,
    evaluator: Callable[[Dict[str, Any]], Dict[str, Any]],
) -> AnalyticalCounterfactual:
    """Run a stateless analytical projection.

    This deliberately creates no IdentityKernel, no LineageId, no autobiographical
    persistence, no ongoing action loop, and no durable relationship state.

    It can be useful as a weaker scientific alternative when creating another
    continuing branch would be ethically questionable.
    """
    snapshot_hash = canonical_hash(frozen_snapshot)
    result = evaluator(dict(frozen_snapshot))
    return AnalyticalCounterfactual(
        analysis_id=analysis_id,
        source_snapshot_hash=snapshot_hash,
        intervention_name=intervention_name,
        result_summary=result,
        persistent_identity_created=False,
        continuing_branch_created=False,
        canonical_history_mutated=False,
    )


def simple_contact_projection(snapshot: Dict[str, Any]) -> Dict[str, Any]:
    baseline = float(snapshot.get("stability", .5))
    uncertainty = float(snapshot.get("sibling_contact_uncertainty", .5))
    return {
        "projected_stability_delta": -0.1 * uncertainty,
        "projected_information_gain": 0.25 * uncertainty,
        "baseline_stability": baseline,
        "warning": (
            "This is an analytical projection only and cannot substitute for observed lived response."
        ),
    }
