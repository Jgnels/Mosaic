# Mosaic 0.2G - Long-History Retrieval Benchmark

## Purpose

0.2G adds an offline fixed-corpus benchmark for the existing grounded relationship
selector. It does not modify the selector or any packaged runtime source.

The corpus contains exactly 500 prior evidence candidates across ten targeted cases:

1. buried negative direct relationship;
2. buried positive direct relationship;
3. mixed-valence direct history;
4. privacy filtering;
5. non-experienced filtering;
6. equal-score EventId tie-break;
7. future-event filtering;
8. duplicate EventId handling;
9. neutral-current mixed history;
10. direct relationship versus newer opinion noise.

Each case is replayed through 64 deterministic input permutations.

## Compared selectors

- current Mosaic grounded relationship selector;
- most-recent eligible baseline;
- clean-room recency/importance/relevance baseline;
- fixed case oracle.

The integration report contains metrics, a deterministic benchmark digest, and a CSV
payload in `Details.caseCsv`.

## Scope boundary

This is an offline research and regression gate. It adds no RimWorld dependency,
provider dependency, database, Graphiti runtime, Python runtime, or package entry.

## Pass conditions

- Mosaic exactly matches all ten fixed oracles;
- corpus count equals 500;
- every permutation produces the same Mosaic selection;
- privacy and perception boundaries produce zero leaks;
- two independent processes reproduce one benchmark digest;
- JSON and extracted CSV evidence are emitted.
