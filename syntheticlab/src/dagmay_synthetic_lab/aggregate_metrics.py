from __future__ import annotations

from collections import defaultdict
from typing import Iterable, Mapping
import statistics


def aggregate_branch_metrics(
    rows: Iterable[Mapping[str, object]],
    group_field: str,
    metric_fields: tuple[str, ...],
    minimum_group_size: int = 4,
) -> dict:
    """Export group statistics without branch IDs.

    This is intentionally simple. It is not a formal differential-privacy system.
    Groups smaller than the threshold are suppressed.
    """
    grouped = defaultdict(list)
    for row in rows:
        grouped[str(row[group_field])].append(row)

    output = {}
    for group, items in grouped.items():
        if len(items) < minimum_group_size:
            output[group] = {
                "suppressed": True,
                "count": len(items),
                "reason": "group below minimum export size",
            }
            continue

        metrics = {}
        for field in metric_fields:
            vals = [float(x[field]) for x in items]
            metrics[field] = {
                "mean": statistics.mean(vals),
                "min": min(vals),
                "max": max(vals),
            }

        output[group] = {
            "suppressed": False,
            "count": len(items),
            "metrics": metrics,
        }

    return output
