# Preregistration — SL-TEMPORAL-BELIEF-001

## Goal
Validate non-destructive belief revision semantics.

## Required properties
- perspective-specific proposition key;
- source evidence IDs;
- confidence;
- valid-from / valid-to;
- prior belief preserved;
- supersession chain preserved;
- final active belief separately queryable.

## Scenario
Direct evidence supports DOYLE reliable=YES.
Later hearsay supports NO.
Later direct evidence plus retraction supports YES.

## Interpretation
This validates storage semantics only. It does not validate the chosen confidence-update psychology.
