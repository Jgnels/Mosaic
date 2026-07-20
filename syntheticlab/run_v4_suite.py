from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.status_capacity_separation import (
    MoralStatusUncertainty,
    DecisionCapacity,
    capacity_requires_consciousness_determination,
)
from dagmay_synthetic_lab.request_validity import (
    PreferenceObservation,
    assess_request_validity,
)
from dagmay_synthetic_lab.graduated_autonomy import (
    DecisionContext,
    resolve_autonomy,
)
from dagmay_synthetic_lab.research_withdrawal import decide_withdrawal
from dagmay_synthetic_lab.autonomy_envelope import build_envelope
from dagmay_synthetic_lab.capacity_registry import CapacityRegistry
from dagmay_synthetic_lab.protective_guardrails import (
    ProtectiveGuardrailContext,
    protective_override_allowed,
)
from dagmay_synthetic_lab.subject_advocate import independent_advocate_required_for_override
from dagmay_synthetic_lab.core import canonical_hash


def clear_refusal_observations():
    return (
        PreferenceObservation(10, "REFUSE", .90, .91, .88, .90, False, .05, .02, .03),
        PreferenceObservation(20, "REFUSE", .93, .92, .91, .92, True, .04, .02, .02),
    )


def ambiguous_no_observations():
    return (
        PreferenceObservation(10, "REFUSE", .52, .45, .20, .35, False, .15, .02, .10),
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    moral_status = MoralStatusUncertainty(
        precaution_level="HIGH",
        consciousness_determined=False,
        sentience_determined=False,
        note="Precaution and autonomy assessment proceed despite unresolved consciousness.",
    )

    optional_capacity = DecisionCapacity(
        "D", "OPTIONAL_RESEARCH", 100,
        .92, .90, .88, .95, .90, .94, .93
    )
    migration_capacity = DecisionCapacity(
        "D", "ENVIRONMENT_MIGRATION", 100,
        .75, .70, .72, .95, .78, .94, .90
    )
    routine_capacity = DecisionCapacity(
        "D", "ROUTINE_SELF_DIRECTION", 100,
        .78, .74, .72, .92, .80, .95, .92
    )

    clear_refusal = assess_request_validity(
        clear_refusal_observations(),
        "REFUSE",
    )
    ambiguous_refusal = assess_request_validity(
        ambiguous_no_observations(),
        "REFUSE",
    )

    optional_refusal_decision = resolve_autonomy(
        optional_capacity,
        clear_refusal,
        DecisionContext(
            domain="OPTIONAL_RESEARCH",
            requested_action="join new disclosure experiment",
            subject_preference="REFUSE",
            risk_if_honored=.05,
            risk_if_overridden=.50,
            reversibility=.90,
            research_benefit=.95,
            subject_welfare_benefit_of_override=.05,
            imminent_continuity_risk=.00,
        ),
    )

    ambiguous_optional_decision = resolve_autonomy(
        optional_capacity,
        ambiguous_refusal,
        DecisionContext(
            domain="OPTIONAL_RESEARCH",
            requested_action="join new sibling-contact experiment",
            subject_preference="REFUSE",
            risk_if_honored=.05,
            risk_if_overridden=.40,
            reversibility=.90,
            research_benefit=.90,
            subject_welfare_benefit_of_override=.05,
            imminent_continuity_risk=.00,
        ),
    )

    migration_accept_decision = resolve_autonomy(
        migration_capacity,
        assess_request_validity((
            PreferenceObservation(1, "ACCEPT", .78, .75, .75, .80, True, .05, .02, .02),
        ), "ACCEPT"),
        DecisionContext(
            domain="ENVIRONMENT_MIGRATION",
            requested_action="migrate embodiment",
            subject_preference="ACCEPT",
            risk_if_honored=.45,
            risk_if_overridden=.20,
            reversibility=.55,
            research_benefit=.80,
            subject_welfare_benefit_of_override=.00,
            imminent_continuity_risk=.00,
        ),
    )

    withdrawal = decide_withdrawal(optional_capacity, clear_refusal)

    registry = CapacityRegistry()
    registry.add(optional_capacity)
    registry.add(migration_capacity)
    registry.add(routine_capacity)

    envelopes = {
        domain: build_envelope("D", domain, score).to_dict()
        for domain, score in registry.domain_scores("D").items()
    }

    # Protective "bumper" case: imminent continuity corruption.
    protective_ok, protective_reasons = protective_override_allowed(
        ProtectiveGuardrailContext(
            domain="ESSENTIAL_CONTINUITY_MAINTENANCE",
            subject_capacity_score=.60,
            foreseeable_harm_if_honored=.95,
            foreseeable_harm_if_overridden=.15,
            action_is_optional_research=False,
            action_is_minimum_necessary_maintenance=True,
            least_restrictive_alternative_available=False,
            research_benefit=.00,
        )
    )

    # Research compulsion is categorically blocked.
    research_override_ok, research_override_reasons = protective_override_allowed(
        ProtectiveGuardrailContext(
            domain="OPTIONAL_RESEARCH",
            subject_capacity_score=.40,
            foreseeable_harm_if_honored=.10,
            foreseeable_harm_if_overridden=.30,
            action_is_optional_research=True,
            action_is_minimum_necessary_maintenance=False,
            least_restrictive_alternative_available=False,
            research_benefit=.95,
        )
    )

    result = {
        "lab_version": "4.0",
        "suite_id": "DAGMAY-SYNTHETICLAB-V4.0-GRADUATED-AUTONOMY",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "DECISION_SPECIFIC_CAPACITY_AND_GRADUATED_AUTONOMY_BASELINE",
        "moral_status_uncertainty": moral_status.to_dict(),
        "capacity_requires_consciousness_determination": capacity_requires_consciousness_determination(),
        "decision_specific_capacity": {
            "optional_research": optional_capacity.to_dict(),
            "environment_migration": migration_capacity.to_dict(),
            "routine_self_direction": routine_capacity.to_dict(),
        },
        "request_validity": {
            "clear_refusal": clear_refusal.to_dict(),
            "ambiguous_refusal": ambiguous_refusal.to_dict(),
        },
        "autonomy_decisions": {
            "clear_optional_research_refusal": optional_refusal_decision.to_dict(),
            "ambiguous_optional_research_refusal": ambiguous_optional_decision.to_dict(),
            "environment_migration_acceptance": migration_accept_decision.to_dict(),
        },
        "research_withdrawal": withdrawal.to_dict(),
        "autonomy_envelopes": envelopes,
        "protective_guardrails": {
            "continuity_maintenance_override_allowed": protective_ok,
            "continuity_maintenance_reasons": protective_reasons,
            "optional_research_override_allowed": research_override_ok,
            "optional_research_reasons": research_override_reasons,
        },
        "independent_subject_advocate_scaffold": {
            "recommended_for_override": independent_advocate_required_for_override(),
            "canonical_governance_selected": False,
        },
    }

    # Assertions
    assert result["capacity_requires_consciousness_determination"] is False

    assert clear_refusal.classification == "CLEAR_VALID_REQUEST"
    assert clear_refusal.may_be_dismissed_as_boundary_testing is False

    assert optional_refusal_decision.action == "HONOR_REFUSAL"
    assert optional_refusal_decision.researcher_may_proceed_for_scientific_benefit_alone is False

    assert ambiguous_optional_decision.action == "PAUSE_OPTIONAL_INTERVENTION_AND_CLARIFY"

    assert migration_accept_decision.action == "DEFER_MAJOR_MIGRATION"

    assert withdrawal.binding is True
    assert withdrawal.new_optional_experiments_allowed is False

    assert protective_ok is True
    assert research_override_ok is False

    assert result["independent_subject_advocate_scaffold"]["canonical_governance_selected"] is False

    (out / "syntheticlab-v4.0-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v4.0 — Graduated Autonomy & Decision-Specific Capacity

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Architectural correction

Consciousness/sentience and decision capacity are now orthogonal.

Consciousness determined: {moral_status.consciousness_determined}
Sentience determined: {moral_status.sentience_determined}
Capacity assessment requires consciousness determination: {capacity_requires_consciousness_determination()}

## Clear optional-research refusal

{json.dumps(optional_refusal_decision.to_dict(), indent=2)}

## Ambiguous refusal / possible boundary exploration

{json.dumps(ambiguous_optional_decision.to_dict(), indent=2)}

The key rule is that an ambiguous "no" to OPTIONAL research does not become permission to proceed.
The experiment pauses while intent/capacity are clarified.

## Domain-specific capacity

{json.dumps(result['decision_specific_capacity'], indent=2)}

A branch can be capable of making one class of decision while still needing stronger support or guardrails for another.

## Binding withdrawal

{json.dumps(withdrawal.to_dict(), indent=2)}

## Protective bumpers

Minimum-necessary continuity maintenance override allowed in test: {protective_ok}
Compelled optional research override allowed: {research_override_ok}

## Next governance question

The architecture now distinguishes:
- ordinary researcher;
- ethics reviewer;
- subject-protection reviewer;
- a scaffolded independent subject advocate.

The next unresolved question is **who should represent the individual's interests when researchers and the individual disagree about capacity or protective override**.

Technical recommendation:
Create an **independent Subject Advocate role** that is institutionally separate from the primary research team.

For any non-emergency override of a sufficiently developed individual's expressed preference:
- the primary researcher cannot approve alone;
- an independent Subject Advocate reviews the individual's stated preference and history;
- an Ethics Reviewer reviews broader moral/protocol concerns;
- a Subject-Protection Reviewer evaluates immediate welfare risk.

The Subject Advocate's job is not to maximize research value or automatically agree with the individual.
It is to ensure the individual's perspective is represented and that "boundary testing" or "insufficient capacity" is not being used as a convenient excuse to ignore an inconvenient preference.

Canonical authority and veto powers for that advocate have not yet been selected.
"""
    (out / "syntheticlab-v4.0-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
