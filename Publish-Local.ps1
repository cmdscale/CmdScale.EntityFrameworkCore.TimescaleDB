#Requires -Version 5.1
<#
.SYNOPSIS
    Increments the version of a .NET project, packs it, and copies the NuGet package to a local directory.
.PARAMETER ProjectName
    The name of the project directory to publish.
.PARAMETER VersionBump
    Specifies which part of the version to increment. Defaults to 'patch'.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "The name of the project to publish (e.g., 'CmdScale.EntityFrameworkCore.TimescaleDB').")]
    [string]$ProjectName,

    [ValidateSet('major', 'minor', 'patch')]
    [string]$VersionBump = 'patch'
)

$ErrorActionPreference = 'Stop'

# --- SCRIPT CONFIGURATION ---
$LocalNuGetRepo = "C:\_DEV\NuGet Packages"
$SolutionRoot = $PSScriptRoot

try {
    # Find the specified project file
    $ProjectDirectory = Join-Path $SolutionRoot $ProjectName
    if (-not (Test-Path $ProjectDirectory)) {
        throw "Project directory not found at '$ProjectDirectory'."
    }
    
    $ProjectFile = Get-ChildItem -Path $ProjectDirectory -Filter *.csproj | Select-Object -First 1
    if (-not $ProjectFile) {
        throw "No .csproj file found in '$ProjectDirectory'."
    }
    Write-Host "📁 Found project: $($ProjectFile.FullName)" -ForegroundColor Green

    # Read the project file to get and bump the version
    [xml]$csprojContent = Get-Content $ProjectFile.FullName
    $versionNode = $csprojContent.SelectSingleNode("//PropertyGroup/Version")
    if (-not $versionNode) {
        throw "Could not determine <Version> from $($ProjectFile.Name)."
    }
    $currentVersion = $versionNode.'#text'

    # --- Version Bumping Logic ---
    Write-Host "🔼 Bumping '$VersionBump' version from '$currentVersion'..." -ForegroundColor Cyan
    $versionParts = $currentVersion.Split('.')
    $major = [int]$versionParts[0]; $minor = [int]$versionParts[1]; $patch = [int]$versionParts[2]
    switch ($VersionBump) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }
    $newVersion = "$major.$minor.$patch"
    $versionNode.'#text' = $newVersion
    $csprojContent.Save($ProjectFile.FullName)
    Write-Host "✅ New version is '$newVersion'" -ForegroundColor Green
    
    # Build and Pack the project
    $outputDir = Join-Path $ProjectFile.DirectoryName "bin/Release"
    Write-Host "📦 Building and Packing project..." -ForegroundColor Cyan
    
    dotnet build $ProjectFile.FullName -c Release | Out-Host
    dotnet pack $ProjectFile.FullName -c Release -o $outputDir | Out-Host

    # Find the created NuGet package instead of guessing its name
    Write-Host "🔎 Finding created package in '$outputDir'..."
    $nupkgFile = Get-ChildItem -Path $outputDir -Filter *.nupkg | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if (-not $nupkgFile) {
        throw "Packed file not found in '$outputDir'. The pack command may have failed."
    }
    Write-Host "  - Found package: $($nupkgFile.Name)"

    # Copy the NuGet package
    if (-not (Test-Path $LocalNuGetRepo)) {
        New-Item -ItemType Directory -Path $LocalNuGetRepo | Out-Null
    }

    Write-Host "🚚 Copying '$($nupkgFile.Name)' to '$LocalNuGetRepo'..." -ForegroundColor Cyan
    Copy-Item -Path $nupkgFile.FullName -Destination $LocalNuGetRepo -Force

    $packageName = $nupkgFile.BaseName.Replace(".$newVersion", "")
    Write-Host "🎉 Successfully published version $newVersion of $packageName to local repository." -ForegroundColor Magenta
    
    Write-Output $newVersion
}
catch {
    Write-Error "❌ An error occurred in Publish-Local.ps1: $($_.Exception.Message)"
    exit 1
}