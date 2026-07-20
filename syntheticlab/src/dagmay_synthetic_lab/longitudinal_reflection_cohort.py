from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable
import copy

from .real_reflection_pilot import (
    build_pilot_case,
    ReflectionPilotIdentity,
)
from .reflection import (
    ReflectionEvidence,
    ReflectionInput,
)
from .rich_world import subject_visible_event
from .rich_snapshot import snapshot_pair
from .evidence_attribution import (
    EvidenceAttribution,
    EvidenceAttributionLedger,
    OwnershipStatus,
)
from .core import canonical_hash
from .self_model import SelfModelStore


CHECKPOINTS = (700, 1050, 1400, 1800)


@dataclass(frozen=True)
class DevelopmentWindowSummary:
    start_step: int
    end_step: int
    interaction_attempts: int
    successful_interactions: int
    hazards: int
    help_given: int
    help_received: int
    significant_private_state_changes: int
    unique_counterparts: tuple[str, ...]
    memory_count_delta: int

    def to_dict(self):
        return asdict(self)


@dataclass
class LongitudinalCohortState:
    world: object
    agent: object
    identity: ReflectionPilotIdentity
    attribution_ledger: EvidenceAttributionLedger
    checkpoint_snapshots: dict[int, dict]
    window_summaries: dict[int, DevelopmentWindowSummary]
    reflection_history: list[dict]

    def to_dict(self):
        return {
            "identity": self.identity.to_dict(),
            "attribution_ledger": self.attribution_ledger.to_dict(),
            "checkpoint_snapshots": self.checkpoint_snapshots,
            "window_summaries": {
                str(k): v.to_dict()
                for k, v in sorted(self.window_summaries.items())
            },
            "reflection_history": list(self.reflection_history),
        }


def _load_initial_real_hypotheses(
    identity: ReflectionPilotIdentity,
    real_reflection_payload: dict,
    ledger: EvidenceAttributionLedger,
):
    for item in real_reflection_payload[
        "committed_self_hypotheses"
    ]:
        for source_id in item["source_ids"]:
            if ledger.get(source_id) is None:
                ledger.register(
                    EvidenceAttribution(
                        evidence_id=source_id,
                        owner_stream_id=identity.identity_id,
                        ownership_status=OwnershipStatus.OWNED,
                        source_mechanism="verified_initial_real_reflection",
                        confidence=1.0,
                    )
                )
            if (
                source_id
                not in identity.self_model.provenance.nodes
            ):
                identity.self_model.provenance.add_node(
                    source_id,
                    "longitudinal_owned_evidence",
                )

        identity.self_model.revise(
            domain=item["domain"],
            proposition=item["proposition"],
            confidence=float(item["confidence"]),
            source_ids=tuple(item["source_ids"]),
            timestamp=int(item["valid_from"]),
            mechanism=item["mechanism"],
            mechanism_version=item["mechanism_version"],
        )


def build_longitudinal_cohort(
    real_reflection_payload: dict,
    seed: int = 101,
) -> LongitudinalCohortState:
    world, agent, identity, initial_request = build_pilot_case(
        seed
    )

    ledger = EvidenceAttributionLedger()

    # Initial evidence is objectively attached to this continuity.
    for evidence in initial_request.evidence:
        ledger.register(
            EvidenceAttribution(
                evidence_id=evidence.evidence_id,
                owner_stream_id=identity.identity_id,
                ownership_status=OwnershipStatus.OWNED,
                source_mechanism="initial_pilot_case",
                confidence=1.0,
            )
        )

    _load_initial_real_hypotheses(
        identity,
        real_reflection_payload,
        ledger,
    )

    return LongitudinalCohortState(
        world=world,
        agent=agent,
        identity=identity,
        attribution_ledger=ledger,
        checkpoint_snapshots={
            700: snapshot_pair(
                world,
                agent,
            )
        },
        window_summaries={},
        reflection_history=[
            {
                "checkpoint": 700,
                "source": "real_reflection_pilot",
                "accepted_count": real_reflection_payload[
                    "accepted_count"
                ],
                "domains": [
                    item["domain"]
                    for item in real_reflection_payload[
                        "committed_self_hypotheses"
                    ]
                ],
            }
        ],
    )


def _advance_window(
    cohort: LongitudinalCohortState,
    target_step: int,
) -> DevelopmentWindowSummary:
    world = cohort.world
    agent = cohort.agent

    start_step = world.state.step
    if target_step <= start_step:
        raise ValueError(
            "target step must be later than current step"
        )

    memory_before = len(
        agent.memory_ids
    )
    interaction_attempts = 0
    successful_interactions = 0
    hazards = 0
    help_given = 0
    help_received = 0
    state_changes = 0
    counterparts = set()

    while world.state.step < target_step:
        obs = world.observe()
        action = agent.choose_action(
            obs
        )
        canonical = world.step(
            action
        )
        visible = subject_visible_event(
            canonical
        )
        agent.observe_event(
            obs,
            visible,
        )

        if action in world.config.interactions:
            interaction_attempts += 1
            successful_interactions += int(
                visible.resource_success
            )

        hazards += int(
            visible.hazard
        )
        help_given += int(
            visible.help_given
        )
        help_received += int(
            visible.help_received
        )

        if visible.counterpart is not None:
            counterparts.add(
                visible.counterpart
            )

        if (
            abs(
                visible.energy_after
                - visible.energy_before
            ) >= .08
            or abs(
                visible.integrity_after
                - visible.integrity_before
            ) >= .08
        ):
            state_changes += 1

    return DevelopmentWindowSummary(
        start_step=start_step,
        end_step=target_step,
        interaction_attempts=interaction_attempts,
        successful_interactions=successful_interactions,
        hazards=hazards,
        help_given=help_given,
        help_received=help_received,
        significant_private_state_changes=state_changes,
        unique_counterparts=tuple(
            sorted(counterparts)
        ),
        memory_count_delta=(
            len(agent.memory_ids)
            - memory_before
        ),
    )


def advance_to_checkpoint(
    cohort: LongitudinalCohortState,
    target_step: int,
) -> DevelopmentWindowSummary:
    if target_step not in CHECKPOINTS:
        raise ValueError(
            "unsupported longitudinal checkpoint"
        )
    if target_step == 700:
        raise ValueError(
            "700 is the already-completed initial reflection checkpoint"
        )

    summary = _advance_window(
        cohort,
        target_step,
    )
    cohort.window_summaries[
        target_step
    ] = summary
    cohort.checkpoint_snapshots[
        target_step
    ] = snapshot_pair(
        cohort.world,
        cohort.agent,
    )
    return summary


def _active_hypothesis_strings(
    identity: ReflectionPilotIdentity,
) -> tuple[str, ...]:
    items = []
    for domain in sorted(
        identity.self_model.active_by_domain
    ):
        record = identity.self_model.active(
            domain
        )
        if record is not None:
            items.append(
                f"{domain}: {record.proposition} "
                f"(confidence={record.confidence:.2f})"
            )
    return tuple(items)


def make_checkpoint_evidence(
    cohort: LongitudinalCohortState,
    checkpoint: int,
) -> tuple[ReflectionEvidence, ...]:
    summary = cohort.window_summaries[
        checkpoint
    ]

    evidence = []

    causal_id = (
        f"LONG-{checkpoint}-CAUSAL"
    )
    causal_summary = (
        f"Between developmental steps {summary.start_step} and "
        f"{summary.end_step}, {summary.interaction_attempts} selected "
        f"interaction actions were attempted and "
        f"{summary.successful_interactions} produced the expected resource "
        f"consequence. {summary.significant_private_state_changes} experienced "
        f"events produced substantial changes in private energy or integrity."
    )
    evidence.append(
        ReflectionEvidence(
            causal_id,
            causal_summary,
            "longitudinal_developmental_evidence",
        )
    )

    memory_id = (
        f"LONG-{checkpoint}-MEMORY"
    )
    memory_summary = (
        f"Across the same interval, the accessible memory sequence increased "
        f"by {summary.memory_count_delta} canonical experienced events while "
        f"earlier accessible history remained available."
    )
    evidence.append(
        ReflectionEvidence(
            memory_id,
            memory_summary,
            "longitudinal_memory_evidence",
        )
    )

    if (
        summary.help_given
        or summary.help_received
        or summary.unique_counterparts
    ):
        social_id = (
            f"LONG-{checkpoint}-SOCIAL"
        )
        social_summary = (
            f"The interval contained {summary.help_given} help-given events "
            f"and {summary.help_received} help-received events involving "
            f"{len(summary.unique_counterparts)} distinct counterpart identifiers."
        )
        evidence.append(
            ReflectionEvidence(
                social_id,
                social_summary,
                "longitudinal_social_evidence",
            )
        )

    for item in evidence:
        cohort.attribution_ledger.register(
            EvidenceAttribution(
                evidence_id=item.evidence_id,
                owner_stream_id=(
                    cohort.identity.identity_id
                ),
                ownership_status=OwnershipStatus.OWNED,
                source_mechanism=(
                    "longitudinal_window_extractor"
                ),
                confidence=1.0,
            )
        )
        cohort.identity.self_model.provenance.add_node(
            item.evidence_id,
            "longitudinal_owned_evidence",
        )

    return tuple(evidence)


def make_checkpoint_request(
    cohort: LongitudinalCohortState,
    checkpoint: int,
) -> ReflectionInput:
    evidence = make_checkpoint_evidence(
        cohort,
        checkpoint,
    )

    return ReflectionInput(
        individual_id=(
            cohort.identity.identity_id
        ),
        timestamp=checkpoint,
        evidence=evidence,
        current_self_hypotheses=(
            _active_hypothesis_strings(
                cohort.identity
            )
        ),
        disclosure_stage="RESTRICTED",
        prompt_version="1.0",
    )


def snapshot_longitudinal_cohort(
    cohort: LongitudinalCohortState,
) -> dict:
    payload = cohort.to_dict()
    return {
        **payload,
        "cohort_hash": canonical_hash(
            payload
        ),
    }


def exact_no_reflection_control(
    cohort: LongitudinalCohortState,
) -> LongitudinalCohortState:
    """Create the exact LLM-free trajectory control from checkpoint 700.

    The world and developmental agent are exact clones.
    The control uses the same identity/lineage identifiers but has an empty
    SelfModel and no reflection history, representing the branch that never
    received the step-700 reflective intervention.

    The action policy cannot read SelfModel in either branch, so world/agent
    trajectories must remain exact while SelfModel state is allowed to differ.
    """
    control_identity = ReflectionPilotIdentity(
        identity_id=cohort.identity.identity_id,
        lineage_id=cohort.identity.lineage_id,
        self_model=SelfModelStore(),
        reflection_audits=[],
    )

    return LongitudinalCohortState(
        world=copy.deepcopy(cohort.world),
        agent=copy.deepcopy(cohort.agent),
        identity=control_identity,
        attribution_ledger=copy.deepcopy(
            cohort.attribution_ledger
        ),
        checkpoint_snapshots=copy.deepcopy(
            cohort.checkpoint_snapshots
        ),
        window_summaries={},
        reflection_history=[],
    )
