# Mosaic RimWorld 0.2 Pre-Alpha Verification Record

**Completed UTC:** 2026-07-22T05:53:30.8123588+00:00  
**Branch:** mosaic-v0.2-prealpha-import  
**Source commit before this verification update:** 53c2083b2c3a53bad5a48fa41a11a616792c3ed7  
**RimWorld installation:** local Steam RimWorld 1.6 assemblies  
**Configuration:** Release

## Passed gates

- static verification: 93 C# files;
- Core compilation;
- Providers compilation;
- contract tests: 60 executed, 0 failed;
- integration harness: 6 scenarios, 0 failed;
- RimWorld adapter compilation: 0 warnings, 0 errors;
- non-installing package creation;
- package SHA-256 verification.

## Package

Package file: Dagmay-RimWorld-0.2-prealpha.zip  
SHA-256: 15ee280014a80c425cbfa75d1dda87f9ed035cd094988baec904944cebc8e372

## Integration coverage

- long-history continuity and persistence;
- provider-outage durable queue recovery;
- checkpoint rollback/forward recovery;
- social perspective and privacy;
- persistence torture;
- failure isolation.

## Not yet verified

- loading the 0.2 package in RimWorld;
- in-game save/load and archive continuity;
- compatibility with the owner's complete mod stack;
- long-session runtime performance;
- RimTalk bridge behavior.

This record does not authorize pawn control or direct LLM job issuance.
