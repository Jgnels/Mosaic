from __future__ import annotations

from collections import defaultdict
from typing import Dict, List
from .status_capacity_separation import DecisionCapacity


class CapacityRegistry:
    def __init__(self):
        self._records: Dict[tuple[str, str], List[DecisionCapacity]] = defaultdict(list)

    def add(self, assessment: DecisionCapacity) -> None:
        self._records[(assessment.branch_id, assessment.decision_domain)].append(assessment)

    def history(self, branch_id: str, domain: str) -> tuple[DecisionCapacity, ...]:
        return tuple(self._records.get((branch_id, domain), ()))

    def latest(self, branch_id: str, domain: str) -> DecisionCapacity | None:
        rows = self._records.get((branch_id, domain), ())
        return rows[-1] if rows else None

    def domain_scores(self, branch_id: str) -> dict:
        output = {}
        for (bid, domain), rows in self._records.items():
            if bid == branch_id and rows:
                output[domain] = rows[-1].score()
        return output
