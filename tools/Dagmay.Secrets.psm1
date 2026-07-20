Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DagmaySecretRoot {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw "LOCALAPPDATA is unavailable; cannot locate the user-bound secret store."
    }

    return Join-Path $env:LOCALAPPDATA "Dagmay\secrets"
}

function Get-DagmayGeminiCredentialPath {
    return Join-Path (Get-DagmaySecretRoot) "gemini-api-key.dpapi"
}

function Save-DagmayGeminiCredential {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [Security.SecureString] $SecureKey,

        [string] $Path = (Get-DagmayGeminiCredentialPath)
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    # ConvertFrom-SecureString without a supplied key uses Windows DPAPI for the
    # current Windows user. The resulting file is not a plaintext API key.
    $encrypted = ConvertFrom-SecureString -SecureString $SecureKey
    Set-Content -LiteralPath $Path -Value $encrypted -Encoding UTF8 -NoNewline

    return [pscustomobject]@{
        path = $Path
        protection = "WINDOWS_DPAPI_CURRENT_USER"
    }
}

function Test-DagmayGeminiCredential {
    [CmdletBinding()]
    param(
        [string] $Path = (Get-DagmayGeminiCredentialPath)
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    try {
        $encrypted = Get-Content -LiteralPath $Path -Raw
        $secure = ConvertTo-SecureString -String $encrypted
        return $null -ne $secure -and $secure.Length -gt 0
    }
    catch {
        return $false
    }
}

function Invoke-WithDagmayGeminiCredential {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [scriptblock] $ScriptBlock,

        [string] $Path = (Get-DagmayGeminiCredentialPath)
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The encrypted Gemini credential is not configured. Run tools\Set-DagmayGeminiCredential.ps1 once."
    }

    $encrypted = Get-Content -LiteralPath $Path -Raw
    $secure = ConvertTo-SecureString -String $encrypted
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $priorDagmay = $env:DAGMAY_GEMINI_API_KEY
    $priorGemini = $env:GEMINI_API_KEY

    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        $env:DAGMAY_GEMINI_API_KEY = $plain
        $env:GEMINI_API_KEY = $null
        & $ScriptBlock
    }
    finally {
        $env:DAGMAY_GEMINI_API_KEY = $priorDagmay
        $env:GEMINI_API_KEY = $priorGemini
        if ($null -ne $bstr -and $bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
        Remove-Variable plain -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function @(
    "Get-DagmayGeminiCredentialPath",
    "Save-DagmayGeminiCredential",
    "Test-DagmayGeminiCredential",
    "Invoke-WithDagmayGeminiCredential"
)
