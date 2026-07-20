from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass
from typing import Dict, Iterable, List, Tuple

from .core import Experience, ProvenanceIndex, SerializableState


@dataclass
class BinomialEvidence:
    successes: int = 0
    trials: int = 0

    def observe(self, outcome: int) -> None:
        self.trials += 1
        self.successes += int(outcome)

    @property
    def posterior_mean(self) -> float:
        # Beta(1,1) prior.
        return (self.successes + 1.0) / (self.trials + 2.0)

    def to_dict(self) -> dict:
        return {
            "successes": self.successes,
            "trials": self.trials,
            "posterior_mean": self.posterior_mean,
        }


class ContextualCausalLearner(SerializableState):
    """Learns P(success | opaque context, opaque action)."""

    def __init__(self, contexts: Iterable[str], actions: Iterable[str]):
        self.contexts = tuple(contexts)
        self.actions = tuple(actions)
        self.stats: Dict[Tuple[str, str], BinomialEvidence] = {
            (c, a): BinomialEvidence() for c in self.contexts for a in self.actions
        }
        self.evidence_ids: Dict[Tuple[str, str], List[str]] = defaultdict(list)
        self.provenance = ProvenanceIndex()

    def observe(self, exp: Experience) -> None:
        key = (exp.context, exp.action)
        self.stats[key].observe(exp.outcome)
        self.evidence_ids[key].append(exp.event_id)
        derived_id = f"estimate:{exp.context}:{exp.action}"
        self.provenance.add(derived_id, self.evidence_ids[key])

    def choose(self, context: str) -> str:
        # Deterministic tie-break by action token.
        return max(
            sorted(self.actions),
            key=lambda a: self.stats[(context, a)].posterior_mean,
        )

    def score(self, context: str, action: str) -> float:
        return self.stats[(context, action)].posterior_mean

    def evidence_for(self, context: str, action: str) -> List[str]:
        return list(self.evidence_ids[(context, action)])

    def to_state_dict(self) -> dict:
        return {
            "type": "ContextualCausalLearner",
            "contexts": self.contexts,
            "actions": self.actions,
            "stats": {
                f"{c}|{a}": self.stats[(c, a)].to_dict()
                for c in self.contexts for a in self.actions
            },
            "evidence_ids": {
                f"{c}|{a}": list(self.evidence_ids[(c, a)])
                for c in self.contexts for a in self.actions
            },
            "provenance": self.provenance.to_dict(),
        }


class GlobalFrequencyLearner(SerializableState):
    """Control: learns which action is globally good but cannot condition on context."""

    def __init__(self, actions: Iterable[str]):
        self.actions = tuple(actions)
        self.stats = {a: BinomialEvidence() for a in self.actions}
        self.evidence_ids: Dict[str, List[str]] = defaultdict(list)

    def observe(self, exp: Experience) -> None:
        self.stats[exp.action].observe(exp.outcome)
        self.evidence_ids[exp.action].append(exp.event_id)

    def choose(self, context: str) -> str:
        del context
        return max(sorted(self.actions), key=lambda a: self.stats[a].posterior_mean)

    def to_state_dict(self) -> dict:
        return {
            "type": "GlobalFrequencyLearner",
            "actions": self.actions,
            "stats": {a: self.stats[a].to_dict() for a in self.actions},
            "evidence_ids": {a: list(self.evidence_ids[a]) for a in self.actions},
        }


class NoLearningAgent(SerializableState):
    """Control: fixed deterministic action; expected to perform at chance on average."""

    def __init__(self, actions: Iterable[str]):
        self.actions = tuple(actions)

    def observe(self, exp: Experience) -> None:
        del exp

    def choose(self, context: str) -> str:
        # A context-independent deterministic baseline.
        del context
        return sorted(self.actions)[0]

    def to_state_dict(self) -> dict:
        return {"type": "NoLearningAgent", "actions": self.actions}


class OracleKnowledgeAgent(SerializableState):
    """Positive control: supplied the answer rather than learning it.

    This is deliberately included to remind us that behavioral success alone
    cannot tell us whether a capability came from lived experience.
    """

    def __init__(self, mapping: Dict[str, str]):
        self.mapping = dict(mapping)

    def observe(self, exp: Experience) -> None:
        del exp

    def choose(self, context: str) -> str:
        return self.mapping[context]

    def to_state_dict(self) -> dict:
        return {"type": "OracleKnowledgeAgent", "mapping": dict(sorted(self.mapping.items()))}


class PartnerContingencyLearner(SerializableState):
    """Learns expected beneficial outcome conditioned on counterpart identity."""

    def __init__(self, partners: Iterable[str]):
        self.partners = tuple(partners)
        self.stats = {p: BinomialEvidence() for p in self.partners}
        self.evidence_ids: Dict[str, List[str]] = defaultdict(list)

    def observe_partner_outcome(self, partner: str, outcome: int, event_id: str) -> None:
        self.stats[partner].observe(outcome)
        self.evidence_ids[partner].append(event_id)

    def prefer(self) -> str:
        return max(sorted(self.partners), key=lambda p: self.stats[p].posterior_mean)

    def score(self, partner: str) -> float:
        return self.stats[partner].posterior_mean

    def to_state_dict(self) -> dict:
        return {
            "type": "PartnerContingencyLearner",
            "partners": self.partners,
            "stats": {p: self.stats[p].to_dict() for p in self.partners},
            "evidence_ids": {p: list(self.evidence_ids[p]) for p in self.partners},
        }


class AgencyCouplingLearner(SerializableState):
    """Learns which opaque entity's action predicts a private internal-state change.

    This is intentionally *not* called a self model. It measures one precursor:
    learned causal agency attribution.
    """

    def __init__(self, entities: Iterable[str]):
        self.entities = tuple(entities)
        # entity -> action_bit -> evidence
        self.stats = {
            entity: {0: BinomialEvidence(), 1: BinomialEvidence()}
            for entity in self.entities
        }
        self.evidence_ids = {
            entity: {0: [], 1: []}
            for entity in self.entities
        }

    def observe(self, entity: str, action_bit: int, signal_changed: int, event_id: str) -> None:
        self.stats[entity][action_bit].observe(signal_changed)
        self.evidence_ids[entity][action_bit].append(event_id)

    def coupling_score(self, entity: str) -> float:
        # Magnitude of intervention-conditioned difference.
        p0 = self.stats[entity][0].posterior_mean
        p1 = self.stats[entity][1].posterior_mean
        return abs(p1 - p0)

    def most_agentic_entity(self) -> str:
        return max(sorted(self.entities), key=self.coupling_score)

    def to_state_dict(self) -> dict:
        return {
            "type": "AgencyCouplingLearner",
            "entities": self.entities,
            "scores": {e: self.coupling_score(e) for e in self.entities},
            "stats": {
                e: {str(a): self.stats[e][a].to_dict() for a in (0, 1)}
                for e in self.entities
            },
            "evidence_ids": {
                e: {str(a): list(self.evidence_ids[e][a]) for a in (0, 1)}
                for e in self.entities
            },
        }
