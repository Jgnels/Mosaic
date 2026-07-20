[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

throw @"
Real-provider execution is deliberately disabled.

The encrypted credential can be configured once, but it will not be used until:
1. opaque provider subject IDs are enforced;
2. exact secret-free payload archiving exists;
3. forbidden-token scans cover the final serialized payload;
4. typed evidence ownership/provenance is enforced;
5. retrieved-evidence citation rules and call-budget enforcement pass offline tests;
6. a bounded human approval record exists for the exact protocol.

This fail-closed stub prevents scheduled or unattended work from crossing Dagmay's current scientific stop line.
"@
