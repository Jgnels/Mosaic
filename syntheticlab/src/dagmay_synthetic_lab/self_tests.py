from __future__ import annotations
from .experiments import run_novel_causality,run_fork_divergence,run_agency_coupling,run_suite
from .advanced_experiments import (
    run_reflection_confound,run_temporal_order,run_false_belief,
    run_embodiment_migration_precursor,run_provenance_overlap,run_advanced_suite)
from .beliefs import run_rumor_retraction_scenario
from .cohort import run_passive_factorial_cohort
from .drives import PROFILES
from .active_development import run_active_cohort,run_active_individual
from .appraisal_lab import run_appraisal_lab
from .relationships import run_relationship_lab
from .segmentation import run_segmentation_lab
from .replay_lab import run_replay_lab
from .skills import run_skill_store_lab
from .core import canonical_hash

def run_self_tests():
    p=[]
    a=run_novel_causality(17);b=run_novel_causality(17);assert canonical_hash(a)==canonical_hash(b);p.append("deterministic novel causality")
    a=run_fork_divergence(17);b=run_fork_divergence(17);assert canonical_hash(a)==canonical_hash(b) and a["fork_exact_equality_verified"] and a["branches_diverged"];p.append("exact fork then divergence")
    a=run_agency_coupling(17);b=run_agency_coupling(17);assert canonical_hash(a)==canonical_hash(b);p.append("deterministic agency coupling")
    a=run_reflection_confound(17);b=run_reflection_confound(17);assert canonical_hash(a)==canonical_hash(b);p.append("deterministic reflection confound")
    a=run_temporal_order(17);b=run_temporal_order(17);assert canonical_hash(a)==canonical_hash(b) and a["same_multiset_verified"];p.append("temporal order same-multiset check")
    assert run_false_belief(17)["correct"];p.append("false-belief perspective separation")
    a=run_embodiment_migration_precursor(17);assert a["identity_continuity_preserved"] and a["correct"];p.append("identity/body-model separation")
    assert run_provenance_overlap(17)["shared_root_correctly_detected"];p.append("provenance overlap detection")
    a=run_rumor_retraction_scenario();assert a["old_beliefs_preserved"] and a["active_is_yes"] and a["supersession_chain_valid"];p.append("temporal belief supersession")
    a=run_passive_factorial_cohort(8);b=run_passive_factorial_cohort(8);assert canonical_hash(a)==canonical_hash(b) and len(a["conditions"])==16;p.append("blinded passive cohort")
    assert PROFILES["HIGH_RISK_EXPERIMENTAL"].status=="EXPERIMENTAL_REQUIRES_EXPLICIT_HUMAN_APPROVAL";p.append("high-risk drive guard")
    a=run_active_individual(17,"BALANCED_MINIMAL",200);b=run_active_individual(17,"BALANCED_MINIMAL",200);assert canonical_hash(a)==canonical_hash(b);p.append("deterministic active individual")
    a=run_active_cohort(8);assert a["canonical_profile"]=="BALANCED_MINIMAL" and set(a["summary"])=={"BALANCED_MINIMAL","MINIMAL_REGULATION","EPISTEMIC_MINIMAL","NONE"};p.append("active cohort drive ablations")
    a=run_appraisal_lab();assert a["all_engines_deterministic_and_bounded"] and a["winner_selected"] is None;p.append("multi-engine appraisal lab")
    a=run_relationship_lab();assert a["asymmetry"]["states_differ"] and a["rumor_retraction"]["retraction_restores_baseline"];p.append("directed relationship invariants")
    a=run_segmentation_lab(8);assert len(a["mean_boundary_f1"])==4;p.append("event segmentation benchmark")
    a=run_replay_lab(8);assert set(a["summary"])=={"none","recent","significant","reservoir","dual"};p.append("replay policy benchmark")
    a=run_skill_store_lab();assert a["old_version_preserved"] and a["active_is_v2"] and a["supersession_valid"] and not a["arbitrary_code_execution_allowed"];p.append("versioned procedural skill store")
    assert run_suite(8)["summary"]["fork_divergence"]["exact_fork_equality_rate"]==1.0;p.append("baseline integrated smoke suite")
    assert run_advanced_suite(8)["summary"]["provenance_overlap"]["shared_root_detection_rate"]==1.0;p.append("advanced integrated smoke suite")
    return p
