# Installs SQL Server 2022 Express as instance .\SQLEXPRESS, which is what the connection
# string in src/SkopiStation.App/appsettings.json expects.
#
# REQUIRES AN ELEVATED POWERSHELL SESSION. SQL Server setup refuses to run otherwise, and the
# script stops immediately rather than failing halfway through:
#
#   Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass',
#       '-File','<path>\tools\install-sqlexpress.ps1'
#
# Why this script exists at all: `winget install Microsoft.SQLServer.2022.Express` does not work.
# Its manifest pins bootstrapper 16.0.1000.6, which Microsoft now rejects server-side with
# "this version of the installer is no longer supported". The evergreen download link below
# serves a current build instead.
#
# Three traps were hit while writing this, all worth keeping in mind before editing it:
#
#   1. SQL Server setup builds paths far deeper than its own root and fails past the Windows
#      260 character limit. Extracting under a long temporary path is enough to trigger it,
#      with a message about network shares that does not name the real cause. Hence the short
#      working directory at C:\sqlexpr.
#   2. Start-Process -ArgumentList joins the list with spaces WITHOUT quoting. A value that
#      contains a space, such as /SQLSVCACCOUNT=NT AUTHORITY\NETWORK SERVICE, arrives split
#      into several arguments and setup fails with "Value cannot be null. Parameter name:
#      userName". Quote any value containing a space yourself.
#   3. /SECURITYMODE only accepts the value SQL, for mixed mode. Passing Windows is invalid;
#      omitting the switch is what selects Windows authentication.
#
# /SQLSVCACCOUNT is deliberately absent: the default is the per-instance virtual account
# NT Service\MSSQL$SQLEXPRESS, which is the recommended one and sidesteps the fact that
# built-in account names are localised on a non-English Windows.

$ErrorActionPreference = 'Stop'

$staging   = Join-Path $env:TEMP 'skopi-sqlexpress'
$bootstrap = Join-Path $staging 'SQL2022-SSEI-Expr.exe'
$media     = Join-Path $staging 'media'
$work      = 'C:\sqlexpr'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must run from an elevated PowerShell session.'
}

New-Item -ItemType Directory -Force $staging | Out-Null

if (-not (Test-Path $bootstrap)) {
    Write-Host 'Downloading the SQL Server Express bootstrapper...'
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/?linkid=2215160' -OutFile $bootstrap -UseBasicParsing
}

$package = Get-ChildItem $media -Filter 'SQLEXPR*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $package) {
    Write-Host 'Downloading the installation media (about 1 GB)...'
    New-Item -ItemType Directory -Force $media | Out-Null
    $p = Start-Process -FilePath $bootstrap -Wait -PassThru -NoNewWindow `
        -ArgumentList '/ACTION=Download', "/MEDIAPATH=$media", '/MEDIATYPE=Core', '/QUIET'
    if ($p.ExitCode -ne 0) { throw "Media download failed with exit code $($p.ExitCode)." }
    $package = Get-ChildItem $media -Filter 'SQLEXPR*.exe' | Select-Object -First 1
}
if (-not $package) { throw "No SQLEXPR package found in $media." }
Write-Host "Using media: $($package.Name)"

if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null

$localPackage = Join-Path $work $package.Name
Copy-Item $package.FullName $localPackage

Write-Host "Extracting to $work ..."
$extract = Join-Path $work 'x'
$p = Start-Process -FilePath $localPackage -Wait -PassThru -NoNewWindow -ArgumentList '/Q', "/X:$extract"
if ($p.ExitCode -ne 0) { throw "Extraction failed with exit code $($p.ExitCode)." }

$account = [Security.Principal.WindowsIdentity]::GetCurrent().Name
Write-Host "Installing instance SQLEXPRESS, sysadmin: $account"
$p = Start-Process -FilePath (Join-Path $extract 'setup.exe') -Wait -PassThru -NoNewWindow -ArgumentList @(
    '/Q'
    '/ACTION=Install'
    '/FEATURES=SQLEngine'
    '/INSTANCENAME=SQLEXPRESS'
    "/SQLSYSADMINACCOUNTS=`"$account`""
    '/TCPENABLED=1'
    '/UPDATEENABLED=False'
    '/IACCEPTSQLSERVERLICENSETERMS'
)

# 3010 means success but a reboot is pending.
if ($p.ExitCode -notin 0, 3010) {
    Write-Host "Setup failed with exit code $($p.ExitCode). Summary log:"
    $summary = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\160\Setup Bootstrap\Log\summary.txt' -ErrorAction SilentlyContinue
    if ($summary) { Get-Content $summary.FullName -TotalCount 40 }
    throw "Setup failed with exit code $($p.ExitCode)."
}

Write-Host "Setup finished with exit code $($p.ExitCode)."
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
Get-Service 'MSSQL$SQLEXPRESS' | Format-Table Name, Status, StartType -AutoSize
