# Next Input Needed

v30 supported own-history alignment specificity, but explicit source framing remains a confound.

Run:

```powershell
.\tools\run-own-history-attribution-framing-ablation.ps1
```

This makes:

- 72 shadow belief-revision calls;
- 18 blinded semantic evaluations;
- 90 total Gemini calls.

The exact own-history content is supplied, but the provider is not told that it belongs to the focal
individual.

Upload:

- `artifacts\own-history-attribution-framing-real-latest.json`
- `artifacts\own-history-attribution-framing-analysis-latest.json`

No canonical state or memory content is changed.
