[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$RimWorldPath = "",

    [switch]$SkipRimWorld,

    [switch]$NoTranscript
)

$ErrorActionPreference = "Stop"
$DagmayVersion = "0.2-prealpha"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$BuildLogs = Join-Path $Artifacts "build-logs"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$TranscriptPath = Join-Path $BuildLogs "Dagmay-build-$RunStamp.txt"
$LatestTranscriptPath = Join-Path $Artifacts "Dagmay-build-latest.txt"
$ResultPath = Join-Path $BuildLogs "Dagmay-build-$RunStamp.json"
$LatestResultPath = Join-Path $Artifacts "Dagmay-build-result-latest.json"
$StartedUtc = [DateTimeOffset]::UtcNow
$CompletedStages = New-Object System.Collections.Generic.List[string]
$TranscriptStarted = $false
$BuildStatus = "failed"
$FailureMessage = ""
$PackagePath = ""
$PackageSha256 = ""
$IntegrationReportPath = Join-Path $Artifacts "Dagmay-integration-latest.json"

New-Item -ItemType Directory -Path $BuildLogs -Force | Out-Null
if (-not $NoTranscript -and (Test-Path -LiteralPath $LatestTranscriptPath)) {
    Remove-Item -LiteralPath $LatestTranscriptPath -Force
}

if (-not $NoTranscript) {
    try {
        Start-Transcript -Path $TranscriptPath -Force | Out-Null
        $TranscriptStarted = $true
    }
    catch {
        Write-Warning "PowerShell could not start a transcript. The structured build result will still be written. $($_.Exception.Message)"
    }
}

Push-Location $Root

try {
    Write-Host "Dagmay $DagmayVersion build started."
    Write-Host "Source root: $Root"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK 8 or newer was not found. Follow docs/11_Development_Guide.md."
    }

    $PythonCommand = $null
    $PythonPrefix = @()
    if (Get-Command python -ErrorAction SilentlyContinue) {
        & python --version *> $null
        if ($LASTEXITCODE -eq 0) { $PythonCommand = "python" }
    }

    if ($null -eq $PythonCommand -and (Get-Command py -ErrorAction SilentlyContinue)) {
        & py -3 --version *> $null
        if ($LASTEXITCODE -eq 0) {
            $PythonCommand = "py"
            $PythonPrefix = @("-3")
        }
    }

    if ($null -ne $PythonCommand) {
        & $PythonCommand @PythonPrefix tools/static_verify.py
        if ($LASTEXITCODE -ne 0) { throw "Static verification failed." }
        $CompletedStages.Add("static-verification") | Out-Null
    }
    else {
        Write-Warning "A working Python 3 interpreter was not found; static_verify.py was skipped."
    }

    dotnet build Dagmay.Core/Dagmay.Core.csproj --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Dagmay.Core build failed." }
    $CompletedStages.Add("core-build") | Out-Null

    dotnet build Dagmay.Providers/Dagmay.Providers.csproj --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Dagmay.Providers build failed." }
    $CompletedStages.Add("providers-build") | Out-Null

    dotnet run --project Dagmay.Tests/Dagmay.Tests.csproj --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Dagmay contract tests failed." }
    $CompletedStages.Add("isolation-tests") | Out-Null

    dotnet run --project Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj --configuration $Configuration -- --output $IntegrationReportPath
    if ($LASTEXITCODE -ne 0) { throw "Dagmay integration harness failed." }
    $CompletedStages.Add("integration-harness") | Out-Null

    if ($SkipRimWorld) {
        Write-Host "RimWorld build skipped by request."
    }
    else {
        if ([string]::IsNullOrWhiteSpace($RimWorldPath)) {
            $RimWorldPath = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\RimWorld"
        }

        $ManagedPath = Join-Path $RimWorldPath "RimWorldWin64_Data\Managed"
        $RequiredRimWorldAssemblies = @(
            "Assembly-CSharp.dll",
            "UnityEngine.IMGUIModule.dll",
            "UnityEngine.TextRenderingModule.dll"
        )
        $MissingRimWorldAssemblies = @(
            $RequiredRimWorldAssemblies | Where-Object {
                -not (Test-Path (Join-Path $ManagedPath $_))
            }
        )
        if ($MissingRimWorldAssemblies.Count -gt 0) {
            $MissingList = $MissingRimWorldAssemblies -join ", "
            throw "Required RimWorld assemblies were not found at '$ManagedPath': $MissingList. Verify the installation or re-run with -RimWorldPath 'C:\your\RimWorld\folder'."
        }

        dotnet build Dagmay.RimWorld/Dagmay.RimWorld.csproj `
            --configuration $Configuration `
            "-p:RimWorldInstallDir=$RimWorldPath"
        if ($LASTEXITCODE -ne 0) { throw "Dagmay.RimWorld build failed." }
        $CompletedStages.Add("rimworld-build") | Out-Null

        $PackagePath = Join-Path $Artifacts "Dagmay-RimWorld-$DagmayVersion.zip"
        $TemporaryPackagePath = Join-Path $Artifacts ".Dagmay-RimWorld-$DagmayVersion-$RunStamp.tmp.zip"
        if (Test-Path $TemporaryPackagePath) { Remove-Item -LiteralPath $TemporaryPackagePath -Force }
        Compress-Archive -Path "Dagmay.RimWorld\Package\*" -DestinationPath $TemporaryPackagePath -CompressionLevel Optimal
        if (Test-Path $PackagePath) { Remove-Item -LiteralPath $PackagePath -Force }
        Move-Item -LiteralPath $TemporaryPackagePath -Destination $PackagePath

        $PackageSha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $HashPath = Join-Path $Artifacts "Dagmay-RimWorld-$DagmayVersion.sha256.txt"
        ("{0}  {1}" -f $PackageSha256, (Split-Path -Leaf $PackagePath)) |
            Set-Content -LiteralPath $HashPath -Encoding UTF8
        $CompletedStages.Add("rimworld-package") | Out-Null

        Write-Host "RimWorld package: $PackagePath"
        Write-Host "Package SHA-256: $PackageSha256"
    }

    $BuildStatus = "succeeded"
    Write-Host "Build and tests completed."
}
catch {
    $FailureMessage = $_.Exception.Message
    throw
}
finally {
    $FinishedUtc = [DateTimeOffset]::UtcNow
    $Result = [ordered]@{
        schema = "dagmay.build-result.v1"
        dagmayVersion = $DagmayVersion
        status = $BuildStatus
        startedUtc = $StartedUtc.ToString("o")
        finishedUtc = $FinishedUtc.ToString("o")
        configuration = $Configuration
        skipRimWorld = [bool]$SkipRimWorld
        rimWorldPath = $RimWorldPath
        completedStages = @($CompletedStages)
        packagePath = $PackagePath
        packageSha256 = $PackageSha256
        integrationReportPath = $IntegrationReportPath
        failure = $FailureMessage
    }

    try {
        $Result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
        Copy-Item -LiteralPath $ResultPath -Destination $LatestResultPath -Force
    }
    catch {
        Write-Warning "The structured build result could not be saved. $($_.Exception.Message)"
    }

    Pop-Location

    if ($TranscriptStarted) {
        try {
            Stop-Transcript | Out-Null
            Copy-Item -LiteralPath $TranscriptPath -Destination $LatestTranscriptPath -Force
        }
        catch {
            Write-Warning "The build transcript could not be finalized. $($_.Exception.Message)"
        }
    }

    Write-Host "Build result: $LatestResultPath"
    if ($TranscriptStarted) { Write-Host "Build output: $LatestTranscriptPath" }
}
