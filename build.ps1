param([switch]$Publish,[switch]$Test,[string]$OutputDirectory="artifacts/VoxLab-win-x64")
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK, then run build.ps1 again.' }
dotnet restore src/VoxLab.App/VoxLab.App.csproj --locked-mode
if($LASTEXITCODE) { throw 'Restore failed.' }
dotnet run --project tests/VoxLab.Tests -c Release -- --export-presets src/VoxLab.App/Web/presets.json
if($LASTEXITCODE) { throw 'Preset export failed.' }
if($Test) {
    dotnet run --project tests/VoxLab.Tests -c Release; if($LASTEXITCODE) { throw 'DSP tests failed.' }
    dotnet restore tests/VoxLab.Windows.Tests --locked-mode; if($LASTEXITCODE) { throw 'Windows test restore failed.' }
    dotnet run --project tests/VoxLab.Windows.Tests -c Release --no-restore; if($LASTEXITCODE) { throw 'Windows audio tests failed.' }
}
if($Publish) {
    dotnet publish src/VoxLab.App/VoxLab.App.csproj -c Release --self-contained true -o $OutputDirectory
    if($LASTEXITCODE) { throw 'Publish failed.' }
    Copy-Item -LiteralPath LICENSE,THIRD-PARTY-NOTICES.md,README.md,START-HERE.txt -Destination $OutputDirectory
    Copy-Item -LiteralPath licenses,docs -Destination $OutputDirectory -Recurse -Force
    Write-Host "Built: $OutputDirectory/VoxLab.exe"
} else {
    dotnet build src/VoxLab.App/VoxLab.App.csproj -c Release
    if($LASTEXITCODE) { throw 'Build failed.' }
}
