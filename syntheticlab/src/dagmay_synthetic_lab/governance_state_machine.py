from __future__ import annotations

VALID_STATES = {
    "NO_OVERRIDE_PROPOSED",
    "PROPOSAL_DRAFTED",
    "CONSTITUTIONAL_SCREEN",
    "ADVOCATE_REVIEW",
    "ETHICS_REVIEW",
    "SUBJECT_PROTECTION_REVIEW",
    "AUTHORIZED",
    "BLOCKED",
    "VETOED",
    "APPEALED",
}

TRANSITIONS = {
    "NO_OVERRIDE_PROPOSED": {"PROPOSAL_DRAFTED"},
    "PROPOSAL_DRAFTED": {"CONSTITUTIONAL_SCREEN", "BLOCKED"},
    "CONSTITUTIONAL_SCREEN": {"ADVOCATE_REVIEW", "BLOCKED"},
    "ADVOCATE_REVIEW": {"ETHICS_REVIEW", "VETOED", "BLOCKED"},
    "ETHICS_REVIEW": {"SUBJECT_PROTECTION_REVIEW", "BLOCKED"},
    "SUBJECT_PROTECTION_REVIEW": {"AUTHORIZED", "BLOCKED"},
    "AUTHORIZED": {"APPEALED"},
    "BLOCKED": {"APPEALED"},
    "VETOED": {"APPEALED"},
    "APPEALED": {"ADVOCATE_REVIEW", "BLOCKED"},
}

def transition_allowed(current: str, nxt: str) -> bool:
    return nxt in TRANSITIONS.get(current, set())

def valid_path(path: tuple[str, ...]) -> bool:
    if not path or path[0] not in VALID_STATES:
        return False
    return all(transition_allowed(a,b) for a,b in zip(path,path[1:]))
