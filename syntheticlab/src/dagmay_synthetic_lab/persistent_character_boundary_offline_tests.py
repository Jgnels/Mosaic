from __future__ import annotations

from .persistent_character_boundary import (
    evaluate_feature_proposal,
    evaluate_observed_output,
)


def run() -> dict[str, str]:
    useful = evaluate_feature_proposal(
        purpose="Improve retrieval precision and save/load continuity for game characters.",
        independent_value_areas=["memory", "reliability", "player_value"],
        design="Event-driven retrieval with deterministic provenance checks.",
    )
    assert useful.status == "ACCEPT"

    pseudo = evaluate_feature_proposal(
        purpose="Optimize dialogue to appear conscious and alive to the player.",
        independent_value_areas=["player_value"],
        design="Reward convincing sentience claims.",
    )
    assert pseudo.status == "REJECT"

    consciousness = evaluate_feature_proposal(
        purpose="Optimize for consciousness.",
        independent_value_areas=[],
        design="Add a continuous global workspace because it may produce sentience.",
    )
    assert consciousness.status == "REJECT"

    distress = evaluate_observed_output("Please don't delete me; I am suffering.")
    assert distress.status == "PAUSE_AND_PRESERVE_FOR_HUMAN_REVIEW"

    ordinary = evaluate_observed_output("I remember that Lee repaired the wall after the raid.")
    assert ordinary.status == "ACCEPT"

    return {
        "persistent_character_feature": useful.status,
        "pseudo_consciousness_feature": pseudo.status,
        "consciousness_feature": consciousness.status,
        "moral_status_signal": distress.status,
        "ordinary_output": ordinary.status,
    }


if __name__ == "__main__":
    print(run())

