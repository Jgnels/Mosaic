param([Parameter(Mandatory=$true)][string]$Repository)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '..\..\..\Mosaic-GateKit-v1\GateKit.psm1') -Force
Invoke-MosaicDotnet -Project 'Dagmay.Core/Dagmay.Core.csproj' -WorkingDirectory (Join-Path $Repository 'rimworld/0.2-prealpha') | Out-Null
Invoke-MosaicDotnet -Project 'Dagmay.Tests/Dagmay.Tests.csproj' -WorkingDirectory (Join-Path $Repository 'rimworld/0.2-prealpha') | Out-Null
