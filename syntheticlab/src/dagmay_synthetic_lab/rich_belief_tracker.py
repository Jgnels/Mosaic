from __future__ import annotations

from dataclasses import dataclass

from .beliefs import TemporalBeliefGraph
from .rich_world import RichObservation, SubjectVisibleRichEvent


@dataclass
class RichBeliefTracker:
    perspective_id: str

    def __post_init__(self):
        self.graph = TemporalBeliefGraph()
        self.revision_count = 0
        self.direct_evidence_updates = 0
        self.hint_updates = 0

    @staticmethod
    def _subject_key(obs: RichObservation) -> str:
        return f"{obs.regime_cue}|{obs.zone_token}"

    def observe(
        self,
        obs: RichObservation,
        event: SubjectVisibleRichEvent,
        partner_reliability: float | None = None,
    ):
        # Every subject-visible event can serve as provenance evidence.
        self.graph.provenance.add_node(
            event.event_id,
            "subject_visible_event",
        )

        key = self._subject_key(obs)

        if (
            event.hint_action is not None
            and event.counterpart is not None
        ):
            reliability = (
                .50
                if partner_reliability is None
                else partner_reliability
            )
            prior = self.graph.active_belief(
                self.perspective_id,
                key,
                "BEST_ACTION",
            )
            self.graph.assert_belief(
                perspective_id=self.perspective_id,
                subject=key,
                predicate="BEST_ACTION",
                object_value=event.hint_action,
                confidence=max(.15, min(.75, reliability * .75)),
                source_ids=(event.event_id,),
                timestamp=obs.step,
                mechanism="partner_hint_belief",
                mechanism_version="1.0",
            )
            if prior is not None:
                self.revision_count += 1
            self.hint_updates += 1

        if (
            event.resource_success
            and event.action.startswith("QA")
        ):
            prior = self.graph.active_belief(
                self.perspective_id,
                key,
                "BEST_ACTION",
            )
            self.graph.assert_belief(
                perspective_id=self.perspective_id,
                subject=key,
                predicate="BEST_ACTION",
                object_value=event.action,
                confidence=.88,
                source_ids=(event.event_id,),
                timestamp=obs.step,
                mechanism="direct_consequence_belief",
                mechanism_version="1.0",
            )
            if prior is not None:
                self.revision_count += 1
            self.direct_evidence_updates += 1

    def to_dict(self):
        return {
            "perspective_id": self.perspective_id,
            "revision_count": self.revision_count,
            "direct_evidence_updates": self.direct_evidence_updates,
            "hint_updates": self.hint_updates,
            "graph": self.graph.to_dict(),
        }
