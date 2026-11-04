param([string]$Scratch=(Join-Path $env:TEMP ('VoxLab-updater-tests-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$Scratch=[IO.Path]::GetFullPath($Scratch)
[IO.Directory]::CreateDirectory($Scratch) | Out-Null
$helper=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/VoxLab.App/Update-VoxLab.ps1'))
$passed=0
function Assert($condition,[string]$message) { if(-not $condition) { throw $message } }
function Fixture([string]$name) {
    $root=Join-Path $Scratch $name
    $data=Join-Path $root 'Data'
    $stage=Join-Path $data 'Updates/test'
    $payload=Join-Path $stage 'payload'
    [IO.Directory]::CreateDirectory((Join-Path $root 'App')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $payload 'App')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $data 'profile.json'),'USER DATA')
    [IO.File]::WriteAllText((Join-Path $root 'App/extra.txt'),'UNKNOWN FILE')
    $files=@()
    foreach($path in @('App/VoxLab.dll','App/second.dll','Начни здесь.txt')) {
        [IO.File]::WriteAllText((Join-Path $root $path),'old '+$path)
        [IO.File]::WriteAllText((Join-Path $payload $path),'new '+$path)
        $files+=@{path=$path;sha256=(Get-FileHash -LiteralPath (Join-Path $payload $path) -Algorithm SHA256).Hash.ToLowerInvariant();length=(Get-Item -LiteralPath (Join-Path $payload $path)).Length;component='app'}
    }
    $plan=@{root=$root;data=$data;payload=$payload;version='0.2.0';parentPid=2147483647;parentStarted=0;files=$files}
    return @{root=$root;data=$data;stage=$stage;plan=$plan;file=(Join-Path $stage 'plan.json')}
}
function Run-Update($fixture) {
    $fixture.plan | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $fixture.file -Encoding UTF8
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=(Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe')
    $info.Arguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$helper+'" -PlanFile "'+$fixture.file+'" -NoRestart'
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $process=[Diagnostics.Process]::Start($info)
    Assert ($process.WaitForExit(20000)) 'Updater did not finish.'
    Assert ($process.ExitCode -eq 0) 'Updater script failed to execute.'
    $process.Dispose()
    return (Get-Content -LiteralPath (Join-Path $fixture.data 'Updates/last-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
}
$f=Fixture 'Success with spaces'
$result=Run-Update $f
Assert $result.success ('Success path failed: '+$result.message)
foreach($entry in $f.plan.files) { Assert ([IO.File]::ReadAllText((Join-Path $f.root $entry.path)) -eq ('new '+$entry.path)) 'File was not updated.' }
Assert ([IO.File]::ReadAllText((Join-Path $f.data 'profile.json')) -eq 'USER DATA') 'Profile lost.'
Assert ([IO.File]::ReadAllText((Join-Path $f.root 'App/extra.txt')) -eq 'UNKNOWN FILE') 'Unmanaged file lost.'
Assert ([IO.File]::ReadAllText((Join-Path $result.backup 'App/VoxLab.dll')) -eq 'old App/VoxLab.dll') 'No backup.'
Write-Output 'PASS Unicode/space paths, backups, Data and unmanaged files';$passed++

$f=Fixture 'Bad hash';$f.plan.files[1].sha256=('a'*64)
$result=Run-Update $f
Assert (-not $result.success) 'Bad hash accepted.'
Assert ([IO.File]::ReadAllText((Join-Path $f.root 'App/VoxLab.dll')) -eq 'old App/VoxLab.dll') 'Partial install before verification.'
Write-Output 'PASS Corrupt payload rejected before mutation';$passed++

$f=Fixture 'Traversal';$f.plan.files[1].path='App/../../outside.dll'
$result=Run-Update $f
Assert (-not $result.success) 'Traversal accepted.'
Assert ([IO.File]::ReadAllText((Join-Path $f.root 'App/VoxLab.dll')) -eq 'old App/VoxLab.dll') 'Partial traversal update.'
Write-Output 'PASS Unsafe path rejected before mutation';$passed++

$f=Fixture 'Rollback'
$held=[IO.File]::Open((Join-Path $f.root 'App/second.dll'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try { $result=Run-Update $f } finally { $held.Dispose() }
Assert (-not $result.success -and $result.rollbackErrors.Count -eq 0) ('Rollback failed: '+$result.message)
foreach($entry in $f.plan.files) { Assert ([IO.File]::ReadAllText((Join-Path $f.root $entry.path)) -eq ('old '+$entry.path)) 'Rollback left mixed versions.' }
Write-Output 'PASS Locked file restores previously replaced files';$passed++

$f=Fixture 'Concurrent install'
$held=[IO.File]::Open((Join-Path $f.root 'App/.update.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try { $result=Run-Update $f } finally { $held.Dispose() }
Assert (-not $result.success -and -not (Test-Path -LiteralPath (Join-Path $f.stage 'installer-ready'))) 'Second installer acquired lock.'
Assert ([IO.File]::ReadAllText((Join-Path $f.root 'App/VoxLab.dll')) -eq 'old App/VoxLab.dll') 'Concurrent installer changed file.'
Write-Output 'PASS Concurrent installation rejected';$passed++
Write-Output "$passed/5 installer checks passed. Fixtures: $Scratch"
