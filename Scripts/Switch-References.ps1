#Requires -Version 5.1
<#
.SYNOPSIS
    Switches C# project references between local ProjectReferences and NuGet PackageReferences.
.DESCRIPTION
    This script automates the process of switching dependencies for development (projects) and testing/production (packages).
    In 'Package' mode, it runs local publishing scripts, removes project references, and adds the latest local NuGet packages.
    In 'Project' mode, it removes the package references, and adds back the direct project references.
    Finally, it rebuilds the solution.
.PARAMETER Mode
    Specifies the reference mode to switch to.
    'Project' - Use direct project-to-project references. Ideal for development.
    'Package' - Use NuGet package references from a local feed. Ideal for testing.
.EXAMPLE
    ./Switch-References.ps1 -Mode Project
    Switches all consumer projects to use ProjectReferences.
.EXAMPLE
    ./Switch-References.ps1 -Mode Package
    Publishes the core libraries locally and switches consumer projects to use PackageReferences.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "Switch between 'Project' or 'Package' references.")]
    [ValidateSet("Project", "Package")]
    [string]$Mode
)

# --- Configuration ---
# Package IDs (used for PackageReference lookups)
$CorePackageIds = @(
    "CmdScale.EntityFrameworkCore.TimescaleDB",
    "CmdScale.EntityFrameworkCore.TimescaleDB.Design"
)

# Map PackageId to actual project file base names (without .csproj)
$PackageToProjectMap = @{
    "CmdScale.EntityFrameworkCore.TimescaleDB" = "TimescaleDB"
    "CmdScale.EntityFrameworkCore.TimescaleDB.Design" = "TimescaleDB.Design"
}

# For backwards compatibility
$CoreLibraryNames = $CorePackageIds

# --- Script Body ---
try {
    # Get the script's directory
    $SolutionRoot = (Get-Item $PSScriptRoot).Parent.FullName
    Write-Host "Solution root identified as: $SolutionRoot" -ForegroundColor Green

    # Find all projects in the solution
    Write-Host "🔍 Finding all projects..."
    $AllProjects = Get-ChildItem -Path $SolutionRoot -Recurse -Filter "*.csproj"

    # Identify the core libraries by matching project file names to package IDs
    $CoreProjectPaths = @{}
    foreach ($project in $AllProjects) {
        foreach ($packageId in $CorePackageIds) {
            $expectedBaseName = $PackageToProjectMap[$packageId]
            if ($project.BaseName -eq $expectedBaseName) {
                $CoreProjectPaths[$packageId] = $project.FullName
                break
            }
        }
    }
    $CoreProjectPaths.GetEnumerator() | ForEach-Object { Write-Host "  - Found core library: $($_.Key) -> $($_.Value)" }

    if ($CoreProjectPaths.Count -ne $CorePackageIds.Count) {
        throw "Could not find all core library projects. Expected: $($CorePackageIds -join ', '). Found: $($CoreProjectPaths.Keys -join ', ')"
    }

    # Build reverse map: project base name -> package ID
    $ProjectToPackageMap = @{}
    foreach ($entry in $PackageToProjectMap.GetEnumerator()) {
        $ProjectToPackageMap[$entry.Value] = $entry.Key
    }

    # Build hash map
    Write-Host "🔎 Analyzing project dependencies..."
    $ConsumerProjectMap = @{}
    foreach ($project in $AllProjects) {
        [xml]$csprojContent = Get-Content $project.FullName -ErrorAction Stop

        $foundCoreRefs = [System.Collections.Generic.List[object]]::new()

        # Find ProjectReferences
        $projectRefNodes = $csprojContent.SelectNodes("//ProjectReference")
        if ($projectRefNodes) {
            foreach ($refNode in $projectRefNodes) {
                $refBaseName = [System.IO.Path]::GetFileNameWithoutExtension(($refNode.Include).Trim())
                # Check if this project file maps to one of our core packages
                if ($ProjectToPackageMap.ContainsKey($refBaseName)) {
                    $packageId = $ProjectToPackageMap[$refBaseName]
                    $foundCoreRefs.Add([PSCustomObject]@{ Name = $packageId; Type = "Project" })
                }
            }
        }

        # Find PackageReferences (these use package IDs directly)
        $packageRefNodes = $csprojContent.SelectNodes("//PackageReference")
        if ($packageRefNodes) {
            foreach ($refNode in $packageRefNodes) {
                $refName = ($refNode.Include).Trim()
                if ($CorePackageIds -contains $refName) {
                    $foundCoreRefs.Add([PSCustomObject]@{ Name = $refName; Type = "Package" })
                }
            }
        }

        if ($foundCoreRefs.Count -gt 0) {
            $uniqueRefs = @()
            $seenNames = New-Object System.Collections.Generic.HashSet[string]
            foreach ($ref in $foundCoreRefs) {
                if ($seenNames.Add($ref.Name)) {
                    $uniqueRefs += $ref
                }
            }

            if ($uniqueRefs.Count -gt 0) {
                $ConsumerProjectMap[$project.FullName] = $uniqueRefs
                $refsAsString = $uniqueRefs | ForEach-Object { "$($_.Name) ($($_.Type))" }
                Write-Host "  - Found consumer project: $($project.Name) (references: $($refsAsString -join ', '))"
            }
        }
    }

    if ($ConsumerProjectMap.Count -eq 0) {
        Write-Host "No consumer projects found that reference the core libraries. Nothing to do." -ForegroundColor Yellow
        return
    }

    # Perform the switch based on the selected mode
    switch ($Mode) {
        "Package" {
            Write-Host "🚀 Switching to Package references..." -ForegroundColor Cyan

            $PublishedVersions = @{}
            # Path to the new, single publish script in the solution root
            $publishScript = Join-Path $SolutionRoot ".\Scripts\Publish-Local.ps1"

            if (-not (Test-Path $publishScript)) {
                throw "The central Publish-Local.ps1 script was not found in the solution root."
            }

            # Publish the core libraries locally using the new central script
            foreach ($projectName in $CoreProjectPaths.Keys) {
                Write-Host "--- Publishing '$projectName' ---"
                # Execute the central script, passing the project name as a parameter
                $newVersion = (& $publishScript -ProjectName $projectName | Out-String).Trim()
            
                if (-not $newVersion) { throw "Publish script for '$projectName' did not output a version." }
                $PublishedVersions[$projectName] = $newVersion
                Write-Host "--- Published '$projectName' version '$newVersion' ---" -ForegroundColor Green
            }

            # Update consumer projects based on the dependency map
            foreach ($consumerPath in $ConsumerProjectMap.Keys) {
                $consumerName = (Get-Item $consumerPath).Name
                Write-Host "  🔧 Modifying '$consumerName' to use packages..."
                foreach ($ref in $ConsumerProjectMap[$consumerPath]) {
                    if ($ref.Type -eq "Project") {
                        $coreProjectPath = $CoreProjectPaths[$ref.Name]
                        $packageVersion = $PublishedVersions[$ref.Name]

                        Write-Host "    - Removing ProjectRef $($ref.Name), Adding PackageRef version $packageVersion..."
                        dotnet remove "$consumerPath" reference "$coreProjectPath"
                        dotnet add "$consumerPath" package $ref.Name --version $packageVersion --no-restore
                    }
                }
            }
        }

        "Project" {
            Write-Host "🔧 Switching to Project references..." -ForegroundColor Cyan
            
            # Update consumer projects based on the dependency map
            foreach ($consumerPath in $ConsumerProjectMap.Keys) {
                $consumerName = (Get-Item $consumerPath).Name
                Write-Host "  🔧 Modifying '$consumerName' to use projects..."
                foreach ($ref in $ConsumerProjectMap[$consumerPath]) {
                    if ($ref.Type -eq "Package") {
                        $coreProjectPath = $CoreProjectPaths[$ref.Name]
                        Write-Host "    - Removing PackageRef $($ref.Name), Adding ProjectRef..."
                        dotnet remove "$consumerPath" package $ref.Name
                        dotnet add "$consumerPath" reference "$coreProjectPath"
                    }
                }
            }
        }
    }

    # A separate, single restore step for the entire solution
    Write-Host "🔄 Restoring all NuGet packages for the solution..." -ForegroundColor Green
    dotnet restore

    # Rebuild the solution
    Write-Host "✅ Rebuilding solution..." -ForegroundColor Green
    dotnet build

    Write-Host "🎉 Script completed successfully." -ForegroundColor Green
}
catch {
    Write-Error "An error occurred: $($_.Exception.Message)"
}