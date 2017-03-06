<#
.SYNOPSIS
   Create NuGet packages for SDL.DXA.Modules.CampaignContent
.EXAMPLE
   .\nuget.ps1 -outputDirectory "c:\nuget" -campaignContentVersion "1.2.3" -campaignContentExampleViewsVersion "1.2.3" -Verbose
#>

Param(
    [Parameter(Mandatory=$false, HelpMessage="Artifact output directory.")]
    [string]$outputDirectory = $PSScriptRoot,

    [Parameter(Mandatory=$false, HelpMessage="SDL.DXA.Modules.CampaignContent version.")]
    [string]$campaignContentVersion = "1.0.0",

    [Parameter(Mandatory=$false, HelpMessage="SDL.DXA.Modules.CampaignContent.Example.Views version.")]
    [string]$campaignContentExampleViewsVersion = "1.0.0"

)

$basePath = "..\SDL.DXA.Modules.CampaignContent"
$packageDestination = Join-Path $PSScriptRoot "packages"

$nuGetPackage = Get-Package -Name NuGet.CommandLine -Destination $packageDestination -ErrorAction SilentlyContinue

if ($nuGetPackage -eq $null) {
    Write-Host "Could not find NuGet executable. Trying to install it..."
    Write-Host

    Install-Package -Name NuGet.CommandLine -Provider Nuget -Source https://www.nuget.org/api/v2 -Destination $packageDestination -Force
}

$nuGetFile = Get-ChildItem -Path (Join-Path $PSScriptRoot "packages") -Filter "nuget.exe" -Recurse | Where-Object { !$_PSIsContainer } | Select-Object -First 1

& $nuGetFile.FullName pack 'SDL.DXA.Modules.CampaignContent.nuspec' -version $campaignContentVersion -basepath $basePath -outputdirectory $outputDirectory
& $nuGetFile.FullName pack 'SDL.DXA.Modules.CampaignContent.Example.Views.nuspec' -version $campaignContentVersion -basepath $basePath -outputdirectory $outputDirectory