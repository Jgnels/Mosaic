# Dagmay Recovery / Current Handoff

## Verified baseline

Version 0.1I is the owner-verified runtime baseline on RimWorld 1.6.4871.

Evidence supplied by the owner shows:

- local Windows build, isolation tests, RimWorld adapter compile, and packaging succeeded;
- Lynx, Schmidt, Michael, and Doyle retained their established individual and lineage IDs;
- identity, experience, and reflection stores remained healthy;
- Google AI Studio reflections dispatched, validated, and committed;
- bounded event capture continued while request budgets were enforced; and
- salience admission reduced an unattempted reflection backlog from 23 to 3 while retaining canonical experiences, with later routine events stored locally instead of automatically becoming model work.

Do not treat earlier Codex local-folder access problems as evidence of broken Dagmay source.

## Current candidate

Version 0.1J adds the first bounded social-experience and relationship-memory foundation without adding pawn control.

It polls only relationships between already-enrolled colonists and records:

- material RimWorld opinion changes (absolute delta of at least 15); and
- direct relationship-label changes exposed by RimWorld.

A social memory links the other enrolled `IndividualId`, is `RelationshipSensitive` by default, and remains separate from the factual event. Direct relationship changes are immediately eligible for meaningful reflection; opinion changes require repeated evidence within a bounded window before model reflection is admitted.

This slice deliberately does **not** intercept every social-interaction callback, create omniscient witnessed events, infer private motives, or persist full canonical trust/affection/fear/resentment dimensions yet. Those remain later social-system work after this grounded observation layer is verified.

## Next owner action

From the extracted 0.1J source folder, with RimWorld closed:

```powershell
.\tools\dev-loop.ps1 -Install -Launch
```

If that succeeds, load the existing test colony and play normally. Social testing is best if enrolled colonists spend time together or experience a relationship change, but no forced scenario is required. After saving, closing, reloading once, and closing again:

```powershell
.\tools\collect-logs.ps1
```

Return `artifacts\Dagmay-diagnostics-latest.txt`. Useful evidence lines include `rimworld.social.opinion_changed`, `rimworld.relationship.direct_changed`, `retained experience locally`, `admitted reflection work`, and successful committed reflections.
