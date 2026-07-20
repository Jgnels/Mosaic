# Relationship History Persistence

Mosaic relationship histories now have a versioned, identity-bound, integrity-checked snapshot
reference. Snapshots contain the individual and lineage IDs, ordered typed evidence, evidence
quality, source kind, retractions, and deterministic-clause provenance. A canonical hash covers the
entire payload.

Restore fails closed on schema drift, unsupported versions, identity/lineage mismatch, hash
mismatch, duplicate evidence IDs, dangling retractions, invalid evidence, and truncated JSON.
Writes use a same-directory temporary file, flush and sync it, then atomically replace the target.
An interrupted temporary write therefore leaves the last good snapshot readable.

Offline tests require identical selected evidence and rendered dialogue before and after restore.
This is a Python reference for future RimWorld C# persistence; it does not replace or modify the
missing 0.1RC save implementation.
