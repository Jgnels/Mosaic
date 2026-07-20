from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Dict, Iterable, List, Tuple, Any
import copy
import hashlib
import json


MASK64 = (1 << 64) - 1


class DeterministicRng:
    """Small SplitMix64-based deterministic RNG.

    We use an explicit algorithm instead of Python's `random` module so that
    experiment streams remain stable across Python versions.
    """

    def __init__(self, seed: int):
        self.state = seed & MASK64

    def next_u64(self) -> int:
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64
        z = self.state
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9 & MASK64
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB & MASK64
        return (z ^ (z >> 31)) & MASK64

    def random(self) -> float:
        return self.next_u64() / float(1 << 64)

    def randrange(self, n: int) -> int:
        if n <= 0:
            raise ValueError("n must be > 0")
        return self.next_u64() % n

    def choice(self, items):
        if not items:
            raise ValueError("cannot choose from an empty collection")
        return items[self.randrange(len(items))]

    def shuffle(self, items):
        for i in range(len(items) - 1, 0, -1):
            j = self.randrange(i + 1)
            items[i], items[j] = items[j], items[i]


def stable_unit_float(*parts: Any) -> float:
    """Stable pseudo-random float in [0,1), keyed by arbitrary values."""
    payload = "|".join(str(p) for p in parts).encode("utf-8")
    digest = hashlib.sha256(payload).digest()
    value = int.from_bytes(digest[:8], "big")
    return value / float(1 << 64)


def canonical_hash(value: Any) -> str:
    encoded = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


@dataclass(frozen=True)
class Experience:
    event_id: str
    timestep: int
    context: str
    action: str
    outcome: int
    actor: str = "E17"
    counterpart: str | None = None

    def to_dict(self) -> dict:
        return asdict(self)


class ProvenanceIndex:
    """Tracks direct evidence ancestry for derived records.

    This is intentionally simple in v0.1. It proves the contract we want:
    derived state can always name the source event IDs that contributed to it.
    """

    def __init__(self):
        self._parents: Dict[str, List[str]] = {}

    def add(self, derived_id: str, source_ids: Iterable[str]) -> None:
        unique = sorted(set(source_ids))
        self._parents[derived_id] = unique

    def parents(self, derived_id: str) -> List[str]:
        return list(self._parents.get(derived_id, []))

    def to_dict(self) -> dict:
        return dict(sorted(self._parents.items()))


class SerializableState:
    def clone(self):
        return copy.deepcopy(self)

    def state_hash(self) -> str:
        return canonical_hash(self.to_state_dict())

    def to_state_dict(self) -> dict:
        raise NotImplementedError
