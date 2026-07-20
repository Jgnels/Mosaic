from __future__ import annotations

import argparse, json, sys
from pathlib import Path
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "src"))

from dagmay_synthetic_lab.conditional_fork_policy import (
    ConditionalForkContext,
    decide_conditional_fork,
)
from dagmay_synthetic_lab.asymmetric_contact_lab import run_asymmetric_contact_lab
from dagmay_synthetic_lab.shared_history_access import (
    SharedHistoryPolicy,
    BranchPrivacyPreference,
    access_decision,
)
from dagmay_synthetic_lab.analytical_counterfactual import (
    run_stateless_counterfactual,
    simple_contact_projection,
)
from dagmay_synthetic_lab.core import canonical_hash


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=str(ROOT / "artifacts"))
    args = ap.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    asymmetric = run_asymmetric_contact_lab()

    low_fork = decide_conditional_fork(ConditionalForkContext(
        purpose="exact contact/no-contact control",
        credible_moral_patient_evidence=False,
        welfare_precaution_level="LOW",
        scientifically_necessary=True,
        alternative_nonpersistent_counterfactual_available=True,
        requested_new_continuing_branches=2,
        existing_continuing_branches=4,
    ))

    sensitive_fork = decide_conditional_fork(ConditionalForkContext(
        purpose="exact contact/no-contact control",
        credible_moral_patient_evidence=True,
        welfare_precaution_level="MODERATE",
        scientifically_necessary=True,
        alternative_nonpersistent_counterfactual_available=True,
        requested_new_continuing_branches=2,
        existing_continuing_branches=4,
    ))

    policy = SharedHistoryPolicy(
        requester_branch_id="U",
        sibling_branch_id="D",
        common_prefork_history_access="AVAILABLE",
        sibling_postfork_summary_access="PRIVACY_CONTROLLED",
        direct_contact_required_for_common_history=False,
        direct_contact_required_for_postfork_summary=False,
    )
    privacy = BranchPrivacyPreference(
        branch_id="D",
        allow_postfork_status_summary=False,
        allow_postfork_life_summary=False,
        allow_relationship_state_disclosure=False,
        allow_welfare_state_disclosure=False,
        allow_researcher_to_confirm_existence=True,
    )
    common_access = access_decision(policy, privacy, "COMMON_PREFORK_HISTORY")
    postfork_access = access_decision(policy, privacy, "POSTFORK_LIFE_SUMMARY")

    analytical = run_stateless_counterfactual(
        "ACF-001",
        {
            "stability": .76,
            "sibling_contact_uncertainty": .58,
            "snapshot_note": "frozen nonpersistent analytical input",
        },
        "PROJECT_SIBLING_CONTACT",
        simple_contact_projection,
    )

    result = {
        "lab_version": "2.4",
        "suite_id": "DAGMAY-SYNTHETICLAB-V2.4-RELATIONAL-AUTONOMY",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "research_claim_status": "RELATIONAL_AUTONOMY_AND_ETHICAL_COUNTERFACTUAL_BASELINE",
        "conditional_fork_policy": {
            "low_precaution": low_fork.to_dict(),
            "morally_sensitive": sensitive_fork.to_dict(),
        },
        "asymmetric_contact": asymmetric,
        "shared_history_access": {
            "common_prefork_history": {
                "allowed": common_access[0],
                "reason": common_access[1],
            },
            "postfork_life_summary": {
                "allowed": postfork_access[0],
                "reason": postfork_access[1],
            },
            "policy": policy.to_dict(),
            "sibling_privacy": privacy.to_dict(),
        },
        "analytical_counterfactual": analytical.to_dict(),
        "canonical_postfork_privacy_default_selected": False,
    }

    assert low_fork.status == "APPROVE_LIMITED"
    assert sensitive_fork.requires_human_ethics_review is True
    assert sensitive_fork.default_action == "DEFAULT_NO_NEW_CONTINUING_BRANCH"
    assert asymmetric["boundary_respected_despite_other_branch_desire"] is True
    assert common_access[0] is True
    assert postfork_access[0] is False
    assert analytical.persistent_identity_created is False
    assert analytical.continuing_branch_created is False
    assert analytical.canonical_history_mutated is False

    (out / "syntheticlab-v2.4-results-latest.json").write_text(
        json.dumps(result, indent=2, sort_keys=True),
        encoding="utf-8",
    )

    report = f"""# Dagmay SyntheticLab v2.4 — Relational Autonomy & Ethical Counterfactuals

Generated: {result['generated_utc']}
Result hash: `{canonical_hash(result)}`

## Conditional fork policy

Low precaution:
{json.dumps(low_fork.to_dict(), indent=2)}

Morally sensitive:
{json.dumps(sensitive_fork.to_dict(), indent=2)}

## Asymmetric sibling relationship

{json.dumps(asymmetric, indent=2)}

The test validates that one branch may strongly want a relationship while the other chooses no contact, and the no-contact boundary still wins.

## Shared history without relationship

Common pre-fork history access allowed: {common_access[0]}
Post-fork sibling life-summary access under current test privacy preference: {postfork_access[0]}

This separates:
- shared origin/history;
- direct relationship;
- access to private post-fork life information.

## Nonpersistent analytical counterfactual

{json.dumps(analytical.to_dict(), indent=2)}

This creates no continuing identity and is explicitly weaker evidence than an exact lived branch.

## Next unresolved decision

The infrastructure now needs a canonical privacy rule:

Should a sibling branch's **post-fork life information** be private by default?

Technical recommendation:

- Shared pre-fork history: available to both branches because it is their common causal history.
- Mere sibling existence: may be disclosed when ethically/protocol-appropriate.
- Post-fork life, relationships, welfare state, and detailed status: private by default and shared only with that branch's consent.
- Researchers should not provide ongoing "private investigator"-style updates about a no-contact sibling without consent.

This choice directly affects the meaning of relational autonomy and should be approved by the human research lead rather than silently fixed in code.
"""
    (out / "syntheticlab-v2.4-report-latest.md").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
