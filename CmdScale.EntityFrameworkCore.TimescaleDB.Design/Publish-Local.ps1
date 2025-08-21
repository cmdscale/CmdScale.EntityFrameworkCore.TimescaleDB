#requires -Version 5.1
<#
.SYNOPSIS
    Increments the version of a .NET project, packs it, and copies the NuGet package to a local directory.
    (This version is compatible with older .NET SDKs that do not have the `dotnet version` command).

.PARAMETER VersionBump
    Specifies which part of the version to increment.
    Valid values are 'major', 'minor', or 'patch'. Defaults to 'patch'.
#>
[CmdletBinding()]
param (
    [ValidateSet('major', 'minor', 'patch')]
    [string]$VersionBump = 'patch'
)

$ErrorActionPreference = 'Stop'

# --- SCRIPT CONFIGURATION ---
$LocalNuGetRepo = "C:\_DEV\NuGet Packages"

try {
    # 1. Find the project file automatically
    $ProjectFile = Get-ChildItem -Path . -Filter *.csproj -Recurse | Select-Object -First 1
    if (-not $ProjectFile) {
        throw "No .csproj file found in the current directory or subdirectories."
    }
    Write-Host "📁 Found project: $($ProjectFile.FullName)" -ForegroundColor Green

    # 2. Read the project file to get the current version and package ID
    [xml]$csprojContent = Get-Content $ProjectFile.FullName
    $versionNode = $csprojContent.Project.PropertyGroup.Version
    $packageName = $csprojContent.Project.PropertyGroup.PackageId

    if (-not $packageName) {
        $packageName = $csprojContent.Project.PropertyGroup.AssemblyName
    }
    if (-not $versionNode -or -not $packageName) {
        throw "Could not determine PackageId or Version from $($ProjectFile.Name). Please ensure <PackageId> and <Version> are set in your .csproj."
    }

    # --- Start: Manual Version Bumping Logic ---
    Write-Host "🔼 Bumping '$VersionBump' version from '$($versionNode)'..." -ForegroundColor Cyan
    $versionParts = $versionNode.Split('.')
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]

    switch ($VersionBump) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }

    $newVersion = "$major.$minor.$patch"
    $csprojContent.Project.PropertyGroup.Version = $newVersion
    $csprojContent.Save($ProjectFile.FullName)
    # --- End: Manual Version Bumping Logic ---
    
    Write-Host "✅ New version is '$newVersion'" -ForegroundColor Green

    # Build the project
    dotnet clean
    dotnet build -c Release

    # Pack the project
    Write-Host "📦 Packing project '$packageName'..." -ForegroundColor Cyan
    dotnet pack $ProjectFile.FullName -c Release -o "./bin/Release"

    # Copy the NuGet package
    $packageFileName = "$packageName.$newVersion.nupkg"
    $sourcePackagePath = Join-Path $ProjectFile.DirectoryName "bin\Release\$packageFileName"

    if (-not (Test-Path $sourcePackagePath)) {
        throw "Packed file not found at '$sourcePackagePath'. The build may have failed."
    }

    if (-not (Test-Path $LocalNuGetRepo)) {
        Write-Host "Destination directory '$LocalNuGetRepo' not found. Creating it..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $LocalNuGetRepo | Out-Null
    }

    Write-Host "🚚 Copying '$packageFileName' to '$LocalNuGetRepo'..." -ForegroundColor Cyan
    Copy-Item -Path $sourcePackagePath -Destination $LocalNuGetRepo -Force

    Write-Host "🎉 Successfully published version $newVersion of $packageName to local repository." -ForegroundColor Magenta

}
catch {
    Write-Error "❌ An error occurred: $($_.Exception.Message)"
    exit 1
}