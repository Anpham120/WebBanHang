<#
.SYNOPSIS
    Smoke test of the deployed site. Exits with a non-zero code when something is wrong.

.DESCRIPTION
    - The home page answers HTTP 200 (retries while the app cold-starts).
    - Asset links are under the application path (PathBase) and the stylesheet really loads.
    - The version shown in the page footer is the one that was just built (when -ExpectedVersion is given).
    - The Products, Categories and Orders pages answer HTTP 200 (this also exercises the database).

.EXAMPLE
    powershell -File scripts\verify-deployment.ps1 -Url http://localhost/webbanhang -ExpectedVersion 1.0.12
#>
[CmdletBinding()]
param(
    [string] $Url = 'http://localhost/webbanhang',
    [string] $ExpectedVersion,
    [int] $TimeoutSec = 90
)

$ErrorActionPreference = 'Stop'

$base     = $Url.TrimEnd('/')
$basePath = ([System.Uri]$base).AbsolutePath.TrimEnd('/')     # "/webbanhang"
$origin   = $base.Substring(0, $base.Length - $basePath.Length) # "http://localhost"

function Get-Page([string] $uri) {
    Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 30
}

# 1) Home page. The first request starts the app, so retry until the timeout.
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ($true) {
    try {
        $homePage = Get-Page "$base/"
        break
    }
    catch {
        if ((Get-Date) -gt $deadline) {
            throw "No HTTP 200 from $base/ within $TimeoutSec seconds: $($_.Exception.Message)"
        }
        Write-Host "Waiting for $base/ ... $($_.Exception.Message)"
        Start-Sleep -Seconds 3
    }
}
Write-Host "OK   GET $base/ -> $($homePage.StatusCode)"

# 2) PathBase: links must carry the application path, and the file must be served from there.
$css = [regex]::Match($homePage.Content, 'href="([^"]*site\.css[^"]*)"').Groups[1].Value
if (-not $css.StartsWith("$basePath/")) {
    throw "Stylesheet link '$css' does not start with '$basePath/' (PathBase is not handled)."
}
$null = Get-Page "$origin$css"
Write-Host "OK   GET $origin$css -> 200"

# 3) The version in the footer is the version that was just built.
if ($ExpectedVersion) {
    $live = [regex]::Match($homePage.Content, 'id="app-version">([^<]*)<').Groups[1].Value
    if ($live -ne $ExpectedVersion) {
        throw "The site shows version '$live' but '$ExpectedVersion' was just deployed."
    }
    Write-Host "OK   version $live is live"
}

# 4) Feature pages.
foreach ($path in '/Products', '/Categories', '/Orders') {
    $null = Get-Page "$base$path"
    Write-Host "OK   GET $base$path -> 200"
}

Write-Host "Verification passed for $base"
exit 0
