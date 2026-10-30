# Builds the launcher in Release mode and packs it into dist\KINGDOM-HEARTS-Launcher-v<version>.zip.
# Usage:  powershell -ExecutionPolicy Bypass -File .\Package-Release.ps1
# Needs Visual Studio or the Build Tools with the .NET Framework 4.7.2 targeting pack.
param(
    # Extra MSBuild arguments, e.g. '/p:TargetFrameworkRootPath=C:\some\path\'
    [string[]]$ExtraBuildArgs = @()
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'KINGDOM HEARTS Launcher\KINGDOM HEARTS Launcher.csproj'
$exeName = 'KINGDOM HEARTS Launcher.exe'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw 'MSBuild not found. Install Visual Studio or the Build Tools (with the .NET Framework 4.7.2 targeting pack).'
}

# Builds without the .NET Framework 4.7.2 targeting pack by assembling a stand-in reference folder from the
# runtime that ships with Windows. Installing the pack is cleaner (see README), but this needs no download or admin.
# Kept at a short path under %TEMP%: WPF's build tasks fail to load assemblies from very long paths.
function New-StandInFramework {
    $fw64 = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
    $fw32 = Join-Path $env:windir 'Microsoft.NET\Framework\v4.0.30319'
    $gac = Join-Path $env:windir 'Microsoft.NET\assembly'
    $stand = Join-Path $env:TEMP 'khfw'
    $dest = Join-Path $stand '.NETFramework\v4.7.2'

    if (Test-Path $stand) { Remove-Item $stand -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $dest 'RedistList') -Force | Out-Null

    $managed = { try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); $true } catch { $false } }
    foreach ($dir in $fw64, (Join-Path $fw64 'WPF')) {
        Get-ChildItem $dir -Filter *.dll | Where-Object $managed | Copy-Item -Destination $dest -Force
    }
    # 32-bit natives and mixed-mode assemblies: the build runs as a 32-bit process.
    foreach ($dir in $fw32, (Join-Path $fw32 'WPF')) {
        Get-ChildItem $dir -Filter *.dll | Copy-Item -Destination $dest -Force -ErrorAction SilentlyContinue
    }
    $names = 'PresentationCore', 'PresentationFramework', 'WindowsBase', 'System.Xaml', 'System.Web.Extensions',
             'System.Windows.Forms', 'System.Drawing', 'System', 'System.Core', 'UIAutomationProvider', 'UIAutomationTypes'
    foreach ($name in $names) {
        foreach ($g in 'GAC_MSIL', 'GAC_32', 'GAC_64') {
            $versions = Join-Path (Join-Path $gac $g) $name
            if (Test-Path $versions) {
                $dll = Join-Path (Get-ChildItem $versions -Directory | Select-Object -Last 1).FullName "$name.dll"
                Copy-Item $dll $dest -Force
                break
            }
        }
    }
    '<FileList Redist="Microsoft-Windows-CLRCoreComp.4.7.2" Name=".NET Framework 4.7.2" RuntimeVersion="4.0" ToolsVersion="4.0" />' |
        Set-Content (Join-Path $dest 'RedistList\FrameworkList.xml')
    return $stand
}

$msbuild = Find-MSBuild

$targetingPack = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll'
if (-not (Test-Path $targetingPack)) {
    Write-Host '.NET Framework 4.7.2 targeting pack not installed; building against the installed runtime instead.'
    $ExtraBuildArgs += "/p:TargetFrameworkRootPath=$(New-StandInFramework)\"
}

Write-Host "Building with $msbuild"
& $msbuild $project /t:Rebuild /p:Configuration=Release /v:minimal /nologo @ExtraBuildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$exe = Join-Path (Split-Path $project) "bin\Release\$exeName"
if (-not (Test-Path $exe)) { throw "Build output not found: $exe" }

$version = (Get-Item $exe).VersionInfo.FileVersion
$dist = Join-Path $root 'dist'
$staging = Join-Path $dist 'KINGDOM HEARTS Launcher'
$zip = Join-Path $dist "KINGDOM-HEARTS-Launcher-v$version.zip"

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item $exe $staging
Copy-Item (Join-Path $root 'Distribution\INSTALL.txt') $staging
Copy-Item (Join-Path $root 'LICENSE.txt') $staging

Compress-Archive -Path $staging -DestinationPath $zip
Write-Host "Created $zip"
Get-ChildItem $staging | Select-Object Name, Length | Format-Table -AutoSize
