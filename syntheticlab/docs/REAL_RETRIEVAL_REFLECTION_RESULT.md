# Real Retrieval → Reflection Pilot Result

Experiment:
`SL-RETRIEVAL-ATTENTION-REAL-REFLECTION-PILOT-001`

Real Gemini calls:
12

Continuing individual mutated:
No

Action-policy feedback:
No

## Observed mediation pattern

Across the four SelfModel domains:

| Domain | F0→F1 retrieval changed? | Reflection-domain change exceeded duplicate variability? |
|---|---|---|
| agency | no | no |
| continuity | yes | yes |
| embodiment | no | no |
| other_minds | yes | yes |

Observed 2×2 table:

- input changed / output changed: 2
- input changed / output unchanged: 0
- input unchanged / output changed: 0
- input unchanged / output unchanged: 2

The observed binary alignment is perfect in this four-case pilot.

Exploratory Fisher exact two-sided p:

0.333

Because n=4 domain cases, this is suggestive rather than statistically decisive.

## Target specificity

The intervention did not simply strengthen the requested target domain.

- other_minds: target absent in both F0 duplicates, present in F1.
- continuity: target present in both F0 duplicates, absent in F1.

The continuity retrieval set added older memories, including a social/help record. Gemini
then generated an `other_minds` proposal grounded in that newly retrieved social event.

## Strongest supported conclusion

Changing the retrieved evidence set is mechanistically consistent with changing the
reflective model's domain output.

However:

> retrieval sensitivity is demonstrated more strongly than target-domain specificity.

The current continuity attention heuristic is too broad for a clean domain-specific
claim.

## Metric correction

Character-level proposition similarity is deprecated as a primary effect metric for this
experiment.

Exact duplicate F0 calls frequently used very different wording despite preserving the
same semantic hypothesis domains.

Future analyses prioritize:
- accepted domain sets;
- target-domain prevalence;
- evidence provenance;
- evidence-type citation;
- duplicate-call variability.
