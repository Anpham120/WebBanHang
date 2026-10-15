<#
.SYNOPSIS
    Copies a "dotnet publish" folder into the IIS application folder.

.DESCRIPTION
    1. Drops app_offline.htm so the ASP.NET Core Module shuts the app down and releases its DLLs.
    2. Mirrors the publish folder into the deploy folder with robocopy (new version replaces the old one).
       App_Data (database), logs and appsettings.Production.json belong to the environment:
       they are never overwritten and never deleted.
    3. Removes app_offline.htm; IIS starts the new version on the next request.

    If the copy fails, app_offline.htm stays in place so visitors see the maintenance page
    instead of a half-updated site. Run the pipeline again to recover.

.EXAMPLE
    powershell -File scripts\deploy-iis.ps1 -PublishDir publish -DeployDir C:\wwwroot\webbanhang
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [string] $DeployDir = 'C:\wwwroot\webbanhang'
)

$ErrorActionPreference = 'Stop'

$publish = (Resolve-Path -LiteralPath $PublishDir).Path.TrimEnd('\')
$deploy  = [System.IO.Path]::GetFullPath($DeployDir).TrimEnd('\')

if (-not (Test-Path -LiteralPath (Join-Path $publish 'WebBanHang.dll'))) {
    throw "WebBanHang.dll not found in '$publish'. Did the Publish stage run?"
}

New-Item -ItemType Directory -Force -Path $deploy | Out-Null
$offlineFile = Join-Path $deploy 'app_offline.htm'
$dll = Join-Path $deploy 'WebBanHang.dll'

Write-Host "Deploying '$publish' -> '$deploy'"

# 1) Stop the running app.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app_offline.htm') -Destination $offlineFile -Force
$deadline = (Get-Date).AddSeconds(30)
while (Test-Path -LiteralPath $dll) {
    try {
        [System.IO.File]::Open($dll, 'Open', 'ReadWrite', 'None').Dispose()
        break
    }
    catch [System.IO.IOException] {
        if ((Get-Date) -gt $deadline) {
            throw "WebBanHang.dll is still locked after 30 seconds: IIS did not stop the application."
        }
        Start-Sleep -Milliseconds 500
    }
}

# 2) Copy the new version. robocopy exit codes 0-7 mean success, 8 and above mean failure.
robocopy $publish $deploy /MIR /XD App_Data logs /XF app_offline.htm appsettings.Production.json /R:5 /W:2 /NJH /NFL /NDL /NP
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE; app_offline.htm was left in place."
}

# 3) Start the app again.
Remove-Item -LiteralPath $offlineFile -Force
Write-Host 'Deploy finished. The new version starts on the next request.'
exit 0
