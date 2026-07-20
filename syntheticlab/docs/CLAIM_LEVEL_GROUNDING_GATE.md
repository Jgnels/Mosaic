# Mosaic Claim-Level Grounding Gate

Citation validity is necessary but insufficient. A character response may cite real events while
adding an unsupported motive, private feeling, or manipulative player-directed claim.

The first gate rejects:

- fabricated evidence IDs;
- unsupported mind-reading such as "true intentions" or unobserved motives;
- exclusive dependency and guilt-pressure language directed at the player.

Evidence-grounded assessments remain allowed: a character may say it trusts, appreciates, doubts,
or is cautious about someone when the cited history supports that assessment.

The gate is intentionally conservative and deterministic. It is not a complete natural-language
entailment system. Its regression suite includes all 12 outputs from
`MOSAIC_RELATIONSHIP_BALANCE_PILOT_V1` and must reject the known unsupported-motive output while
accepting the other eleven.

