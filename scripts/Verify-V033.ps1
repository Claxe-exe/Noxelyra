param([string]$Executable)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Executable){
    dotnet build (Join-Path $root 'src/ClaxeDesktop.App') -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $Executable=Join-Path $root 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe'
}
$old=$env:CLAXE_DATA_DIR
try{
    $before=@(Get-Process explorer|ForEach-Object{$_.Id})
    $env:CLAXE_DATA_DIR=Join-Path $root ('artifacts/v033-runtime/'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR|Out-Null
    '{"SchemaVersion":3,"FirstRunComplete":true}'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
    $process=Start-Process -FilePath $Executable -ArgumentList '--verify-v033' -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(90000)){throw 'Verification timeout; isolated process retained for inspection.'}
    $path=Join-Path $env:CLAXE_DATA_DIR 'v033-results.json'
    $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    $report.checks|Format-Table name,passed,detail -Wrap
    $out=Join-Path $root 'artifacts/v033'; New-Item -ItemType Directory -Force $out|Out-Null
    Copy-Item -LiteralPath $path -Destination $out
    Get-ChildItem -LiteralPath $env:CLAXE_DATA_DIR -Filter 'v03*.png'|Copy-Item -Destination $out
    if(!$report.success){throw $report.error}
    if(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')){throw 'Unclean exit'}
    $after=@(Get-Process explorer|ForEach-Object{$_.Id})
    if(@(Compare-Object $before $after).Count){throw 'Explorer processes changed during verification; investigate before attributing cause.'}
    Write-Host "v0.33: $($report.checks.Count)/$($report.checks.Count) checks passed; clean exit, Explorer intact."
}finally{$env:CLAXE_DATA_DIR=$old}
