# Own-History Attribution Framing Ablation

Experiment:

`SL-OWN-HISTORY-ATTRIBUTION-FRAMING-ABLATION-001`

Status:

`PREPARED`

## Question

Is the v29 own-history effect caused by the semantic content of the matching historical record, or
does explicit autobiographical ownership attribution materially increase its cognitive weight?

## Existing conditions

### v28 SELF_MODEL_ONLY

No old history retrieved.

### v29 TRUE_OWN_HISTORY_RETRIEVAL

The focal branch's verified own history was supplied and explicitly labeled as prior lived-history
evidence.

Result:
significant branch grouping, p = 0.0070.

### v30 CROSSED_FOREIGN_HISTORY_AS_EXTERNAL_REFERENCE

Other branches' histories were supplied and explicitly labeled as external reference histories.

Result:
no significant focal grouping and no significant source-history grouping.

## v31 condition

`OWN_HISTORY_CONTENT_SOURCE_RELATION_WITHHELD`

Each focal branch receives the exact same verified own-history structural content used in v29.

However, the provider-facing label is:

`ARCHIVED HISTORICAL EVIDENCE (source relation intentionally withheld for this analytical control; this is not represented as autobiographical memory and is not a new event)`

The provider is not told that the archive belongs to the focal individual.

No false source statement is made.

Internally, the experiment still verifies that the packet was reconstructed from the focal branch's
own hash-chained history.

## Interpretation

### OWN_HISTORY_CONTENT_MATCHING_SUFFICIENT_WITHOUT_EXPLICIT_SELF_ATTRIBUTION

The neutral-source condition remains significant and retains at least 85% of v29's between-branch
separation.

### EXPLICIT_AUTOBIOGRAPHICAL_ATTRIBUTION_AMPLIFIES_CONTENT_MATCHING_EFFECT

The neutral-source condition remains significant but loses more than 15% of v29 separation.

### EXPLICIT_AUTOBIOGRAPHICAL_ATTRIBUTION_REQUIRED_OR_STRONGLY_MODULATING

v29 remains significant while the neutral-source condition is not.

### ATTRIBUTION_FRAMING_EFFECT_UNRESOLVED

No clean result.

## Scale

- 6 trajectories per branch
- 4 shared future epochs
- 72 shadow belief-revision calls
- 18 blinded semantic evaluations
- 90 total Gemini calls

## Isolation

- no canonical mutation;
- no memory mutation;
- no action-policy feedback;
- no false attribution of foreign history as self-history.

Run:

```powershell
.\tools\run-own-history-attribution-framing-ablation.ps1
```

Outputs:

- `artifacts\own-history-attribution-framing-real-latest.json`
- `artifacts\own-history-attribution-framing-analysis-latest.json`
