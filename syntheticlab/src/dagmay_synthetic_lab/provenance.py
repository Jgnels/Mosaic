from __future__ import annotations

from collections import defaultdict, deque
from dataclasses import dataclass, asdict
from typing import Dict, Iterable, List, Set, Tuple

from .core import canonical_hash


@dataclass(frozen=True)
class ProvenanceNode:
    node_id: str
    node_type: str
    mechanism: str
    mechanism_version: str


class ProvenanceDag:
    """Tracks ancestry of evidence and derived state.

    The central research purpose is to prevent independent-treatment errors:
    a belief, reflection, and self-concept that all descend from the same event
    are not three independent pieces of evidence.
    """

    def __init__(self):
        self.nodes: Dict[str, ProvenanceNode] = {}
        self.parents: Dict[str, Set[str]] = defaultdict(set)

    def add_node(
        self,
        node_id: str,
        node_type: str,
        mechanism: str = "external",
        mechanism_version: str = "1",
        parents: Iterable[str] = (),
    ) -> None:
        if node_id in self.nodes:
            existing = self.nodes[node_id]
            candidate = ProvenanceNode(node_id, node_type, mechanism, mechanism_version)
            if existing != candidate:
                raise ValueError(f"Provenance node {node_id} redefined inconsistently.")
        else:
            self.nodes[node_id] = ProvenanceNode(
                node_id=node_id,
                node_type=node_type,
                mechanism=mechanism,
                mechanism_version=mechanism_version,
            )
        for parent in parents:
            if parent == node_id:
                raise ValueError("A provenance node cannot be its own parent.")
            self.parents[node_id].add(parent)

    def ancestors(self, node_id: str) -> Set[str]:
        result: Set[str] = set()
        queue = deque(self.parents.get(node_id, set()))
        while queue:
            current = queue.popleft()
            if current in result:
                continue
            result.add(current)
            queue.extend(self.parents.get(current, set()))
        return result

    def root_ancestors(self, node_id: str) -> Set[str]:
        ancestors = self.ancestors(node_id)
        roots = set()
        for candidate in ancestors:
            if not self.parents.get(candidate):
                roots.add(candidate)
        if not ancestors and node_id in self.nodes:
            roots.add(node_id)
        return roots

    def overlap(self, node_ids: Iterable[str]) -> Dict[str, object]:
        ids = list(node_ids)
        roots = {node_id: self.root_ancestors(node_id) for node_id in ids}
        duplicated_roots: Set[str] = set()
        for i in range(len(ids)):
            for j in range(i + 1, len(ids)):
                duplicated_roots |= roots[ids[i]] & roots[ids[j]]
        return {
            "nodes": ids,
            "root_ancestors": {k: sorted(v) for k, v in roots.items()},
            "shared_root_evidence": sorted(duplicated_roots),
            "has_shared_root_evidence": bool(duplicated_roots),
        }

    def to_dict(self) -> dict:
        return {
            "nodes": {
                node_id: asdict(node)
                for node_id, node in sorted(self.nodes.items())
            },
            "parents": {
                node_id: sorted(parents)
                for node_id, parents in sorted(self.parents.items())
            },
            "hash": self.state_hash(),
        }

    def state_hash(self) -> str:
        return canonical_hash({
            "nodes": {
                node_id: asdict(node)
                for node_id, node in sorted(self.nodes.items())
            },
            "parents": {
                node_id: sorted(parents)
                for node_id, parents in sorted(self.parents.items())
            },
        })
