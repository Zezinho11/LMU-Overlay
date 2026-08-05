param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts\visual-baselines")
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\src\LmuOverlay.Desktop\LmuOverlay.Desktop.csproj"
dotnet run --project $project --configuration Release -- --capture-visual-baselines $OutputDirectory
Write-Host "Visual baselines written to $OutputDirectory"
