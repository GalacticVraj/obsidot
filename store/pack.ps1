# Builds release\Obsidot.msix for the Microsoft Store.
#   .\store\pack.ps1                                   test package (identity GalacticVraj.Obsidot / CN=GalacticVraj)
#   .\store\pack.ps1 -Name <Package/Identity/Name> -Publisher <Package/Identity/Publisher> -PublisherName <display name>
#                                                      Store package: copy the three values from Partner Center >
#                                                      your app > Product identity
#   .\store\pack.ps1 -Register                         also install it loose for testing (needs Developer Mode)
# The Store signs the package itself; nothing here needs a certificate.
param(
    [string]$Name = "GalacticVraj.Obsidot",
    [string]$Publisher = "CN=GalacticVraj",
    [string]$PublisherName = "GalacticVraj",
    [string]$Version = "1.0.0.0",
    [switch]$Register
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$tools = Join-Path $PSScriptRoot ".tools"
$layout = Join-Path $root "release\msix"
$out = Join-Path $root "release\Obsidot.msix"

# makeappx / makepri come from Microsoft's own Windows SDK build-tools NuGet package (no SDK install, no admin)
$makeappx = Get-ChildItem $tools -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | ? { $_.FullName -match '\\x64\\' } | Select -First 1
if (-not $makeappx) {
    New-Item -ItemType Directory -Force $tools | Out-Null
    $v = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/index.json").versions | ? { $_ -notmatch '-' } | Select -Last 1
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$v/microsoft.windows.sdk.buildtools.$v.nupkg" -OutFile "$tools\bt.zip"
    Expand-Archive "$tools\bt.zip" "$tools\bt" -Force; Remove-Item "$tools\bt.zip"
    $makeappx = Get-ChildItem $tools -Recurse -Filter makeappx.exe | ? { $_.FullName -match '\\x64\\' } | Select -First 1
}
$makepri = Join-Path $makeappx.DirectoryName "makepri.exe"

# 1. build
& cmd /c "`"$root\build.cmd`"" | Out-Host
if ($LASTEXITCODE) { throw "build failed" }

# 2. layout: the exe, the manifest, the logos
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force "$layout\Assets" | Out-Null
Copy-Item "$root\Obsidot.exe" $layout
(Get-Content "$PSScriptRoot\AppxManifest.xml" -Raw).Replace('$NAME$', $Name).Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($Publisher)).Replace('$PUBLISHERNAME$', [Security.SecurityElement]::Escape($PublisherName)).Replace('$VERSION$', $Version) |
    Set-Content "$layout\AppxManifest.xml" -Encoding UTF8
& "$PSScriptRoot\assets.ps1" "$layout\Assets"

# 3. resources.pri (so Windows picks the right logo size), then the package
Push-Location $layout
& $makepri createconfig /cf "..\priconfig.xml" /dq en-US /pv 10.0.0 /o | Out-Null
& $makepri new /pr . /cf "..\priconfig.xml" /mn AppxManifest.xml /of resources.pri /o | Out-Null
Pop-Location
if ($LASTEXITCODE) { throw "makepri failed" }
& $makeappx.FullName pack /d $layout /p $out /o | Select-Object -Last 1
if ($LASTEXITCODE) { throw "makeappx failed" }
"{0}  ({1:N0} KB, identity {2})" -f $out, ((Get-Item $out).Length / 1KB), $Name

if ($Register) {
    Get-AppxPackage -Name $Name | Remove-AppxPackage
    Add-AppxPackage -Register "$layout\AppxManifest.xml"
    "registered: launch Obsidot from Start"
}
