#Requires -Version 5.1
<#
.SYNOPSIS
    Packs a .NET project with its current version and copies the NuGet package to a local repository,
    overwriting any existing package and clearing the NuGet cache.
.PARAMETER ProjectName
    The name of the project directory to publish.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "The name of the project to publish (e.g., 'CmdScale.EntityFrameworkCore.TimescaleDB').")]
    [string]$ProjectName
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

    # Read the project file to get the current version
    [xml]$csprojContent = Get-Content $ProjectFile.FullName
    $versionNode = $csprojContent.SelectSingleNode("//PropertyGroup/Version")
    if (-not $versionNode) {
        throw "Could not determine <Version> from $($ProjectFile.Name)."
    }
    $currentVersion = $versionNode.'#text'
    Write-Host "✅ Project version is '$currentVersion'" -ForegroundColor Green
    
    # Build and Pack the project
    $outputDir = Join-Path $ProjectFile.DirectoryName "bin/Release"
    Write-Host "📦 Building and Packing project..." -ForegroundColor Cyan
    
    dotnet build $ProjectFile.FullName -c Release | Out-Host
    dotnet pack $ProjectFile.FullName -c Release -o $outputDir | Out-Host

    # Find the created NuGet package
    Write-Host "🔎 Finding created package in '$outputDir'..."
    $nupkgFile = Get-ChildItem -Path $outputDir -Filter *.nupkg | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if (-not $nupkgFile) {
        throw "Packed file not found in '$outputDir'. The pack command may have failed."
    }
    Write-Host "   - Found package: $($nupkgFile.Name)"

    # Copy the NuGet package, overwriting any existing one
    if (-not (Test-Path $LocalNuGetRepo)) {
        New-Item -ItemType Directory -Path $LocalNuGetRepo | Out-Null
    }

    Write-Host "🚚 Copying '$($nupkgFile.Name)' to '$LocalNuGetRepo'..." -ForegroundColor Cyan
    Copy-Item -Path $nupkgFile.FullName -Destination $LocalNuGetRepo -Force

    # Clear the local NuGet cache to ensure the new package is used
    Write-Host "🧹 Clearing local NuGet cache..." -ForegroundColor Cyan
    dotnet nuget locals all --clear | Out-Host

    $packageName = $nupkgFile.BaseName.Replace(".$currentVersion", "")
    Write-Host "🎉 Successfully re-published version $currentVersion of $packageName to local repository." -ForegroundColor Magenta
    
    Write-Output $currentVersion
}
catch {
    Write-Error "❌ An error occurred in Publish-Local.ps1: $($_.Exception.Message)"
    exit 1
}