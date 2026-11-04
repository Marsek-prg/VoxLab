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
    if(-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory=Join-Path $PSScriptRoot $OutputDirectory }
    $OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
    if(Test-Path -LiteralPath (Join-Path $OutputDirectory 'VoxLab.dll')) {
        throw 'This is an old flat package. Publish to a new folder and copy your Data folder there.'
    }
    $applicationDirectory=Join-Path $OutputDirectory 'App'
    New-Item -ItemType Directory -Path $applicationDirectory -Force | Out-Null
    dotnet publish src/VoxLab.App/VoxLab.App.csproj -c Release --self-contained true -o $applicationDirectory "-p:VoxLabPortableRoot=$OutputDirectory"
    if($LASTEXITCODE) { throw 'Publish failed.' }
    Remove-Item -LiteralPath (Join-Path $applicationDirectory 'VoxLab.exe')
    Copy-Item -LiteralPath LICENSE,THIRD-PARTY-NOTICES.md,README.md -Destination $applicationDirectory
    Copy-Item -LiteralPath licenses,docs -Destination $applicationDirectory -Recurse -Force
    Copy-Item -LiteralPath README.txt -Destination (Join-Path $OutputDirectory 'README.txt')
    Write-Host "Built: $OutputDirectory/VoxLab.exe"
} else {
    dotnet build src/VoxLab.App/VoxLab.App.csproj -c Release
    if($LASTEXITCODE) { throw 'Build failed.' }
}
