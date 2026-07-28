param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = "artifacts"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $projectRoot $OutputDirectory
$publishDirectory = Join-Path $artifacts "publish"
$packageName = "LMU-Overlay-$Version-$Runtime"
$packageDirectory = Join-Path $artifacts $packageName
$archivePath = Join-Path $artifacts "$packageName.zip"
$checksumPath = "$archivePath.sha256"
$manifestPath = Join-Path $artifacts "latest.json"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}
if (Test-Path -LiteralPath $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

$desktopProject = Join-Path $projectRoot "src/LmuOverlay.Desktop/LmuOverlay.Desktop.csproj"
$nugetConfig = Join-Path $projectRoot "NuGet.Config"

dotnet restore $desktopProject `
    --runtime $Runtime `
    --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet publish $desktopProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --no-restore `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $publishDirectory "LmuOverlay.Desktop.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Published executable was not created at $executablePath."
}

Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $packageDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "CHANGELOG.md") -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "SECURITY.md") -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "docs/desktop/quick-start.md") `
    -Destination (Join-Path $packageDirectory "QUICK-START.md")
Copy-Item -LiteralPath (Join-Path $projectRoot "docs/eac/safety-model.md") `
    -Destination (Join-Path $packageDirectory "EAC-SAFETY.md")

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archivePath
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
"$hash  $packageName.zip" | Set-Content -LiteralPath $checksumPath -Encoding ascii
$manifest = [ordered]@{
    schemaVersion = 1
    version = $Version
    runtime = $Runtime
    archive = "$packageName.zip"
    sha256 = $hash
    releaseUrl = "https://github.com/Zezinho11/LMU-Overlay/releases/tag/v$Version"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    minimumWindowsVersion = "10.0.17763"
    signed = ((Get-AuthenticodeSignature -LiteralPath $executablePath).Status -eq "Valid")
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Created $archivePath"
Write-Host "Created $checksumPath"
Write-Host "Created $manifestPath"
