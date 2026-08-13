# build.ps1 - Build BeamgunApp (WPF) without needing the full Visual Studio targeting packs.
#
# Usage:
#   .\build.ps1                              # Debug / AnyCPU
#   .\build.ps1 -Configuration Release       # Release build
#   .\build.ps1 -Configuration Debug -Platform AnyCPU
#
# Why FrameworkPathOverride?
#   This machine may only have the .NET Framework 4.8.1 targeting pack installed,
#   while the project targets v4.6.1. We therefore point the build at reference
#   assemblies shipped via the NuGet package
#   "Microsoft.NETFramework.ReferenceAssemblies.net461" (restored into ./packages).

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "AnyCPU"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found) { return $found }
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    throw "MSBuild.exe not found. Install Visual Studio Build Tools or the .NET Framework SDK."
}

function Find-FrameworkPath {
    # Preferred: reference assemblies from the restored NuGet package.
    $pkgRoot = Join-Path $root "packages"
    if (Test-Path $pkgRoot) {
        $pkg = Get-ChildItem $pkgRoot -Directory -Filter "Microsoft.NETFramework.ReferenceAssemblies.net461.*" |
               Sort-Object Name -Descending | Select-Object -First 1
        if ($pkg) {
            $path = Join-Path $pkg.FullName "build\.NETFramework\v4.6.1"
            if (Test-Path (Join-Path $path "mscorlib.dll")) { return $path }
        }
    }

    # Fallback: use an installed targeting pack (4.6.1 preferred, then newer).
    $raRoot = Join-Path ${env:ProgramFiles(x86)} "Reference Assemblies\Microsoft\Framework\.NETFramework"
    if (Test-Path $raRoot) {
        foreach ($ver in @("v4.6.1", "v4.8.1", "v4.7.2", "v4.6.2", "v4.6")) {
            $path = Join-Path $raRoot $ver
            if (Test-Path (Join-Path $path "mscorlib.dll")) { return $path }
        }
    }

    throw "No .NET Framework reference assemblies found. Restore the NuGet package or install a targeting pack."
}

$msbuild       = Find-MSBuild
$frameworkPath = Find-FrameworkPath
$project       = Join-Path $root "BeamgunApp\BeamgunApp.csproj"

Write-Host "MSBuild       : $msbuild"
Write-Host "FrameworkPath : $frameworkPath"
Write-Host "Project       : $project"
Write-Host "Config        : $Configuration / $Platform"
Write-Host ""

& $msbuild $project "/p:Configuration=$Configuration" "/p:Platform=$Platform" "/p:FrameworkPathOverride=$frameworkPath" /v:minimal /nologo

exit $LASTEXITCODE
