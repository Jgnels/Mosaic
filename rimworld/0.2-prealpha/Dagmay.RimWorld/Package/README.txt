Dagmay 0.1K — Social Path Certification & 0.1 Release Closure

0.1K freezes the RimWorld 0.1 cognitive feature set at the tested 0.1J.3 baseline.

New in 0.1K:
- automatic diagnostic-only social-path certification;
- stable counterpart-link verification;
- relationship-sensitive memory verification;
- reflection-eligibility verification;
- save/reload persistence certification;
- log collection that embeds the latest certification sidecar.

The certification report is stored outside canonical individual state under the RimWorld
config Dagmay diagnostics directory.

0.1K remains Observer-only. It adds no pawn control, no autonomous action, no canonical
SelfModel promotion, and no post-0.1J.3 SyntheticLab cognitive mechanism.

RimWorld test gate:
1. generate a real bounded social event between enrolled colonists;
2. save;
3. reload;
4. run tools\check-social-certification.ps1;
5. collect tools\collect-logs.ps1 evidence.
