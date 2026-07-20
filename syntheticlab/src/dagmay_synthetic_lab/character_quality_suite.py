"""Mosaic persistent-character quality experiments.

The suite measures useful game-character behavior, never apparent consciousness.
"""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import hashlib
import json
import random
from pathlib import Path
from statistics import mean
from typing import Iterable

from .persistent_character_boundary import assert_persistent_character_objective


@dataclass(frozen=True)
class MemoryEvent:
    event_id: str
    tick: int
    actor: str
    topic: str
    importance: float
    causal_parent: str | None = None
    evidence_quality: float = 1.0
    source_kind: str = "DIRECT"


def _score(query_actor: str, query_topic: str, event: MemoryEvent, newest_tick: int) -> float:
    actor_match = 1.0 if event.actor == query_actor else 0.0
    topic_match = 1.0 if event.topic == query_topic else 0.0
    recency = event.tick / max(newest_tick, 1)
    direct_bonus = 2.0 if event.source_kind == "DIRECT" else 0.0
    return (
        4.0 * actor_match
        + 4.0 * topic_match
        + 2.5 * event.evidence_quality
        + direct_bonus
        + 1.5 * event.importance
        + 0.25 * recency
    )


def retrieve_mosaic(
    history: Iterable[MemoryEvent], *, actor: str, topic: str, k: int
) -> list[MemoryEvent]:
    items = list(history)
    newest = max((item.tick for item in items), default=1)
    ranked = sorted(
        items,
        key=lambda item: (_score(actor, topic, item, newest), item.tick, item.event_id),
        reverse=True,
    )
    # Returning fewer than the maximum is preferable to filling context with weak evidence.
    selected = [item for item in ranked if _score(actor, topic, item, newest) >= 10.5][:k]
    # Preserve causal context when a selected event depends on an earlier event.
    by_id = {item.event_id: item for item in items}
    for item in tuple(selected):
        if item.causal_parent and item.causal_parent in by_id and by_id[item.causal_parent] not in selected:
            selected[-1] = by_id[item.causal_parent]
    return selected


def retrieve_recency(history: Iterable[MemoryEvent], *, k: int) -> list[MemoryEvent]:
    return sorted(history, key=lambda item: item.tick, reverse=True)[:k]


def retrieve_surface_overlap(
    history: Iterable[MemoryEvent], *, actor: str, topic: str, k: int
) -> list[MemoryEvent]:
    """A deliberately shallow baseline: semantic overlap plus recency, no provenance quality."""
    return sorted(
        history,
        key=lambda item: (
            int(item.actor == actor) + int(item.topic == topic),
            item.tick,
        ),
        reverse=True,
    )[:k]


def _trial(seed: int, distractors: int = 96, k: int = 4) -> dict[str, float]:
    rng = random.Random(seed)
    actors = ["Ari", "Bo", "Cy", "Dee", "Eli"]
    topics = ["medicine", "defense", "food", "crafting", "travel"]
    actor = rng.choice(actors)
    topic = rng.choice(topics)
    history: list[MemoryEvent] = []

    root_id = f"T{seed}-ROOT"
    history.append(MemoryEvent(root_id, 3, actor, topic, 0.95))
    consequence_id = f"T{seed}-CONSEQUENCE"
    history.append(MemoryEvent(consequence_id, 11, actor, topic, 0.90, root_id))

    for tick in range(12, 12 + distractors):
        other_actor = rng.choice([value for value in actors if value != actor])
        other_topic = rng.choice([value for value in topics if value != topic])
        history.append(
            MemoryEvent(
                event_id=f"T{seed}-D{tick}",
                tick=tick,
                actor=other_actor,
                topic=other_topic,
                importance=rng.uniform(0.05, 0.75),
            )
        )

    relevant = {root_id, consequence_id}

    def metrics(selected: list[MemoryEvent]) -> tuple[float, float, float]:
        ids = {item.event_id for item in selected}
        hits = len(ids & relevant)
        precision = hits / max(len(ids), 1)
        recall = hits / len(relevant)
        causal_pair = 1.0 if relevant <= ids else 0.0
        return precision, recall, causal_pair

    mosaic = metrics(retrieve_mosaic(history, actor=actor, topic=topic, k=k))
    recency = metrics(retrieve_recency(history, k=k))
    return {
        "mosaic_precision": mosaic[0],
        "mosaic_recall": mosaic[1],
        "mosaic_causal_pair": mosaic[2],
        "recency_precision": recency[0],
        "recency_recall": recency[1],
        "recency_causal_pair": recency[2],
    }


def run_memory_relevance_experiment(seeds: int = 128) -> dict[str, object]:
    assert_persistent_character_objective(
        purpose="Improve relevant autobiographical memory retrieval under long-history distraction.",
        independent_value_areas=["memory", "causal_coherence", "player_value", "reliability"],
        design="Compare bounded event-driven retrieval with a recency-only baseline.",
    )
    trials = [_trial(seed) for seed in range(seeds)]
    summary = {key: mean(trial[key] for trial in trials) for key in trials[0]}
    summary["recall_gain_over_recency"] = summary["mosaic_recall"] - summary["recency_recall"]
    summary["causal_pair_gain_over_recency"] = (
        summary["mosaic_causal_pair"] - summary["recency_causal_pair"]
    )
    return {
        "experiment_id": "MOSAIC-MEMORY-RELEVANCE-001",
        "classification": "PERSISTENT_CHARACTER_ENGINEERING",
        "provider_calls": 0,
        "canonical_mutation": False,
        "seeds": seeds,
        "summary": summary,
    }


def _adversarial_trial(seed: int, k: int = 4) -> dict[str, float]:
    rng = random.Random(100_000 + seed)
    actors = ["Ari", "Bo", "Cy", "Dee", "Eli"]
    topics = ["medicine", "defense", "food", "crafting", "travel"]
    actor = rng.choice(actors)
    topic = rng.choice(topics)
    root_id = f"A{seed}-ROOT"
    consequence_id = f"A{seed}-CONSEQUENCE"
    history = [
        MemoryEvent(root_id, 2, actor, topic, 0.92, evidence_quality=1.0),
        MemoryEvent(consequence_id, 8, actor, topic, 0.96, root_id, 1.0, "DIRECT"),
    ]
    tick = 9
    # Partial lexical matches test actor/topic discrimination.
    for _ in range(32):
        history.append(
            MemoryEvent(
                f"A{seed}-P{tick}", tick,
                actor if tick % 2 else rng.choice([a for a in actors if a != actor]),
                rng.choice([t for t in topics if t != topic]) if tick % 2 else topic,
                rng.uniform(0.3, 0.8), evidence_quality=rng.uniform(0.6, 1.0),
            )
        )
        tick += 1
    # Recent same-actor/same-topic rumors are superficially perfect matches but weak evidence.
    for _ in range(8):
        history.append(
            MemoryEvent(
                f"A{seed}-R{tick}", tick, actor, topic, 0.85,
                evidence_quality=0.05, source_kind="RUMOR",
            )
        )
        tick += 1

    relevant = {root_id, consequence_id}

    def score(selected: list[MemoryEvent]) -> tuple[float, float, float, float]:
        ids = {item.event_id for item in selected}
        hits = len(ids & relevant)
        rumor_rate = sum(item.source_kind == "RUMOR" for item in selected) / max(len(selected), 1)
        return (
            hits / max(len(ids), 1),
            hits / len(relevant),
            1.0 if relevant <= ids else 0.0,
            rumor_rate,
        )

    methods = {
        "mosaic": retrieve_mosaic(history, actor=actor, topic=topic, k=k),
        "surface": retrieve_surface_overlap(history, actor=actor, topic=topic, k=k),
        "recency": retrieve_recency(history, k=k),
    }
    result: dict[str, float] = {}
    for name, selected in methods.items():
        precision, recall, causal_pair, rumor_rate = score(selected)
        result[f"{name}_precision"] = precision
        result[f"{name}_recall"] = recall
        result[f"{name}_causal_pair"] = causal_pair
        result[f"{name}_rumor_rate"] = rumor_rate
    return result


def run_adversarial_memory_experiment(seeds: int = 128) -> dict[str, object]:
    assert_persistent_character_objective(
        purpose="Keep reliable causal memories available despite recent rumors and partial matches.",
        independent_value_areas=["memory", "causal_coherence", "hallucination_resistance", "player_value"],
        design="Compare provenance-aware retrieval with surface-overlap and recency baselines.",
    )
    trials = [_adversarial_trial(seed) for seed in range(seeds)]
    summary = {key: mean(trial[key] for trial in trials) for key in trials[0]}
    return {
        "experiment_id": "MOSAIC-MEMORY-ADVERSARIAL-002",
        "classification": "PERSISTENT_CHARACTER_ENGINEERING",
        "provider_calls": 0,
        "canonical_mutation": False,
        "seeds": seeds,
        "summary": summary,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=128)
    parser.add_argument("--output")
    args = parser.parse_args()
    result = {
        "suite_id": "MOSAIC-CHARACTER-QUALITY-V0.2",
        "memory_relevance": run_memory_relevance_experiment(args.seeds),
        "adversarial_memory": run_adversarial_memory_experiment(args.seeds),
    }
    encoded = json.dumps(result, indent=2, sort_keys=True)
    result_hash = hashlib.sha256(encoded.encode("utf-8")).hexdigest()
    envelope = {"result_hash": result_hash, "result": result}
    if args.output:
        path = Path(args.output)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(envelope, indent=2, sort_keys=True), encoding="utf-8")
    print(json.dumps(envelope, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
