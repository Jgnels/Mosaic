from __future__ import annotations

import math
import statistics
from typing import Iterable, Tuple


def mean(values: Iterable[float]) -> float:
    values = list(values)
    if not values:
        raise ValueError("mean requires at least one value")
    return statistics.mean(values)


def sample_sd(values: Iterable[float]) -> float:
    values = list(values)
    if len(values) < 2:
        return 0.0
    return statistics.stdev(values)


def cohen_d_independent(a: Iterable[float], b: Iterable[float]) -> float:
    a = list(a)
    b = list(b)
    if len(a) < 2 or len(b) < 2:
        return 0.0
    va = statistics.variance(a)
    vb = statistics.variance(b)
    pooled = math.sqrt(((len(a)-1)*va + (len(b)-1)*vb) / (len(a)+len(b)-2))
    if pooled == 0:
        return 0.0
    return (statistics.mean(a) - statistics.mean(b)) / pooled


def wilson_interval(successes: int, n: int, z: float = 1.96) -> Tuple[float, float]:
    if n <= 0:
        raise ValueError("n must be > 0")
    p = successes / n
    denom = 1 + z*z/n
    center = (p + z*z/(2*n)) / denom
    margin = z * math.sqrt((p*(1-p)/n) + (z*z/(4*n*n))) / denom
    return max(0.0, center-margin), min(1.0, center+margin)
