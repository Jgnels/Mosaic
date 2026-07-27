# Mosaic 0.2F - Read-Only Decision Trace Foundation

## Purpose

0.2F adds one immutable, deterministic explanation contract for read-only decisions.
It does not add a Why UI, provider call, persistence store, semantic mutation, dialogue
turn, action proposal, or pawn authority.

The trace records:

- deterministic trace ID;
- individual and operation;
- tick;
- mechanism and version;
- accepted and rejected evidence;
- rejection reasons;
- scores and accepted rank;
- tie-break explanation;
- bounded result;
- equal before/after canonical fingerprints.

Equal fingerprints are enforced by the constructor. A `ReadOnlyDecisionTrace` cannot
represent canonical mutation.

## Scope boundary

The new contract is placed in shipped `Dagmay.Core` so later versions can project it
into a read-only Why inspector. No RimWorld source consumes it in 0.2F.

This milestone therefore changes the Core assembly surface but does not change the
current RimWorld runtime path.

## Required verification

- all contracts pass twice with identical normalized output;
- the 128-cycle trace scenario reproduces one trace digest across processes;
- source firewall finds no provider, persistence-write, mutation, or pawn-control
  reference in the new trace source;
- Core, Providers, and RimWorld Release builds remain warning/error free;
- package and frozen-0.1 gates remain unchanged.
