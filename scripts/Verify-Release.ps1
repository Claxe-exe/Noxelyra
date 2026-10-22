$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$package=Join-Path $root 'artifacts/Noxelyra-0.11.0-win-x64'
$run=Join-Path $root ('artifacts/release-install-test/'+[Guid]::NewGuid().ToString('N'))
$previousData=$env:CLAXE_DATA_DIR;$previousFixture=$env:CLAXE_MAINTENANCE_FIXTURE;$previousTarget=$env:CLAXE_INSTALL_TEST_ROOT
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed){$checks.Add([pscustomobject]@{name=$Name;passed=$Passed});if(!$Passed){throw $Name}}
try{
    $shellBefore=@(Get-Process explorer -ErrorAction Stop|ForEach-Object{$_.Id})
    $env:CLAXE_DATA_DIR=Join-Path $run 'data';$env:CLAXE_MAINTENANCE_FIXTURE='1';$env:CLAXE_INSTALL_TEST_ROOT=Join-Path $run 'Noxelyra'
    New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR|Out-Null
    '{"SchemaVersion":3,"FirstRunComplete":true}'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
    'keep me'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'personal.txt')
    & (Join-Path $package 'Install.ps1') -NoShortcut
    $installed=$env:CLAXE_INSTALL_TEST_ROOT
    $exe=Join-Path $installed 'app/Noxelyra.exe'
    Check 'Verified per-user payload install includes original SDK license and notice' ((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath (Join-Path $installed 'app/WebView2-LICENSE.txt')) -and (Test-Path -LiteralPath (Join-Path $installed 'app/WebView2-NOTICE.txt')))
    $app=Start-Process -FilePath $exe -ArgumentList '--verify-release' -WindowStyle Hidden -PassThru
    if(!$app.WaitForExit(25000)){throw 'Release UI verification timeout; isolated instance remains for inspection.'}
    $report=Get-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'release-results.json') -Raw|ConvertFrom-Json
    foreach($check in $report.checks){$checks.Add($check)}
    if(!$report.success){throw $report.error}
    # A small deliberately corrupt package is sufficient to exercise the hash gate.
    $tampered=Join-Path $run 'tampered';New-Item -ItemType Directory -Force (Join-Path $tampered 'app')|Out-Null
    foreach($name in @('Install.ps1','Paths.ps1')){Copy-Item -LiteralPath (Join-Path $package $name) -Destination $tampered}
    $manifest=@(Get-Content -LiteralPath (Join-Path $package 'payload-sha256.json') -Raw|ConvertFrom-Json)
    'corrupted'|Set-Content -LiteralPath (Join-Path $tampered ('app/'+$manifest[0].path))
    ConvertTo-Json -InputObject $manifest -Depth 3|Set-Content -LiteralPath (Join-Path $tampered 'payload-sha256.json')
    $env:CLAXE_INSTALL_TEST_ROOT=Join-Path $run 'other/ClaxeDesktop';$rejected=$false
    try{& (Join-Path $tampered 'Install.ps1') -NoShortcut}catch{$rejected=$_.Exception.Message -like '*sağlama toplamı*'}
    Check 'Corrupted package is rejected before creating install directory' ($rejected -and !(Test-Path -LiteralPath $env:CLAXE_INSTALL_TEST_ROOT))
    $env:CLAXE_INSTALL_TEST_ROOT=$installed
    . (Join-Path $package 'Paths.ps1')
    $rejected=$false;try{Assert-NoxelyraPath $run}catch{$rejected=$true}
    Check 'Deletion guard rejects a target outside exact installation directory' ($rejected -and (Test-Path -LiteralPath $exe))
    'broken recovery record'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'wallpaper-recovery.json')
    $blocked=$false;try{& (Join-Path $installed 'Uninstall.ps1')}catch{$blocked=$_.Exception.Message -like '*geri yüklenemedi*'}
    Check 'Uninstall stops on recovery failure and preserves app and personal data' ($blocked -and (Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'personal.txt')))
    Remove-Item -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'wallpaper-recovery.json')
    & (Join-Path $installed 'Uninstall.ps1')
    $shellAfter=@(Get-Process explorer -ErrorAction Stop|ForEach-Object{$_.Id})
    Check 'Successful uninstall keeps personal data exits cleanly and leaves Explorer processes intact' (!(Test-Path -LiteralPath $installed) -and (Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'personal.txt')) -and !(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')) -and !(@(Compare-Object $shellBefore $shellAfter).Count))
    $checks|Format-Table name,passed
    ConvertTo-Json -InputObject ([ordered]@{success=$true;checks=$checks;runtime=$run;limitations='WM_HOTKEY dispatch tested; physical key and exclusive fullscreen game are manual QA. Taskbar/wallpaper real mutations were not repeated in this scope.'}) -Depth 5|Set-Content -LiteralPath (Join-Path $root 'artifacts/phase9-results.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'summoned-dock.png') -Destination (Join-Path $root 'artifacts/phase9-summoned-dock.png')
    Write-Host "Phase 9: $($checks.Count)/$($checks.Count) passed."
}finally{$env:CLAXE_DATA_DIR=$previousData;$env:CLAXE_MAINTENANCE_FIXTURE=$previousFixture;$env:CLAXE_INSTALL_TEST_ROOT=$previousTarget}
