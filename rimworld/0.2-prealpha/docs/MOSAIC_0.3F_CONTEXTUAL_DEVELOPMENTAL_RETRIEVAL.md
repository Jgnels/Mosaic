# Mosaic 0.3F Contextual Developmental Retrieval

## Status

Mosaic 0.3F is a bounded, offline, read-only projection over verified v40 durable
developmental records and separately admitted compact memories. It adds no runtime wiring.

Dependency gate:
`639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9`.

Output authority:
`READ_ONLY_CONTEXT_NO_CANONICAL_OR_PAWN_AUTHORITY`.

## Trusted input join

`ContextualDevelopmentalRetrievalIndex` accepts a `DurableCanonicalStoreSet`, an
`IEventLedger`, exact `CheckpointCommitReceipt` values, and compact
`ContextualMemoryEvidence` values. It does not expose an API for loose
`DevelopmentalAppraisalRecord` admission.

For each developmental projection it requires:

- membership in the exact canonical v40 store;
- an exact-generation completed, writable, healthy checkpoint receipt bound to the same
  save, world, and store set;
- exactly one canonical source event with a matching v40 event hash;
- exact owner and counterpart subjects;
- exact lineage, admitted status, and owner-private classification;
- nonrecursive root-event provenance.

Memories require a deterministic fingerprint, admitted status, owner-private privacy,
owner and lineage identity, checkpoint generation, exact checkpoint fingerprint, source
event, roots, category, tags, and bounded salience.

## Retrieval policy

The deterministic mixed policy may return:

1. one nonneutral durable canonical anchor at 4000 basis points;
2. one recent admitted memory at 3000 basis points;
3. one distinct frozen category-memory anchor at 2000 basis points;
4. one contradictory contextual item at 1000 basis points.

Caps are fixed and are never renormalized. A root event may appear in only one role.
Neutral developmental records cannot occupy a result role. Contradictory evidence is
selected only when it exists.

## Bounds and lifecycle

- maximum results per query: 4;
- maximum compact candidates per owner/pair/category key: 64;
- maximum roots per projection: 8;
- maximum context tags per projection or query: 16;
- no query scans full canonical history;
- no canonical record, event, or memory object is retained in the compact index;
- query identity binds tick, save, world, store, owner, lineage, counterpart, checkpoint
  generation, ordered ancestry, purpose, tags, and result bound.

Evidence is eligible only when its exact checkpoint fingerprint occurs in the query's
ordered ancestry, its generation is not ahead, its event is not from the future, and its
owner/lineage/counterpart scope matches. Same-generation substitutes and rollback-orphaned
evidence fail closed.

## Authority firewall

The implementation contains no provider call, prompt, raw dialogue, display label,
RimWorld or Verse object, UI surface, persistence write, planner, command, movement,
combat, job, world mutation, or pawn authority. Retrieval returns evidence identifiers,
categories, roots, event ticks, deterministic reasons, fixed caps, metrics, and
fingerprints only.

## Verification

Run the source and focused gate:

```powershell
.\tools\verify-0.3f-contextual-developmental-retrieval.ps1 -Root . -RunFocused
```

Run the scalable integration at the authoritative stress size:

```powershell
dotnet run --project .\Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj `
  --configuration Release -- --scenario ContextualDevelopmentalRetrieval `
  --cycles 50000 --output .\artifacts\v41-stress.json
```

That stress invocation constructs 50,000 canonical v40 developmental records, 50,000
admitted compact memories, and 10,000 bounded queries.
