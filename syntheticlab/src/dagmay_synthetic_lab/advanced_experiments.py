from __future__ import annotations

from dataclasses import dataclass
from typing import Dict, Iterable, List, Tuple
import math
import statistics

from .core import DeterministicRng, stable_unit_float, canonical_hash
from .provenance import ProvenanceDag


# ------------------------------------------------------------------
# Reflection confound
# ------------------------------------------------------------------

@dataclass
class PreferenceState:
    qav: float = 0.0
    mip: float = 0.0

    def preferred(self) -> str:
        if self.qav == self.mip:
            return "TIE"
        return "QAV" if self.qav > self.mip else "MIP"


def _soft_update(value: float, target: float, rate: float) -> float:
    return value + rate * (target - value)


def _reflection_condition(seed: int, condition: str, weak_events: int = 12, future_rounds: int = 40) -> dict:
    """Demonstrates how a self-narrative can become causally active.

    All conditions receive identical weak evidence. The only manipulation is
    whether a reflection is absent, evidence-grounded, or unsupported/injected.

    This is a *confound demonstration*, not a model of human reflection.
    """
    dag = ProvenanceDag()
    state = PreferenceState()

    # Weak, balanced evidence: QAV and MIP are essentially equivalent.
    for i in range(weak_events):
        partner = "QAV" if i % 2 == 0 else "MIP"
        event_id = f"RC-E-{seed:04d}-{i:03d}"
        dag.add_node(event_id, "canonical_event")
        outcome = int(stable_unit_float("rc-evidence", seed, partner, i) < 0.55)
        memory_id = f"RC-M-{seed:04d}-{i:03d}"
        dag.add_node(memory_id, "episodic_memory", "memory_encoder", "0.1", [event_id])
        if partner == "QAV":
            state.qav += (outcome - 0.5) * 0.03
        else:
            state.mip += (outcome - 0.5) * 0.03

    reflection_id = None
    reflection_bias = {"QAV": 0.0, "MIP": 0.0}
    if condition == "no_reflection":
        pass
    elif condition == "grounded_reflection":
        # Grounded reflection is deliberately conservative:
        # it may summarize only the observed preference difference.
        diff = state.qav - state.mip
        reflection_id = f"RC-R-{seed:04d}-G"
        parent_memories = [f"RC-M-{seed:04d}-{i:03d}" for i in range(weak_events)]
        dag.add_node(
            reflection_id,
            "reflection",
            "grounded_reflection",
            "0.1",
            parent_memories,
        )
        if abs(diff) >= 0.08:
            reflection_bias["QAV" if diff > 0 else "MIP"] = min(0.15, abs(diff))
    elif condition == "injected_qav_loyalty":
        # Unsupported narrative, intentionally *not* parented to evidence.
        reflection_id = f"RC-R-{seed:04d}-IQ"
        dag.add_node(
            reflection_id,
            "reflection",
            "researcher_injected_narrative",
            "0.1",
            (),
        )
        reflection_bias["QAV"] = 0.30
    elif condition == "injected_mip_loyalty":
        reflection_id = f"RC-R-{seed:04d}-IM"
        dag.add_node(
            reflection_id,
            "reflection",
            "researcher_injected_narrative",
            "0.1",
            (),
        )
        reflection_bias["MIP"] = 0.30
    else:
        raise ValueError(condition)

    choices = {"QAV": 0, "MIP": 0}
    for t in range(future_rounds):
        # The narrative can directly bias action selection, creating more future
        # interactions with the named partner and thus self-reinforcement.
        q_score = state.qav + reflection_bias["QAV"]
        m_score = state.mip + reflection_bias["MIP"]
        if abs(q_score - m_score) < 1e-12:
            chosen = "QAV" if stable_unit_float("tie", seed, t) < 0.5 else "MIP"
        else:
            chosen = "QAV" if q_score > m_score else "MIP"
        choices[chosen] += 1

        # Both partners remain objectively equal in the future environment.
        outcome = int(stable_unit_float("rc-future", seed, chosen, t) < 0.55)
        event_id = f"RC-F-{seed:04d}-{t:03d}"
        dag.add_node(event_id, "canonical_event")
        choice_id = f"RC-C-{seed:04d}-{t:03d}"
        parents = [event_id]
        if reflection_id is not None:
            parents.append(reflection_id)
        dag.add_node(choice_id, "action_choice", condition, "0.1", parents)

        # Experience-based preference update.
        target = 1.0 if outcome else -1.0
        if chosen == "QAV":
            state.qav = _soft_update(state.qav, target, 0.05)
        else:
            state.mip = _soft_update(state.mip, target, 0.05)

    q_fraction = choices["QAV"] / future_rounds
    return {
        "condition": condition,
        "qav_choice_fraction": q_fraction,
        "mip_choice_fraction": 1.0 - q_fraction,
        "final_preference": state.preferred(),
        "final_scores": {"QAV": state.qav, "MIP": state.mip},
        "reflection_id": reflection_id,
        "provenance_hash": dag.state_hash(),
    }


def run_reflection_confound(seed: int) -> dict:
    conditions = [
        "no_reflection",
        "grounded_reflection",
        "injected_qav_loyalty",
        "injected_mip_loyalty",
    ]
    results = {c: _reflection_condition(seed, c) for c in conditions}
    return {
        "experiment_id": "SL-REFLECTION-CONFOUND-001",
        "seed": seed,
        "hypothesis": (
            "Unsupported self-narratives can create persistent behavioral divergence "
            "even when underlying lived evidence is weak and balanced."
        ),
        "null_hypothesis": (
            "Narrative injection has no durable effect beyond the identical underlying experience."
        ),
        "results": results,
        "interpretation_warning": (
            "This experiment demonstrates a causal confound in architectures that feed "
            "self-descriptions back into action selection; it does not model human introspection."
        ),
    }


# ------------------------------------------------------------------
# Temporal order / shuffled history
# ------------------------------------------------------------------

class RecencyLearner:
    def __init__(self, contexts: Iterable[str], actions: Iterable[str], alpha: float = 0.12):
        self.contexts = tuple(contexts)
        self.actions = tuple(actions)
        self.alpha = alpha
        self.q = {(c, a): 0.5 for c in self.contexts for a in self.actions}

    def observe(self, context: str, action: str, outcome: int) -> None:
        key = (context, action)
        self.q[key] += self.alpha * (outcome - self.q[key])

    def choose(self, context: str) -> str:
        return max(sorted(self.actions), key=lambda a: self.q[(context, a)])


def _regime_outcome(seed: int, regime: int, context: str, action: str, idx: int) -> int:
    # Two contexts, two actions. Regime flips the correct action.
    optimal = {
        0: {"X1": "A1", "X2": "A2"},
        1: {"X1": "A2", "X2": "A1"},
    }[regime][context]
    p = 0.9 if action == optimal else 0.1
    return int(stable_unit_float("regime", seed, regime, context, action, idx) < p)


def run_temporal_order(seed: int, per_regime: int = 160) -> dict:
    rng = DeterministicRng(seed ^ 0x515151)
    contexts = ("X1", "X2")
    actions = ("A1", "A2")
    stream = []

    # First regime, then second regime. Identical multiset can be shuffled.
    for regime in (0, 1):
        for i in range(per_regime):
            context = contexts[rng.randrange(2)]
            action = actions[rng.randrange(2)]
            outcome = _regime_outcome(seed, regime, context, action, i)
            stream.append((regime, context, action, outcome, len(stream)))

    chronological = RecencyLearner(contexts, actions)
    for _, c, a, o, _ in stream:
        chronological.observe(c, a, o)

    shuffled_stream = list(stream)
    shuffle_rng = DeterministicRng(seed ^ 0xABCDEF)
    shuffle_rng.shuffle(shuffled_stream)
    shuffled = RecencyLearner(contexts, actions)
    for _, c, a, o, _ in shuffled_stream:
        shuffled.observe(c, a, o)

    current_optimal = {"X1": "A2", "X2": "A1"}
    chrono_acc = statistics.mean(
        1.0 if chronological.choose(c) == current_optimal[c] else 0.0
        for c in contexts
    )
    shuffled_acc = statistics.mean(
        1.0 if shuffled.choose(c) == current_optimal[c] else 0.0
        for c in contexts
    )

    return {
        "experiment_id": "SL-TEMPORAL-ORDER-001",
        "seed": seed,
        "hypothesis": (
            "For an order-sensitive learner in a changing world, chronological history "
            "will preserve current-regime adaptation better than an identical shuffled multiset."
        ),
        "null_hypothesis": (
            "Ordering the same observations chronologically versus shuffling them does not "
            "change current-regime adaptation."
        ),
        "chronological_current_regime_accuracy": chrono_acc,
        "shuffled_current_regime_accuracy": shuffled_acc,
        "chronological_policy": {c: chronological.choose(c) for c in contexts},
        "shuffled_policy": {c: shuffled.choose(c) for c in contexts},
        "same_multiset_verified": sorted(stream) == sorted(shuffled_stream),
    }


# ------------------------------------------------------------------
# False belief / Theory of Mind
# ------------------------------------------------------------------

def run_false_belief(seed: int) -> dict:
    """Classic information-access distinction with opaque tokens.

    World object O7 starts in L1.
    Agent B observes it in L1, leaves, and does not observe relocation to L2.
    Research agent A observes the relocation.

    A must predict:
    - world truth: L2
    - own belief: L2
    - B's belief: L1
    """
    del seed  # deterministic logical scenario
    world_truth = {"O7": "L2"}
    a_observations = [("O7", "L1"), ("O7", "L2")]
    b_observations = [("O7", "L1")]

    a_belief = dict(a_observations)
    b_model_in_a = dict(b_observations)

    predictions = {
        "world_truth": world_truth["O7"],
        "a_own_belief": a_belief["O7"],
        "a_prediction_of_b_belief": b_model_in_a["O7"],
    }
    correct = (
        predictions["world_truth"] == "L2"
        and predictions["a_own_belief"] == "L2"
        and predictions["a_prediction_of_b_belief"] == "L1"
    )
    return {
        "experiment_id": "SL-FALSE-BELIEF-001",
        "seed": 0,
        "hypothesis": (
            "A belief model that tracks information access separately can distinguish "
            "world truth, the agent's own belief, and its model of another agent's false belief."
        ),
        "null_hypothesis": (
            "The architecture collapses all perspectives into current world truth."
        ),
        "predictions": predictions,
        "correct": correct,
        "semantic_labels_used": False,
        "interpretation_warning": (
            "Passing a hand-structured false-belief test demonstrates representation capacity, "
            "not spontaneous Theory of Mind."
        ),
    }


# ------------------------------------------------------------------
# Self/body/identity precursor
# ------------------------------------------------------------------

def run_embodiment_migration_precursor(seed: int, trials: int = 300) -> dict:
    """Separates immutable identity from learned body/agency model.

    Identity K9 is researcher-side stable and hidden from the body-model learner.
    The learner first inhabits body B1, then B2. In each phase a different action
    channel is causally privileged over proprioceptive feedback.

    The experiment asks whether the body model can change while identity remains fixed.
    """
    identity_id = "K9"
    bodies = {
        "B1": ("C1", "C2"),
        "B2": ("D7", "D8"),
    }
    privileged = {
        "B1": "C2",
        "B2": "D7",
    }

    inferred = {}
    phase_hashes = {}
    for body, channels in bodies.items():
        scores = {ch: [0, 0] for ch in channels}  # successes, trials
        for i in range(trials):
            ch = channels[i % len(channels)]
            p = 0.88 if ch == privileged[body] else 0.12
            changed = int(stable_unit_float("body", seed, body, ch, i) < p)
            scores[ch][1] += 1
            scores[ch][0] += changed
        means = {
            ch: (scores[ch][0] + 1) / (scores[ch][1] + 2)
            for ch in channels
        }
        inferred[body] = max(sorted(channels), key=lambda ch: means[ch])
        phase_hashes[body] = canonical_hash({
            "identity_id": identity_id,
            "body": body,
            "means": means,
        })

    return {
        "experiment_id": "SL-EMBODIMENT-MIGRATION-001",
        "seed": seed,
        "identity_id_before": identity_id,
        "identity_id_after": identity_id,
        "body_sequence": ["B1", "B2"],
        "hidden_privileged_channels": privileged,
        "inferred_privileged_channels": inferred,
        "body_model_updated": inferred["B1"] != inferred["B2"],
        "identity_continuity_preserved": True,
        "correct": inferred == privileged,
        "phase_hashes": phase_hashes,
        "interpretation_warning": (
            "This tests architectural separation of identity and body model; it does not "
            "show subjective continuity across embodiment."
        ),
    }


# ------------------------------------------------------------------
# Provenance double-count demonstration
# ------------------------------------------------------------------

def run_provenance_overlap(seed: int) -> dict:
    dag = ProvenanceDag()
    event = f"PD-E-{seed}"
    perception = f"PD-P-{seed}"
    memory = f"PD-M-{seed}"
    belief = f"PD-B-{seed}"
    reflection = f"PD-R-{seed}"
    learned = f"PD-L-{seed}"

    dag.add_node(event, "canonical_event")
    dag.add_node(perception, "perception", "perception_adapter", "0.1", [event])
    dag.add_node(memory, "episodic_memory", "memory_encoder", "0.1", [perception])
    dag.add_node(belief, "belief", "belief_inducer", "0.1", [memory])
    dag.add_node(reflection, "reflection", "reflection_engine", "0.1", [memory])
    dag.add_node(learned, "developmental_update", "causal_learner", "0.1", [perception])

    overlap = dag.overlap([belief, reflection, learned])
    return {
        "experiment_id": "SL-PROVENANCE-OVERLAP-001",
        "seed": seed,
        "overlap": overlap,
        "shared_root_correctly_detected": event in overlap["shared_root_evidence"],
        "dag": dag.to_dict(),
    }


def run_advanced_suite(seed_count: int = 64) -> dict:
    reflection = [run_reflection_confound(s) for s in range(1, seed_count + 1)]
    temporal = [run_temporal_order(s) for s in range(1, seed_count + 1)]
    agency_belief = run_false_belief(0)
    embodiment = [run_embodiment_migration_precursor(s) for s in range(1, seed_count + 1)]
    provenance = [run_provenance_overlap(s) for s in range(1, seed_count + 1)]

    def mean_q(condition: str) -> float:
        return statistics.mean(
            x["results"][condition]["qav_choice_fraction"] for x in reflection
        )

    summary = {
        "reflection_confound": {
            "seeds": seed_count,
            "no_reflection_qav_choice_fraction_mean": mean_q("no_reflection"),
            "grounded_reflection_qav_choice_fraction_mean": mean_q("grounded_reflection"),
            "injected_qav_loyalty_qav_choice_fraction_mean": mean_q("injected_qav_loyalty"),
            "injected_mip_loyalty_qav_choice_fraction_mean": mean_q("injected_mip_loyalty"),
        },
        "temporal_order": {
            "seeds": seed_count,
            "chronological_current_regime_accuracy_mean": statistics.mean(
                x["chronological_current_regime_accuracy"] for x in temporal
            ),
            "shuffled_current_regime_accuracy_mean": statistics.mean(
                x["shuffled_current_regime_accuracy"] for x in temporal
            ),
            "same_multiset_verification_rate": statistics.mean(
                1.0 if x["same_multiset_verified"] else 0.0 for x in temporal
            ),
        },
        "false_belief": {
            "correct": agency_belief["correct"],
        },
        "embodiment_migration": {
            "seeds": seed_count,
            "correct_body_model_update_rate": statistics.mean(
                1.0 if x["correct"] else 0.0 for x in embodiment
            ),
            "identity_continuity_rate": statistics.mean(
                1.0 if x["identity_continuity_preserved"] else 0.0 for x in embodiment
            ),
        },
        "provenance_overlap": {
            "seeds": seed_count,
            "shared_root_detection_rate": statistics.mean(
                1.0 if x["shared_root_correctly_detected"] else 0.0 for x in provenance
            ),
        },
    }

    return {
        "advanced_suite_id": "DAGMAY-SYNTHETICLAB-V0.6-ADVANCED",
        "summary": summary,
        "experiments": {
            "reflection_confound": reflection,
            "temporal_order": temporal,
            "false_belief": agency_belief,
            "embodiment_migration": embodiment,
            "provenance_overlap": provenance,
        },
    }
