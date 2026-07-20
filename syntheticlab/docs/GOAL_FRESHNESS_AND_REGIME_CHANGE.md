# Goal Freshness and Regime Change

Every environment candidate can be wrapped in an observation tick, expiry tick, and world revision.
Mosaic rejects candidates from the future, expired observations, and mismatched world revisions before
goal scoring. A stale emergency therefore cannot override a valid current task.

The offline benchmark changes the colony's preferred work permanently at tick 50, injects an expired
starvation signal at tick 40, and a valid emergency at tick 70. Acceptance requires adaptation to the
new work regime within one tick, rejection of the stale emergency, selection of the fresh emergency,
and resumption of the new ordinary commitment afterward.

This contract depends on the environment adapter producing trustworthy ticks and revision IDs. It
does not by itself detect a game adapter that reports incorrect but internally fresh observations.
