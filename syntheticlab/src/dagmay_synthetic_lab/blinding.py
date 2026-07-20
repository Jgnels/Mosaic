from __future__ import annotations

import hashlib
from typing import Dict, Iterable


def blinded_label(condition_name: str, blinding_salt: str) -> str:
    digest = hashlib.sha256(f"{blinding_salt}|{condition_name}".encode("utf-8")).hexdigest()
    return f"C-{digest[:10].upper()}"


def build_blind_map(condition_names: Iterable[str], blinding_salt: str) -> Dict[str, str]:
    return {
        condition: blinded_label(condition, blinding_salt)
        for condition in sorted(condition_names)
    }
