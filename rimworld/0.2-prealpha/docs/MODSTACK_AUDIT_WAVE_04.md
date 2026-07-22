# Mod-Stack Audit Wave 04

## Vanilla Expanded Framework

Canonical source reviewed: `Vanilla-Expanded/VanillaExpandedFramework`.

Important findings:
- The framework is explicitly a shared code library used by multiple Vanilla Expanded mods.
- It is designed to expose modular shared behaviours to dependent mods.
- The maintainers warn that RimWorld 1.6 introduced namespace changes that can break existing XML/C# integrations.

Mosaic consequences:
1. Do not scatter hard-coded Vanilla Expanded CLR type names throughout Mosaic.
2. Treat Vanilla Expanded as an optional capability boundary.
3. Detect capabilities/version at runtime in the RimWorld adapter.
4. Keep durable Mosaic evidence portable and independent of VE CLR objects.
5. A VE namespace/API change should disable the affected adapter rather than corrupting or preventing load of existing Mosaic history.

This directly motivated `ExternalCapability`.

## Adapter admission hardening

As the real-mod-stack audit grows, merely accepting every event emitted by an installed mod would create noise and false causality.

Examples that should NOT automatically become memories:
- internal cache refreshes;
- pathing optimizations;
- job-driver implementation steps;
- UI events;
- temporary mod bookkeeping.

Therefore external adapters now feed a deterministic admission policy:
- allowed source mod;
- allowed event kind;
- enrolled participant(s);
- portable payload.

Only admitted story-significant proposals proceed toward Mosaic evidence.

This directly motivated `IExternalEventAdmissionPolicy` and
`AllowListedExternalEventAdmissionPolicy`.

## Vanilla Social Interactions Expanded

A canonical current standalone source repository was not cleanly discoverable through the available GitHub index during this wave. The mod remains high priority, but no claim of a completed source audit is made here.

Next action:
- obtain/resolve the exact source lineage or audit the installed Workshop files on the user's gaming PC;
- then enumerate actual interaction/relationship hooks before writing a dedicated adapter.
