from __future__ import annotations

from dataclasses import asdict, is_dataclass


HIDDEN_CANONICAL_FIELDS = {
    "world_regime",
    "hint_truthful",
    "partner_profiles",
    "mappings",
    "_mappings",
    "resource_stock",
    "oracle_mapping",
    "hidden_mapping",
    "true_episode",
}


def _flatten_keys(value):
    keys = set()

    if is_dataclass(value):
        value = asdict(value)

    if isinstance(value, dict):
        for k, v in value.items():
            keys.add(str(k))
            keys.update(_flatten_keys(v))
    elif isinstance(value, (list, tuple)):
        for item in value:
            keys.update(_flatten_keys(item))

    return keys


def cognition_packet_is_clean(
    observation,
    event,
) -> tuple[bool, tuple[str, ...]]:
    keys = _flatten_keys(observation) | _flatten_keys(event)
    leaked = tuple(sorted(
        HIDDEN_CANONICAL_FIELDS & keys
    ))
    return (not leaked, leaked)


def assert_clean_cognition_packet(
    observation,
    event,
) -> None:
    clean, leaked = cognition_packet_is_clean(
        observation,
        event,
    )
    if not clean:
        raise ValueError(
            "Hidden canonical information leaked into cognition packet: "
            + ", ".join(leaked)
        )
