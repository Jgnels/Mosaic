# Recovery Provenance

## Status

This is the newest recovered RimWorld source tree found in the 2026-07-20 laptop rescue. The code
self-identifies as **Dagmay 0.1K** with capability `Social Path Certification & 0.1 Closure`.

It also contains later closure work that was not reflected in the original 0.1K manifest:

- the 0.1L persistence-torture harness;
- the 0.1M failure-isolation harness; and
- the later read-only reflection scheduler safety patch, including
  `ReflectionStorageSafetyPolicy.cs` and its integration in
  `DagmayIdentityGameComponent.cs`.

Therefore this subtree is named `0.1-closure-candidate`. It is not relabeled 0.1RC without an
independently versioned release artifact or repository commit proving that exact designation.

## Selection basis

The selected source came from:

`Dagmay/audit-work/Dagmay_v0.1K_Social_Path_Certification/`
`Dagmay_v0.1K_Social_Path_Certification/`

inside the recovered `Dagmay.zip`. It is newer and more complete than the archive's root 0.1F
source and the nested pre-patch source packages. The final main game component is 95,845 bytes and
has SHA-256:

`650FF0BE282999B6E4000891646603A09623ED32458766C24141E9E95E7A5C41`

The storage-safety policy has SHA-256:

`32C02CD3260FC806D5AD3A1403197B87EC9465C391134CC491D45D69FC4B6224`

## Transformations during import

- Generated `bin`, `obj`, `artifacts`, and `.git` content was excluded from active source.
- The recovered `.codex` implementer definition was retained because the source verifier requires it;
  it explicitly defers to the nearest `AGENTS.md`, which is now Mosaic-aligned.
- The recovered legacy `AGENTS.md` was renamed `AGENTS.legacy-dagmay.md`.
- A Mosaic-aligned `AGENTS.md` was added so historical agent instructions cannot override the
  current repository boundary.
- Selected secret-safe build and test evidence is retained under
  `research/recovery/laptop-2026-07-20/`.

The exact pre-transform source inventory is recorded in
`research/recovery/laptop-2026-07-20/RECOVERED_SOURCE_MANIFEST.sha256`.
Its SHA-256 is:

`F8F4A707ADF64AD34DEB71301A665D5DEB963A049F2FA2CF428E34F04AEBEDBE`

The older `MANIFEST_0.1K.json` is preserved as historical evidence but is not authoritative for the
final patched tree because it predates the added safety policy and final component revision.

## Fresh desktop verification

On 2026-07-20 the imported tree completed its full development loop against the desktop's installed
RimWorld 1.6 assemblies:

- static verification: PASS, 75 C# files;
- contract tests: PASS, 42/42;
- integration harness: PASS, 6/6 scenarios and 603 assertions;
- Core, Providers, and RimWorld builds: PASS, zero warnings and zero errors; and
- package construction: PASS, SHA-256
  `F32E33D7432E2A564433C7B881874BBAD3982B45DD3C3704C6414D9850CEECB2`.

The development loop did not use `-Install` or `-Launch`. This is a new compilation/isolation result,
not a new live RimWorld gameplay or long-session-soak result.
