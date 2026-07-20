from __future__ import annotations

import math
import statistics


def mean_sd_ci95(values):
    vals = [float(v) for v in values]
    if not vals:
        return {
            "n": 0,
            "mean": None,
            "sd": None,
            "ci95_low": None,
            "ci95_high": None,
        }

    mean = statistics.mean(vals)

    if len(vals) == 1:
        return {
            "n": 1,
            "mean": mean,
            "sd": 0.0,
            "ci95_low": mean,
            "ci95_high": mean,
        }

    sd = statistics.stdev(vals)
    se = sd / math.sqrt(len(vals))
    margin = 1.96 * se

    return {
        "n": len(vals),
        "mean": mean,
        "sd": sd,
        "ci95_low": mean - margin,
        "ci95_high": mean + margin,
    }


def paired_difference_summary(
    a_values,
    b_values,
    label="A_MINUS_B",
):
    a = list(a_values)
    b = list(b_values)
    if len(a) != len(b):
        raise ValueError("paired samples must have equal length")

    diffs = [
        float(x) - float(y)
        for x, y in zip(a, b)
    ]

    result = mean_sd_ci95(diffs)
    result["label"] = label
    result["positive_pair_fraction"] = (
        sum(1 for d in diffs if d > 0) / len(diffs)
        if diffs else None
    )
    result["zero_pair_fraction"] = (
        sum(1 for d in diffs if d == 0) / len(diffs)
        if diffs else None
    )
    return result
