#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time preparation of the Windows server that hosts Jenkins and IIS for WebBanHang.

.DESCRIPTION
    Run it once in an elevated Windows PowerShell on the server (Windows Server 2016 or later, or
    Windows 10/11). Every step is skipped when it is already done, so it is safe to run again.

      1. .NET 8 SDK, installed for all users (Jenkins needs it to build)
      2. IIS and the ASP.NET Core 8 Hosting Bundle
      3. Deploy folder C:\wwwroot\webbanhang with App_Data and logs
      4. Application pool WebBanHangPool (No Managed Code)
      5. Application /webbanhang under "Default Web Site"
      6. Folder permissions
           app pool identity : read, plus write on App_Data and logs only
           Jenkins account   : modify (needed to copy the new version)

    Git is only checked: install "Git for Windows" yourself if it is missing (Jenkins needs it).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup-server.ps1
#>
[CmdletBinding()]
param(
    [string] $DeployDir = 'C:\wwwroot\webbanhang',
    [string] $PoolName = 'WebBanHangPool',
    [string] $SiteName = 'Default Web Site',
    [string] $AppName = 'webbanhang',
    [string] $JenkinsServiceName = 'Jenkins'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # progress bars make Invoke-WebRequest very slow in Windows PowerShell
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rebootNeeded = $false

function Write-Step([string] $text) {
    Write-Host ""
    Write-Host "=== $text" -ForegroundColor Cyan
}

function Install-MicrosoftInstaller([string] $name, [string] $url) {
    $file = Join-Path $env:TEMP "$name.exe"
    Write-Host "Downloading $name ..."
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
    $signature = Get-AuthenticodeSignature $file
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Microsoft Corporation*') {
        throw "The $name installer is not signed by Microsoft (signature status: $($signature.Status))."
    }
    Write-Host "Installing $name ..."
    $process = Start-Process -FilePath $file -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        throw "The $name installer failed with exit code $($process.ExitCode)."
    }
    if ($process.ExitCode -eq 3010) { $script:rebootNeeded = $true }
}

function Invoke-Icacls {
    & icacls.exe @args | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls failed (exit code $LASTEXITCODE): icacls $args" }
}

# ---------------------------------------------------------------- 1. .NET 8 SDK and Git
Write-Step '1/6 .NET 8 SDK and Git'
$dotnetExe = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$sdk8 = @()
if (Test-Path $dotnetExe) { $sdk8 = @(& $dotnetExe --list-sdks | Where-Object { $_ -match '^8\.0\.' }) }
if ($sdk8.Count -gt 0) {
    Write-Host "Already installed: $($sdk8[0])"
} else {
    Install-MicrosoftInstaller 'dotnet-sdk-8' 'https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.exe'
}
if (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Host "Git found: $(git --version)"
} else {
    Write-Warning 'Git is not installed or not in PATH. Install Git for Windows (https://git-scm.com/download/win); Jenkins needs it.'
}

# ---------------------------------------------------------------- 2. IIS + Hosting Bundle
Write-Step '2/6 IIS and ASP.NET Core Hosting Bundle'
if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
    # Windows Server
    $result = Install-WindowsFeature -Name Web-Server, Web-Mgmt-Console, Web-Scripting-Tools
    if ($result.RestartNeeded -eq 'Yes') { $rebootNeeded = $true }
} else {
    # Windows 10/11
    $result = Enable-WindowsOptionalFeature -Online -All -NoRestart `
        -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-ManagementConsole, IIS-ManagementScriptingTools
    if ($result.RestartNeeded) { $rebootNeeded = $true }
}
Write-Host 'IIS is installed.'

# The Hosting Bundle must be installed after IIS so that it can register its IIS module.
$aspNetCoreModule = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
if (Test-Path $aspNetCoreModule) {
    Write-Host 'ASP.NET Core Hosting Bundle already installed.'
} else {
    Install-MicrosoftInstaller 'dotnet-hosting-8' 'https://aka.ms/dotnet/8.0/dotnet-hosting-win.exe'
    iisreset | Out-Null
}

# ---------------------------------------------------------------- 3. Folders
Write-Step "3/6 Deploy folder $DeployDir"
$appData = Join-Path $DeployDir 'App_Data'
$logs = Join-Path $DeployDir 'logs'
New-Item -ItemType Directory -Force -Path $DeployDir, $appData, $logs | Out-Null
Write-Host 'Folders ready (App_Data holds the database and survives every deployment).'

# ---------------------------------------------------------------- 4. Application pool
Write-Step "4/6 Application pool $PoolName"
Import-Module WebAdministration
if (-not (Test-Path "IIS:\Sites\$SiteName")) { throw "IIS site '$SiteName' does not exist." }
if (-not (Test-Path "IIS:\AppPools\$PoolName")) { New-WebAppPool -Name $PoolName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ''   # No Managed Code
Write-Host "$PoolName created with .NET CLR version = No Managed Code."

# ---------------------------------------------------------------- 5. Application
Write-Step "5/6 Application /$AppName under '$SiteName'"
$appPath = "IIS:\Sites\$SiteName\$AppName"
if (Test-Path $appPath) {
    Set-ItemProperty $appPath -Name physicalPath -Value $DeployDir
    Set-ItemProperty $appPath -Name applicationPool -Value $PoolName
} else {
    New-WebApplication -Name $AppName -Site $SiteName -PhysicalPath $DeployDir -ApplicationPool $PoolName | Out-Null
}
Write-Host "http://localhost/$AppName -> $DeployDir (pool $PoolName)."

# ---------------------------------------------------------------- 6. Permissions
Write-Step '6/6 Folder permissions'
$poolIdentity = "IIS AppPool\$PoolName"
# Replace inherited permissions (on C:\ they let every signed-in user modify new folders) by explicit ones.
Invoke-Icacls $DeployDir /inheritance:r /grant:r 'NT AUTHORITY\SYSTEM:(OI)(CI)F' 'BUILTIN\Administrators:(OI)(CI)F' "${poolIdentity}:(OI)(CI)RX"
Invoke-Icacls $appData /grant "${poolIdentity}:(OI)(CI)M"
Invoke-Icacls $logs /grant "${poolIdentity}:(OI)(CI)M"
Write-Host "$poolIdentity : read on the site, modify on App_Data and logs."

$jenkins = Get-CimInstance Win32_Service -Filter "Name='$JenkinsServiceName'" -ErrorAction SilentlyContinue
if ($jenkins) {
    $account = $jenkins.StartName
    if ($account -eq 'LocalSystem') { $account = 'NT AUTHORITY\SYSTEM' }
    if ($account.StartsWith('.\')) { $account = "$env:COMPUTERNAME\" + $account.Substring(2) }
    Invoke-Icacls $DeployDir /grant "${account}:(OI)(CI)M"
    Write-Host "$account (Jenkins service account) : modify on $DeployDir."
} else {
    Write-Warning "Service '$JenkinsServiceName' not found. Give the account that runs Jenkins 'Modify' on $DeployDir yourself."
}

Write-Step 'Done'
if ($rebootNeeded) { Write-Warning 'A restart is pending: reboot the server before running the pipeline.' }
Write-Host 'Next: run the Jenkins pipeline. The first deployment fills the folder, then open http://localhost/webbanhang'
