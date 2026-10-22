$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet build src/ClaxeDesktop.App -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $old=$env:CLAXE_DATA_DIR
    try {
        $env:CLAXE_DATA_DIR=Join-Path $root ('artifacts/taskbar-runtime/'+[Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR|Out-Null
        '{"SchemaVersion":3,"FirstRunComplete":true}'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
        $process=Start-Process -FilePath (Join-Path $root 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe') -ArgumentList '--verify-taskbar' -WindowStyle Hidden -PassThru
        if(!$process.WaitForExit(40000)){throw 'Taskbar verification timed out; isolated process remains available for recovery.'}
        $result=Join-Path $env:CLAXE_DATA_DIR 'taskbar-verification/results.json'
        if(!(Test-Path -LiteralPath $result)){throw 'No taskbar verification results'}
        $report=Get-Content -LiteralPath $result -Raw|ConvertFrom-Json
        $report.checks|Format-Table name,passed
        Copy-Item -LiteralPath $result -Destination (Join-Path $root 'artifacts/phase6-taskbar-results.json')
        if(!$report.success){throw $report.error}
        if(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')){throw 'Unclean exit'}
        Write-Host "Taskbar/wallpaper: $($report.checks.Count)/$($report.checks.Count) passed; clean exit."
    } finally {$env:CLAXE_DATA_DIR=$old}
} finally {Pop-Location}
