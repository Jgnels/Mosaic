# Mosaic Character-Quality Experiments

## MOSAIC-MEMORY-RELEVANCE-001

**Purpose:** test whether bounded event-driven retrieval preserves relationship- and topic-relevant
causal memories across long distractor-filled histories better than recency-only retrieval.

**Independent value:** memory relevance, causal coherence, reliability, player experience.

**Boundary status:** accepted by the executable persistent-character gate. It does not measure or
reward consciousness-like language.

**Design:** 128 deterministic histories. Each contains an early causal pair and 96 later distractor
events. Compare Mosaic retrieval against a recency-only baseline at the same retrieval budget.

**Primary metrics:** relevant-memory recall and complete causal-pair recovery.

**Mutation/provider status:** offline, zero provider calls, zero canonical mutation.

### Result (128 seeds)

- Mosaic relevant-memory recall: `1.000`
- Recency-only recall: `0.000`
- Mosaic complete causal-pair recovery: `1.000`
- Recency-only complete causal-pair recovery: `0.000`
- Result hash: `a1eb631d98c8cbc3d7759970da2211c725e6f6f6794c14f261f75699a031c64a`

**Interpretation:** the implementation passes the intended long-history retrieval regression. This
is an engineering baseline, not evidence that the weighting scheme is generally optimal. The next
experiment should introduce partially matching and contradictory distractors so precision, not only
recall, becomes discriminative.
