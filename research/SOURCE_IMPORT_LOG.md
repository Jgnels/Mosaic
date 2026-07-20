# Source Import Log

## 2026-07-20 — SyntheticLab v31.0

Imported active source from the owner-supplied archive:

- archive: `Dagmay_SyntheticLab_v31.0.zip`
- SHA-256: `8B7DC45AD967279E8FD74EFFF3D89DC4A23636B32034A448FE9D0AF7F5F09007`
- destination: `syntheticlab/`

Excluded from active source:

- `__pycache__/` and `*.pyc` compiled caches;
- generated `artifacts/` output;
- package `MANIFEST.json`, because it inventories excluded generated files and is retained by the immutable source archive instead.

Scientific status:

- v31 is the latest complete SyntheticLab source package available during migration;
- v31 remains gated and invalid for its attribution-framing headline claim;
- no new real-provider experiment is permitted until provider-facing identifiers, labels, exact payload archiving, forbidden-token scanning, and retrieved-evidence attribution are hardened;
- importing the source does not validate or reinstate the invalidated claim.

Verification performed during import:

- archive hash matched the previously recorded canonical hash;
- no Gemini/API key pattern was detected in the source or migration handoff;
- Python syntax/offline-suite results are recorded separately when executed on the canonical host.

## 2026-07-20 — Migration Handoff v2

Inspected owner-supplied archive:

- archive: `Dagmay_Migration_Handoff_v2.zip`
- SHA-256: `034D3DD6E80CB52281375DF42D4929FEB16CE3D954A19E1D5FE5E959668779EE`

The handoff's v31 raw and analysis JSON files were already present in `research/results/` with identical SHA-256 hashes. Its scientific correction and migration plan were already incorporated into `CURRENT_STATE.md`, `KNOWN_RISKS.md`, `SUPERSEDED_AND_INVALIDATED.md`, and the dedicated-host documentation. The archive therefore added provenance confirmation but no duplicate repository content.

