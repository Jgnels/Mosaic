param(
    [Parameter(Mandatory=$false)]
    [string]$RepoPath = ".",
    [Parameter(Mandatory=$false)]
    [string]$OutputDirectory = "."
)

$ErrorActionPreference = "Stop"
$RepoPath = (Resolve-Path $RepoPath).Path
if (-not (Test-Path $OutputDirectory)) { New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null }
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

& git -C $RepoPath rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) { throw "RepoPath is not a Git working tree." }

$status = & git -C $RepoPath status --porcelain
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect repository status." }
if ($status) { throw "Worktree is not clean. Commit intended review changes first so the snapshot has an exact identity." }

$sha = (& git -C $RepoPath rev-parse HEAD).Trim()
$short = (& git -C $RepoPath rev-parse --short=12 HEAD).Trim()
$branch = (& git -C $RepoPath branch --show-current).Trim()
$safeBranch = if ($branch) { $branch -replace '[^A-Za-z0-9._-]', '-' } else { 'detached' }
$zip = Join-Path $OutputDirectory "Mosaic-review-$safeBranch-$short.zip"

if (Test-Path $zip) { Remove-Item -Force $zip }
& git -C $RepoPath archive --format=zip --output=$zip HEAD
if ($LASTEXITCODE -ne 0) { throw "git archive failed." }

$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
$sidecar = "$zip.sha256.txt"
"$hash  $(Split-Path -Leaf $zip)" | Set-Content -Encoding ascii $sidecar

Write-Host "REVIEW_SNAPSHOT=$zip"
Write-Host "HEAD=$sha"
Write-Host "BRANCH=$branch"
Write-Host "SHA256=$hash"
Write-Host "Tracked files only; untracked local files and .git metadata are excluded by git archive."
