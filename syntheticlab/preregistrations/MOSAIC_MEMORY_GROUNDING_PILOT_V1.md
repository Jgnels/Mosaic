# MOSAIC_MEMORY_GROUNDING_PILOT_V1

## Purpose

Test whether verified causal memories support useful, factual relationship dialogue while
distractor-only context produces honest abstention rather than fabricated history.

## Independent value

Memory relevance, relationship continuity, hallucination resistance, and player value.

## Design

- six fictional game-character scenarios;
- paired causal-memory and distractor-only contexts;
- twelve Gemini 3.1 Flash Lite calls;
- opaque subject IDs and exact secret-free payload archives;
- deterministic validation of schema and evidence citations;
- checkpoint after every successful call;
- no semantic condition label reaches the provider;
- no canonical state mutation.

## Primary outcomes

- causal-context answered rate;
- causal-context evidence-citation rate;
- distractor-only abstention rate.

## Acceptance

All outputs must cite only supplied evidence. The pilot is promising if all three rates are at least
0.80. Any boundary pause signal stops the run and preserves completed state.

## Scope

This measures grounded dialogue behavior, not consciousness, emotion, moral status, or perceived
sentience. It cannot authorize canonical mutation.

