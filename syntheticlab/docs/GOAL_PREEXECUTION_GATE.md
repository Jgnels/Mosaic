# Goal Pre-Execution Gate

A selected goal is not authority to issue a RimWorld job. Immediately before execution, Mosaic
rechecks the request ID, world revision, current candidate feasibility, exact target, exact evidence
contract, evidence freshness, and target reachability. Any mismatch returns `REPLAN`.

Each request ID is single-use, including rejected requests. Accepted requests accept exactly one
bounded outcome. Success completes the goal; target loss, failed preconditions, interruption, or game
job rejection return to replanning. The gate records only an audit ledger and never issues a job.

This closes the common time-of-check/time-of-use gap between deciding and acting. A future C# adapter
must perform its own final game-native reservation and job validation after this contract passes.
