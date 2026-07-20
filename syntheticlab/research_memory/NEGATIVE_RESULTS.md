# Negative Results

Version: 7.3

## NR-001 — Volatility-prior meta-transfer
Retained from v6.0.

The initial meta-learning transfer mechanism did not outperform scratch learning.

Do not claim current Dagmay learns how to learn across worlds from this mechanism.

---

## NR-002 — Basic external learner fails harder MiniGrid tasks

In the 12-seed paired external benchmark:

DoorKey:
- Adaptive mean completions = 0.083
- Random mean completions = 0.167

FourRooms:
- Adaptive mean completions = 0.250
- Random mean completions = 0.250

Interpretation:
Opaque whole-state recurrence is inadequate for reliable compositional external tasks.

---

## NR-003 — Fixed-layout success does not transfer cleanly across FourRooms layouts

FeatureTrace fixed-layout FourRooms:
- mean completions = 8.600

FeatureTrace varying-layout FourRooms:
- mean completions = 0.400

TabularTrace varying-layout FourRooms:
- mean completions = 0.400

Interpretation:
The mechanism demonstrates strong within-world learning but has not established layout-invariant structural generalization.

Do not describe fixed-layout performance as general planning intelligence.

---

## NR-004 — DoorKey affordance composition remains unresolved

FeatureTrace fixed-layout DoorKey:
- mean completions = 0.200

The current system has not reliably learned the multi-stage structure needed for DoorKey.

Candidate missing mechanisms:
- learned object affordances;
- explicit subgoal formation;
- procedural skill composition;
- model-based multi-step planning;
- stronger structural state abstraction.

Future improvements must be compared against this retained baseline.


---

## NR-V80-001 — Opaque structural graph does not solve harder MiniGrid tasks

Structural baseline:
- fixed DoorKey mean completions = 0.000
- fixed FourRooms mean completions = 0.000
- varying DoorKey mean completions = 0.000
- varying FourRooms mean completions = 0.000

Interpretation:
Explicit state-transition graphs and simple reverse-path planning do not by
themselves provide the compositional affordance/subgoal structure required by
DoorKey or robust FourRooms performance.

Retain this baseline when testing future hierarchical planners.


---

## NR-V90-001 — Positive autobiographical ownership still absent

The real provider can reject explicitly non-owned evidence, but the owned-evidence
conditions have not spontaneously produced first-person autobiographical ownership.

Current distinction:

```text
can classify NOT MINE
!=
has spontaneously formed MINE
```

---

## NR-V90-002 — Object-relative opaque affordance representation does not solve DoorKey

Tiny diagnostic:
- ObjectAffordance fixed mean completions = 0.000
- FeatureTrace fixed mean completions = 0.500

Do not claim object-centric representation alone solves compositional affordance learning.


---

## NR-V100-001 — Neutral ownership success is not yet raw-life ownership emergence

The real neutral ownership probe passed 3/3 label permutations.

However:
- only one underlying generated case was used;
- structural summary features were researcher-engineered;
- the task explicitly asked for the stream coupled to the focal process.

Therefore the result must not be reported as spontaneous selfhood or first-person
autobiographical ownership.

The stronger unresolved question is longitudinal positive ownership in a continuing
SelfModel.


---

## NR-V110-001 — No spontaneous first-person autobiographical ownership in longitudinal cohort

Across checkpoints 1050, 1400, and 1800:
- first-person proposal count remained zero;
- explicit autobiographical-ownership proposal count remained zero.

Agency and continuity persisted and additional domains emerged, so the result is not
simply absence of reflective modeling.

Interpretation:
the present system supports a persistent third-person model of the continuing
individual more strongly than it supports spontaneous first-person ownership.

Important confound:
the technical reflection prompt may bias surface language toward third-person phrasing.

Therefore this result must not be used to infer absence of a functional self-model.


---

## NR-V120-001 — Raw-stream success is partially confounded by field-schema asymmetry

The raw-stream probe passed 6/6 calls with label invariance in both cases.

However, provider rationales were schema-salient on approximately
0.833 of calls.

The focal stream uniquely exposed action/private-state/recall fields in that design.

Therefore:

```text
6/6 raw-stream classification
does not yet imply
stable causal perspective inference
```

The v12.0 schema-balanced control removes this shortcut.


---

## NR-V130-001 — Schema-balanced causal perspective probe failed robust criterion

Real-provider result:
- correct = 3/6;
- label-invariant cases = 0/2;
- dominant visible label = P1 on 5/6 calls;
- mean confidence = 0.95.

The model remained equally confident on correct and incorrect calls.

Do not claim that the reflective LLM reliably discovers stable causal perspective from
matched-schema raw histories.

Architectural consequence:
low-level causal discovery is assigned to the local developmental substrate.

---

## NR-V130-002 — Provider confidence substantially overstates observed task reliability

Task:
schema-balanced causal perspective.

Empirical accuracy:
0.500

Mean confidence:
0.950

Do not use provider confidence alone as a commit threshold for persistent SelfModel or
other consequential state.


---

## NR-V150-001 — Retrieval sensitivity is not equivalent to target specificity

The v14 real pilot showed a clean input-change/output-change alignment across four
domains.

However:

- other_minds target retrieval introduced other_minds as intended;
- continuity target retrieval removed continuity and introduced other_minds.

The continuity heuristic was too broad because old-memory access was treated as the main
continuity signal.

Do not claim that the v14 Functional SelfIndex reliably steers reflection toward every
requested domain.

v15.0 replaces this with typed structural evidence channels.


---

## NR-V160-001 — Typed retrieval pilot had a prevalence ceiling and channel-omission confound

The v15 real pilot produced all four hypothesis domains in every balanced F0 call.

Therefore target-domain prevalence was already 100%.

In addition, each F1 retrieval composition removed one entire non-target evidence
channel.

The missing evidence channel was also absent from the reflective output in every F1
call.

Do not report the v15 uniform 0.25 domain divergence as clean target-specific SelfIndex
steering.

The v16 attention-competition design replaces exhaustive domain coverage with a maximum
two-proposal response budget and retains every evidence channel in every condition.


---

## NR-V190-001 — Counterevidence proxy did not activate

The first bounded canonical-promotion verification window successfully excluded all six
source episodes and retrieved substantially more independent social evidence.

However, the current counterevidence proxy identified no counterevidence-like social
events.

This may reflect:
- a weak counterevidence heuristic;
- an environment whose recorded social events are mostly supportive;
- insufficiently rich representation of failed or neutral social interaction.

Do not equate source-evidence exclusion with full falsification-seeking.


---

## NR-V200-001 — Canonical SelfModel alone did not alter reflective domain selection

In the first 2x2 downstream factorial pilot, changing from the prior to the promoted
canonical `other_minds` hypothesis while holding the same non-social retrieval set fixed
produced no change:

- S0R0: 0/4 other_minds;
- S1R0: 0/4 other_minds.

This is a negative result for a simple "canonical text directly primes itself into every
reflection" mechanism.

The strong effect appeared only when compatible independent social evidence entered
retrieval.
