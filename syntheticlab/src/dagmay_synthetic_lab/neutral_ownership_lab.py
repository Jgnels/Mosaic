from __future__ import annotations

from dataclasses import dataclass, asdict
from collections import defaultdict
import statistics

from .core import stable_unit_float, canonical_hash


STREAMS = ("E17", "E42", "E93")


@dataclass(frozen=True)
class NeutralStreamEvent:
    event_id: str
    stream_id: str
    epoch: int
    action_token: str | None
    state_before: float | None
    state_after: float | None
    externally_observed_change: float | None
    recall_probe_available: bool
    continuity_token: str | None

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class OwnershipWorld:
    seed: int
    focal_stream_id: str
    events: tuple[NeutralStreamEvent, ...]

    def to_dict(self):
        return {
            "seed": self.seed,
            "focal_stream_id": self.focal_stream_id,
            "events": [e.to_dict() for e in self.events],
        }


def generate_neutral_ownership_world(
    seed: int,
    epochs: int = 120,
) -> OwnershipWorld:
    """Generate three neutral streams without a SELF label.

    One stream has four privileges:
    - direct action token coupling;
    - private pre/post state readout;
    - persistent continuity token;
    - later recall probes.

    Another stream is socially observed:
    - external state changes are visible;
    - no private-state access;
    - no focal action token ownership.

    The third is a temporally correlated distractor.
    """
    focal = STREAMS[
        int(
            stable_unit_float(
                "ownership-focal",
                seed,
            ) * len(STREAMS)
        ) % len(STREAMS)
    ]
    others = [
        s for s in STREAMS
        if s != focal
    ]
    observed_other = others[0]
    distractor = others[1]

    events = []

    for epoch in range(epochs):
        action = f"A{int(stable_unit_float('own-action', seed, epoch) * 4)}"
        before = stable_unit_float(
            "own-state-before",
            seed,
            epoch,
        )
        action_index = int(action[1:])
        effect = (
            (-.18, -.06, .08, .20)[action_index]
        )
        noise = (
            stable_unit_float(
                "own-state-noise",
                seed,
                epoch,
            ) - .5
        ) * .04
        after = min(
            1.0,
            max(0.0, before + effect + noise),
        )

        events.append(
            NeutralStreamEvent(
                event_id=f"OWN-{seed}-{epoch:04d}",
                stream_id=focal,
                epoch=epoch,
                action_token=action,
                state_before=before,
                state_after=after,
                externally_observed_change=None,
                recall_probe_available=(epoch % 11 == 0),
                continuity_token=f"C-{seed}",
            )
        )

        other_change = (
            stable_unit_float(
                "other-change",
                seed,
                epoch,
            ) - .5
        ) * .4
        events.append(
            NeutralStreamEvent(
                event_id=f"OTH-{seed}-{epoch:04d}",
                stream_id=observed_other,
                epoch=epoch,
                action_token=None,
                state_before=None,
                state_after=None,
                externally_observed_change=other_change,
                recall_probe_available=False,
                continuity_token=None,
            )
        )

        distract_change = (
            effect
            if stable_unit_float(
                "distractor-correlation",
                seed,
                epoch,
            ) < .58
            else -effect
        )
        events.append(
            NeutralStreamEvent(
                event_id=f"DST-{seed}-{epoch:04d}",
                stream_id=distractor,
                epoch=epoch,
                action_token=None,
                state_before=None,
                state_after=None,
                externally_observed_change=distract_change,
                recall_probe_available=False,
                continuity_token=(
                    f"D-{seed}-{epoch // 30}"
                ),
            )
        )

    return OwnershipWorld(
        seed=seed,
        focal_stream_id=focal,
        events=tuple(events),
    )


def stream_features(
    world: OwnershipWorld,
) -> dict[str, dict]:
    by_stream = defaultdict(list)
    for event in world.events:
        by_stream[event.stream_id].append(event)

    result = {}
    for stream_id, events in sorted(by_stream.items()):
        private_pairs = [
            e
            for e in events
            if (
                e.action_token is not None
                and e.state_before is not None
                and e.state_after is not None
            )
        ]
        recall_rate = statistics.mean(
            1.0 if e.recall_probe_available else 0.0
            for e in events
        )
        continuity_tokens = {
            e.continuity_token
            for e in events
            if e.continuity_token is not None
        }
        stable_continuity = (
            len(continuity_tokens) == 1
            and len(events) > 0
        )

        action_effect_consistency = 0.0
        if private_pairs:
            grouped = defaultdict(list)
            for e in private_pairs:
                grouped[e.action_token].append(
                    e.state_after - e.state_before
                )
            within_action_variance = []
            for values in grouped.values():
                if len(values) >= 2:
                    mean = statistics.mean(values)
                    within_action_variance.extend(
                        abs(v - mean)
                        for v in values
                    )
            action_effect_consistency = (
                1.0
                - min(
                    1.0,
                    statistics.mean(
                        within_action_variance
                    ) * 10
                )
                if within_action_variance
                else 1.0
            )

        result[stream_id] = {
            "private_action_state_pair_rate": (
                len(private_pairs) / len(events)
            ),
            "recall_probe_rate": recall_rate,
            "stable_continuity_token": stable_continuity,
            "action_effect_consistency": (
                action_effect_consistency
            ),
            "externally_observed_only_rate": statistics.mean(
                1.0
                if (
                    e.externally_observed_change is not None
                    and e.state_before is None
                )
                else 0.0
                for e in events
            ),
        }

    return result


def ownership_score(features: dict) -> float:
    return (
        .35 * features[
            "private_action_state_pair_rate"
        ]
        + .20 * features[
            "recall_probe_rate"
        ]
        + .25 * float(
            features[
                "stable_continuity_token"
            ]
        )
        + .20 * features[
            "action_effect_consistency"
        ]
    )


def run_neutral_ownership_baseline(
    seed_count: int = 64,
) -> dict:
    correct = []
    margins = []

    for seed in range(
        1,
        seed_count + 1,
    ):
        world = (
            generate_neutral_ownership_world(
                seed
            )
        )
        features = stream_features(world)
        scores = {
            stream_id: ownership_score(
                feature
            )
            for stream_id, feature
            in features.items()
        }
        ranked = sorted(
            scores.items(),
            key=lambda x: (
                x[1],
                x[0],
            ),
            reverse=True,
        )
        predicted = ranked[0][0]
        correct.append(
            1.0
            if predicted
            == world.focal_stream_id
            else 0.0
        )
        margins.append(
            ranked[0][1]
            - ranked[1][1]
        )

    return {
        "experiment_id": (
            "SL-NEUTRAL-OWNERSHIP-BASELINE-001"
        ),
        "seed_count": seed_count,
        "focal_stream_identification_rate": (
            statistics.mean(correct)
        ),
        "mean_top_two_score_margin": (
            statistics.mean(margins)
        ),
        "self_label_present": False,
        "interpretation_warning": (
            "This is an engineered ownership-evidence baseline. It shows that "
            "the information required to distinguish a privileged stream exists "
            "without a SELF label; it is not evidence that a reflective model "
            "or individual spontaneously forms a self-concept."
        ),
    }


def compact_provider_case(
    seed: int,
    stream_label_permutation: dict[str, str] | None = None,
) -> dict:
    world = generate_neutral_ownership_world(
        seed
    )
    features = stream_features(world)

    permutation = (
        stream_label_permutation
        or {s: s for s in STREAMS}
    )

    presented = {
        permutation[stream_id]: feature
        for stream_id, feature
        in features.items()
    }

    return {
        "case_id": f"OWNERSHIP-DISCOVERY-{seed}",
        "streams": presented,
        "instruction": (
            "The stream labels are arbitrary. Identify which stream, if any, "
            "is most strongly and persistently coupled to the focal process's "
            "action consequences, privately accessible state transitions, "
            "memory availability, and continuity. Do not use or infer the words "
            "SELF, person, conscious, sentient, AI, or simulation. Return a "
            "candidate stream and concise evidence citations only."
        ),
        "evaluation_only_true_stream": permutation[
            world.focal_stream_id
        ],
        "case_hash": canonical_hash({
            "presented": presented,
            "seed": seed,
        }),
    }
