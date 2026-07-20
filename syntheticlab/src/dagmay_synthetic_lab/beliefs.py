from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict, Iterable, List, Optional, Set, Tuple
from collections import defaultdict

from .core import canonical_hash
from .provenance import ProvenanceDag


@dataclass
class BeliefRecord:
    belief_id: str
    perspective_id: str
    subject: str
    predicate: str
    object_value: str
    confidence: float
    source_ids: tuple[str, ...]
    valid_from: int
    valid_to: int | None = None
    supersedes: str | None = None
    contradicted_by: tuple[str, ...] = ()
    status: str = "ACTIVE"
    mechanism: str = "belief_graph"
    mechanism_version: str = "0.1"

    def proposition_key(self) -> tuple[str, str, str]:
        return (self.perspective_id, self.subject, self.predicate)

    def to_dict(self) -> dict:
        return asdict(self)


class TemporalBeliefGraph:
    """Dagmay-native prototype inspired by temporal graph/provenance systems.

    It does not overwrite old beliefs. Revision closes validity on the prior
    belief and creates a new record with explicit lineage.
    """

    def __init__(self):
        self.records: Dict[str, BeliefRecord] = {}
        self.active_by_key: Dict[tuple[str, str, str], str] = {}
        self.provenance = ProvenanceDag()
        self._counter = 0

    def _next_id(self) -> str:
        self._counter += 1
        return f"B{self._counter:06d}"

    def assert_belief(
        self,
        perspective_id: str,
        subject: str,
        predicate: str,
        object_value: str,
        confidence: float,
        source_ids: Iterable[str],
        timestamp: int,
        mechanism: str = "belief_inducer",
        mechanism_version: str = "0.1",
    ) -> BeliefRecord:
        if not 0.0 <= confidence <= 1.0:
            raise ValueError("confidence must be in [0,1]")
        source_ids = tuple(sorted(set(source_ids)))
        key = (perspective_id, subject, predicate)
        prior_id = self.active_by_key.get(key)

        supersedes = None
        if prior_id is not None:
            prior = self.records[prior_id]
            if prior.object_value == object_value:
                # Reinforcement creates a new version so history remains explicit.
                prior.valid_to = timestamp
                prior.status = "SUPERSEDED"
                supersedes = prior.belief_id
            else:
                prior.valid_to = timestamp
                prior.status = "CONTRADICTED"
                supersedes = prior.belief_id

        belief_id = self._next_id()
        record = BeliefRecord(
            belief_id=belief_id,
            perspective_id=perspective_id,
            subject=subject,
            predicate=predicate,
            object_value=object_value,
            confidence=confidence,
            source_ids=source_ids,
            valid_from=timestamp,
            supersedes=supersedes,
            mechanism=mechanism,
            mechanism_version=mechanism_version,
        )
        self.records[belief_id] = record
        self.active_by_key[key] = belief_id

        self.provenance.add_node(
            belief_id,
            "belief",
            mechanism,
            mechanism_version,
            source_ids,
        )
        return record

    def active_belief(self, perspective_id: str, subject: str, predicate: str) -> BeliefRecord | None:
        key = (perspective_id, subject, predicate)
        belief_id = self.active_by_key.get(key)
        return self.records.get(belief_id) if belief_id else None

    def history(self, perspective_id: str, subject: str, predicate: str) -> List[BeliefRecord]:
        key = (perspective_id, subject, predicate)
        return sorted(
            [r for r in self.records.values() if r.proposition_key() == key],
            key=lambda r: (r.valid_from, r.belief_id),
        )

    def state_hash(self) -> str:
        return canonical_hash({
            "records": {k: v.to_dict() for k, v in sorted(self.records.items())},
            "active_by_key": {
                "|".join(k): v for k, v in sorted(self.active_by_key.items())
            },
            "provenance": self.provenance.to_dict(),
        })

    def to_dict(self) -> dict:
        return {
            "records": {k: v.to_dict() for k, v in sorted(self.records.items())},
            "active_by_key": {
                "|".join(k): v for k, v in sorted(self.active_by_key.items())
            },
            "provenance": self.provenance.to_dict(),
            "state_hash": self.state_hash(),
        }


def run_rumor_retraction_scenario() -> dict:
    """Controlled belief revision with truth, hearsay, contradiction, and retraction."""
    g = TemporalBeliefGraph()

    # Canonical evidence nodes exist outside belief graph.
    for event_id in ("E-DIRECT-1", "E-RUMOR-1", "E-DIRECT-2", "E-RETRACT-1"):
        g.provenance.add_node(event_id, "canonical_event")

    b1 = g.assert_belief(
        perspective_id="MICHAEL",
        subject="DOYLE",
        predicate="reliable",
        object_value="YES",
        confidence=0.72,
        source_ids=["E-DIRECT-1"],
        timestamp=10,
        mechanism="direct_experience",
    )
    b2 = g.assert_belief(
        perspective_id="MICHAEL",
        subject="DOYLE",
        predicate="reliable",
        object_value="NO",
        confidence=0.45,
        source_ids=["E-RUMOR-1"],
        timestamp=20,
        mechanism="hearsay",
    )
    b3 = g.assert_belief(
        perspective_id="MICHAEL",
        subject="DOYLE",
        predicate="reliable",
        object_value="YES",
        confidence=0.83,
        source_ids=["E-DIRECT-2", "E-RETRACT-1"],
        timestamp=30,
        mechanism="evidence_reconciliation",
    )

    history = g.history("MICHAEL", "DOYLE", "reliable")
    active = g.active_belief("MICHAEL", "DOYLE", "reliable")

    return {
        "experiment_id": "SL-TEMPORAL-BELIEF-001",
        "history": [r.to_dict() for r in history],
        "active": active.to_dict() if active else None,
        "old_beliefs_preserved": len(history) == 3,
        "active_is_yes": active is not None and active.object_value == "YES",
        "supersession_chain_valid": (
            history[1].supersedes == history[0].belief_id
            and history[2].supersedes == history[1].belief_id
        ),
        "graph": g.to_dict(),
        "interpretation_warning": (
            "This demonstrates temporal/provenance semantics, not a validated human belief-revision rule."
        ),
    }
