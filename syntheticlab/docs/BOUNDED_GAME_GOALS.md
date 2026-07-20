# Bounded Game-Grounded Goals

Mosaic chooses only among feasible goal candidates supplied by the environment adapter. Every
candidate has a fixed goal kind, observed evidence IDs, feasibility, urgency, utility, bounded
personality fit, bounded risk, and an optional game target. Free-text goals are not accepted.

Critical needs form a priority pool before scoring, so personality and commitment cannot override
starvation, urgent treatment, or comparable game emergencies. Outside emergencies, a small
commitment bonus reduces task thrashing while bounded personality fit provides character flavor.

A provider may later propose only an existing candidate ID. Mosaic independently revalidates
feasibility and evidence before accepting it. Final goal dialogue is deterministic and cannot add
facts, motives, dependency, or existential framing.

This is an offline SyntheticLab reference. It does not issue RimWorld jobs and does not authorize
canonical action-policy mutation.
