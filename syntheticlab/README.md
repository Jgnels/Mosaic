# Dagmay SyntheticLab v31.0

v31.0 follows the v30 finding:

`OWN_HISTORY_ALIGNMENT_SPECIFICITY_SUPPORTED`

True own-history retrieval produced significant historical-branch grouping.

Crossed foreign reference histories produced neither significant focal-branch grouping nor
significant grouping by the external history source.

## Remaining question

Was that because the matching historical content matters, or because the provider was explicitly
told that the material was the focal individual's own history?

v31 supplies the exact focal branch's own historical content but withholds the provider-facing
ownership relation.

No false provenance is supplied.

Run:

```powershell
.\tools\run-own-history-attribution-framing-ablation.ps1
```

90 Gemini calls.

No canonical state or memory content is modified.
