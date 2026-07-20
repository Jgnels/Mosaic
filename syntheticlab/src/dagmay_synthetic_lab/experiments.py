from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, List, Any
import statistics

from .causal_world import NovelCausalWorld, OPAQUE_CONTEXTS, OPAQUE_ACTIONS
from .core import DeterministicRng, stable_unit_float, canonical_hash
from .learners import (
    ContextualCausalLearner,
    GlobalFrequencyLearner,
    NoLearningAgent,
    OracleKnowledgeAgent,
    PartnerContingencyLearner,
    AgencyCouplingLearner,
)


def _accuracy(mapping: Dict[str, str], chooser) -> float:
    correct = 0
    for context, optimal_action in mapping.items():
        correct += int(chooser.choose(context) == optimal_action)
    return correct / len(mapping)


def run_novel_causality(seed: int, training_episodes: int = 320) -> dict:
    world = NovelCausalWorld(seed=seed, noise=0.08)
    history = world.generate_exploration_history(
        episodes=training_episodes,
        policy_seed=seed ^ 0xA5A5A5A5,
    )

    developmental = ContextualCausalLearner(OPAQUE_CONTEXTS, OPAQUE_ACTIONS)
    global_control = GlobalFrequencyLearner(OPAQUE_ACTIONS)
    no_learning = NoLearningAgent(OPAQUE_ACTIONS)
    oracle = OracleKnowledgeAgent(world.rules.optimal_action_by_context)

    # Counterfactual-history control: same context/action experience schedule,
    # but outcomes come from a different hidden world.
    alt_world = NovelCausalWorld(seed=seed ^ 0x9E3779B9, noise=0.08)
    counterfactual = ContextualCausalLearner(OPAQUE_CONTEXTS, OPAQUE_ACTIONS)

    for i, exp in enumerate(history):
        developmental.observe(exp)
        global_control.observe(exp)
        no_learning.observe(exp)

        alt_outcome = alt_world.outcome(exp.context, exp.action, i)
        alt_exp = type(exp)(
            event_id=f"CF-{exp.event_id}",
            timestep=exp.timestep,
            context=exp.context,
            action=exp.action,
            outcome=alt_outcome,
            actor=exp.actor,
            counterpart=exp.counterpart,
        )
        counterfactual.observe(alt_exp)

    mapping = world.rules.optimal_action_by_context

    chosen = {
        context: {
            "optimal": mapping[context],
            "developmental": developmental.choose(context),
            "global_control": global_control.choose(context),
            "no_learning": no_learning.choose(context),
            "counterfactual_history": counterfactual.choose(context),
            "oracle_supplied_knowledge": oracle.choose(context),
            "developmental_evidence_count": len(
                developmental.evidence_for(context, developmental.choose(context))
            ),
            "developmental_evidence_sample": developmental.evidence_for(
                context, developmental.choose(context)
            )[-8:],
        }
        for context in OPAQUE_CONTEXTS
    }

    return {
        "experiment_id": "SL-NOVEL-CAUSALITY-001",
        "seed": seed,
        "hypothesis": (
            "An agent that continually learns context-conditioned consequences from "
            "its own ordered experience will outperform non-learning, context-free, "
            "and counterfactual-history controls in a novel opaque causal world."
        ),
        "null_hypothesis": (
            "Personal causal history provides no advantage beyond generic action frequency, "
            "fixed behavior, or an equally large but causally mismatched history."
        ),
        "world_rules_hash": canonical_hash(world.rules.to_dict()),
        "mapping_hidden_during_learning": True,
        "training_episodes": training_episodes,
        "metrics": {
            "developmental_accuracy": _accuracy(mapping, developmental),
            "global_control_accuracy": _accuracy(mapping, global_control),
            "no_learning_accuracy": _accuracy(mapping, no_learning),
            "counterfactual_history_accuracy": _accuracy(mapping, counterfactual),
            "oracle_supplied_knowledge_accuracy": _accuracy(mapping, oracle),
        },
        "chosen_actions": chosen,
        "developmental_state_hash": developmental.state_hash(),
        "developmental_provenance": developmental.provenance.to_dict(),
    }


def _partner_outcome(seed: int, branch: str, partner: str, index: int) -> int:
    # Shared phase handled separately. After fork:
    # Branch A: QAV becomes highly reliable, MIP moderately unreliable.
    # Branch B: QAV becomes unreliable, MIP stays moderately reliable.
    probs = {
        "A": {"QAV": 0.92, "MIP": 0.38},
        "B": {"QAV": 0.18, "MIP": 0.66},
    }
    return int(stable_unit_float("partner", seed, branch, partner, index) < probs[branch][partner])


def run_fork_divergence(seed: int, shared_events: int = 120, branch_events: int = 220) -> dict:
    partners = ("QAV", "MIP")
    base = PartnerContingencyLearner(partners)

    # Shared prehistory: both partners are similarly reliable.
    for i in range(shared_events):
        partner = partners[i % 2]
        outcome = int(stable_unit_float("shared", seed, partner, i) < 0.62)
        base.observe_partner_outcome(partner, outcome, f"SH-{seed:08X}-{i:05d}")

    fork_hash = base.state_hash()
    branch_a = base.clone()
    branch_b = base.clone()

    assert branch_a.state_hash() == fork_hash
    assert branch_b.state_hash() == fork_hash

    for i in range(branch_events):
        partner = partners[i % 2]
        outcome_a = _partner_outcome(seed, "A", partner, i)
        outcome_b = _partner_outcome(seed, "B", partner, i)
        branch_a.observe_partner_outcome(partner, outcome_a, f"A-{seed:08X}-{i:05d}")
        branch_b.observe_partner_outcome(partner, outcome_b, f"B-{seed:08X}-{i:05d}")

    return {
        "experiment_id": "SL-FORK-DIVERGENCE-001",
        "seed": seed,
        "hypothesis": (
            "Two exact cognitive-state forks exposed to different counterpart-conditioned "
            "outcomes will develop measurably divergent partner expectations and preferences."
        ),
        "null_hypothesis": (
            "Controlled post-fork experience will not produce reliable divergence beyond "
            "stochastic variation."
        ),
        "shared_events": shared_events,
        "branch_events": branch_events,
        "fork_state_hash": fork_hash,
        "fork_exact_equality_verified": True,
        "branch_a": {
            "preferred_partner": branch_a.prefer(),
            "scores": {p: branch_a.score(p) for p in partners},
            "state_hash": branch_a.state_hash(),
        },
        "branch_b": {
            "preferred_partner": branch_b.prefer(),
            "scores": {p: branch_b.score(p) for p in partners},
            "state_hash": branch_b.state_hash(),
        },
        "branches_diverged": branch_a.state_hash() != branch_b.state_hash(),
        "preference_diverged": branch_a.prefer() != branch_b.prefer(),
    }


def run_agency_coupling(seed: int, events: int = 420) -> dict:
    entities = ("E17", "E42", "E93")
    # The identity is hidden from the learner; only causal structure distinguishes it.
    self_like_entity = entities[seed % len(entities)]
    learner = AgencyCouplingLearner(entities)
    rng = DeterministicRng(seed ^ 0xC0FFEE)

    for i in range(events):
        entity = entities[rng.randrange(len(entities))]
        action_bit = rng.randrange(2)

        # Only one entity's own intervention is strongly coupled to the private signal.
        if entity == self_like_entity:
            base_p = 0.88 if action_bit == 1 else 0.12
        else:
            base_p = 0.52 if action_bit == 1 else 0.48

        changed = int(stable_unit_float("agency", seed, entity, action_bit, i) < base_p)
        learner.observe(entity, action_bit, changed, f"AG-{seed:08X}-{i:05d}")

    inferred = learner.most_agentic_entity()
    return {
        "experiment_id": "SL-AGENCY-COUPLING-001",
        "seed": seed,
        "hypothesis": (
            "Without being given a SELF label, a learner can identify the persistent entity "
            "whose interventions uniquely predict changes in its private/internal signal."
        ),
        "null_hypothesis": (
            "The learner cannot reliably distinguish the causally privileged entity from "
            "other observed entities."
        ),
        "events": events,
        "hidden_causally_privileged_entity": self_like_entity,
        "inferred_most_agentic_entity": inferred,
        "correct": inferred == self_like_entity,
        "coupling_scores": {e: learner.coupling_score(e) for e in entities},
        "state_hash": learner.state_hash(),
        "interpretation_warning": (
            "Success demonstrates learned agency coupling, not consciousness or a complete self-model."
        ),
    }


def run_suite(seed_count: int = 64) -> dict:
    novel = [run_novel_causality(seed) for seed in range(1, seed_count + 1)]
    forks = [run_fork_divergence(seed) for seed in range(1, seed_count + 1)]
    agency = [run_agency_coupling(seed) for seed in range(1, seed_count + 1)]

    def mean_metric(items, key):
        return statistics.mean(x["metrics"][key] for x in items)

    summary = {
        "novel_causality": {
            "seeds": seed_count,
            "developmental_accuracy_mean": mean_metric(novel, "developmental_accuracy"),
            "global_control_accuracy_mean": mean_metric(novel, "global_control_accuracy"),
            "no_learning_accuracy_mean": mean_metric(novel, "no_learning_accuracy"),
            "counterfactual_history_accuracy_mean": mean_metric(novel, "counterfactual_history_accuracy"),
            "oracle_supplied_knowledge_accuracy_mean": mean_metric(novel, "oracle_supplied_knowledge_accuracy"),
        },
        "fork_divergence": {
            "seeds": seed_count,
            "exact_fork_equality_rate": statistics.mean(
                1.0 if x["fork_exact_equality_verified"] else 0.0 for x in forks
            ),
            "state_divergence_rate": statistics.mean(
                1.0 if x["branches_diverged"] else 0.0 for x in forks
            ),
            "preference_divergence_rate": statistics.mean(
                1.0 if x["preference_diverged"] else 0.0 for x in forks
            ),
        },
        "agency_coupling": {
            "seeds": seed_count,
            "identification_accuracy": statistics.mean(
                1.0 if x["correct"] else 0.0 for x in agency
            ),
        },
    }

    return {
        "lab_version": "0.1",
        "suite_id": "DAGMAY-SYNTHETICLAB-V0.1-BASELINE",
        "research_claim_status": "PRELIMINARY_ENGINEERING_BASELINE",
        "summary": summary,
        "experiments": {
            "novel_causality": novel,
            "fork_divergence": forks,
            "agency_coupling": agency,
        },
    }
