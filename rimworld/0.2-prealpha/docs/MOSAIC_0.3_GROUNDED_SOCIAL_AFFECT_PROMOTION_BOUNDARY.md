# Mosaic 0.3 Grounded Social Affect Promotion Boundary

Exact base: `c4e27be9d6883ce5a8369e5551708ffba80d5a5c` (draft PR #10 head).

This candidate promotes inert, deterministic contracts only:

- typed sanitization of the two PR #10 social event kinds;
- exact PR #10 deduplication verification;
- explicit source admission positions and observation batch IDs;
- one compound bundle per capture batch, preserving 1–2 exact EventIds;
- actor-owned, relationship-private appraisal only;
- conservative owner-calibrated meaning without personality caricature;
- constant-space fixed-point affect state;
- internal-only, tamper-evident proposal/commit;
- deterministic affect checkpoint contract;
- pure RimWorld projection bridge with no Pawn/Map/Def/job access.

It does not wire the contracts into `DagmayIdentityGameComponent`, add save fields, call a provider,
display a reaction, alter dialogue, create player-visible knowledge, or issue any RimWorld action.
Runtime behavior must remain unchanged.

The C# candidate is not yet compiler-certified. Offline Python results do not substitute for the
real Core/Tests/RimWorld compilation gate.
