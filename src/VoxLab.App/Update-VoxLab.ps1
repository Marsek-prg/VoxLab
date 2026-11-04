param([Parameter(Mandatory=$true)][string]$PlanFile,[switch]$NoRestart)
$ErrorActionPreference='Stop'
$lockStream=$null
$applied=New-Object System.Collections.Generic.List[object]
$parentGone=$false
$plan=$null
$resultPath=$null
$rollbackErrors=New-Object System.Collections.Generic.List[string]
function Safe-Path([string]$Base,[string]$Relative) {
    if($Relative -cne 'VoxLab.exe' -and $Relative -cne 'Начни здесь.txt' -and -not $Relative.StartsWith('App/')) { throw 'Invalid update destination.' }
    $badSegments=@($Relative.Split('/') | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' -or $_ -match '[ .]$' -or $_ -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)' -or $_ -eq '.update.lock' })
    if($Relative.Length -gt 220 -or $Relative -match '[\\:*?"<>|\x00-\x1f]' -or $badSegments.Count -gt 0) { throw 'Unsafe update path.' }
    $full=[IO.Path]::GetFullPath((Join-Path $Base $Relative))
    $prefix=[IO.Path]::GetFullPath($Base).TrimEnd('\')+'\'
    if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside portable folder.' }
    $probe=$full
    while($probe) {
        if(Test-Path -LiteralPath $probe) {
            if(([IO.File]::GetAttributes($probe) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Symbolic links are not supported for updates.' }
        }
        $probe=[IO.Path]::GetDirectoryName($probe)
    }
    return $full
}
function Replace-File([string]$Source,[string]$Destination) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destination)) | Out-Null
    $temporary=$Destination+'.voxlab-'+[Guid]::NewGuid().ToString('N')
    try {
        [IO.File]::Copy($Source,$temporary,$false)
        if([IO.File]::Exists($Destination)) {
            # Windows PowerShell converts a null string argument into an empty
            # path. An explicit sibling backup works on .NET Framework too.
            $previous=$temporary+'.previous'
            [IO.File]::Replace($temporary,$Destination,$previous)
            try { [IO.File]::Delete($previous) } catch { }
        }
        else { [IO.File]::Move($temporary,$Destination) }
    } finally { if([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}
function Hash-File([string]$Path) {
    $stream=[IO.File]::OpenRead($Path)
    $sha=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose();$stream.Dispose() }
}
try {
    $PlanFile=[IO.Path]::GetFullPath($PlanFile)
    $stage=[IO.Path]::GetDirectoryName($PlanFile)
    $plan=Get-Content -LiteralPath $PlanFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $root=[IO.Path]::GetFullPath($plan.root)
    $payload=[IO.Path]::GetFullPath($plan.payload)
    if($payload -ne (Join-Path $stage 'payload')) { throw 'Unexpected staging folder.' }
    $data=[IO.Path]::GetFullPath($plan.data)
    if(-not $stage.StartsWith((Join-Path $data 'Updates')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Staging folder is outside Data/Updates.' }
    $resultPath=Join-Path $data 'Updates/last-result.json'
    $lockPath=Join-Path $root 'App/.update.lock'
    # Validate the ancestor chain before opening the installation lock.
    $null=Safe-Path $root 'App/VoxLab.dll'
    $lockStream=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $parent=Get-Process -Id $plan.parentPid -ErrorAction SilentlyContinue
    $waitForParent=$null -ne $parent -and $parent.StartTime.ToUniversalTime().Ticks -eq [long]$plan.parentStarted
    [IO.File]::WriteAllText((Join-Path $stage 'installer-ready'),'ready')
    if($waitForParent) {
        if(-not $parent.WaitForExit(45000)) { throw 'VoxLab did not exit; no files were changed.' }
    }
    $parentGone=$true
    if($plan.files.Count -gt 2000) { throw 'Too many update files.' }
    $backup=Join-Path $stage 'backup'
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    $entries=New-Object System.Collections.Generic.List[object]
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $plan.files) {
        if(-not $seen.Add($file.path) -or $file.sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid file entry.' }
        $source=Safe-Path $payload $file.path
        $target=Safe-Path $root $file.path
        $saved=Safe-Path $backup $file.path
        if((Get-Item -LiteralPath $source).Length -ne $file.length -or (Hash-File $source) -ne $file.sha256) { throw ('Staged file failed verification: '+$file.path) }
        $existed=[IO.File]::Exists($target)
        if(Test-Path -LiteralPath $target -PathType Container) { throw 'A directory conflicts with an update file.' }
        if($existed) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            [IO.File]::Copy($target,$saved,$false)
        }
        $entries.Add([pscustomobject]@{source=$source;target=$target;backup=$saved;existed=$existed;hash=$file.sha256})
    }
    foreach($entry in $entries) {
        Replace-File $entry.source $entry.target
        $applied.Add($entry)
        if((Hash-File $entry.target) -ne $entry.hash) { throw 'Installed file failed verification.' }
    }
    @{success=$true;version=$plan.version;message='Update installed';backup=$backup;time=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
} catch {
    $failure=$_.Exception.Message
    $rollbackErrors=New-Object System.Collections.Generic.List[string]
    for($i=$applied.Count-1;$i -ge 0;$i--) {
        $entry=$applied[$i]
        try {
            if($entry.existed) { Replace-File $entry.backup $entry.target }
            elseif([IO.File]::Exists($entry.target)) { [IO.File]::Delete($entry.target) }
        } catch { $rollbackErrors.Add($_.Exception.Message) }
    }
    if($resultPath) {
        @{success=$false;message=$failure;rollbackErrors=@($rollbackErrors);backup=$backup;time=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
    }
} finally {
    if($lockStream) { $lockStream.Dispose() }
}
if($parentGone -and -not $NoRestart -and $plan -and $rollbackErrors.Count -eq 0) {
    $executable=Join-Path $plan.root 'VoxLab.exe'
    Start-Process -FilePath $executable -WorkingDirectory $plan.root -ArgumentList @('--data-dir',('"'+$plan.data.TrimEnd('\')+'"'))
}
