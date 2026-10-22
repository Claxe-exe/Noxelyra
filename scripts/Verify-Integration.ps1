$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet build src/ClaxeDesktop.App -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $previousData=$env:CLAXE_DATA_DIR
    $previousProbe=$env:CLAXE_PROBE_EXE
    try {
        $env:CLAXE_DATA_DIR=Join-Path $root 'artifacts/integration-runtime'
        $env:CLAXE_PROBE_EXE=Join-Path $root 'tests/ClaxeDesktop.Core.Tests/bin/Release/net10.0-windows/ClaxeDesktop.Core.Tests.exe'
        if(!(Test-Path -LiteralPath $env:CLAXE_PROBE_EXE)){throw 'Launch probe is missing. Build tests/ClaxeDesktop.Core.Tests first.'}
        New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR | Out-Null
        '{"SchemaVersion":3,"FirstRunComplete":true}' | Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
        $started=Get-Date
        $process=Start-Process -FilePath (Join-Path $root 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe') -ArgumentList '--verify-integration' -PassThru -WindowStyle Hidden
        if(!$process.WaitForExit(30000)){throw 'Integration verification timed out; close the isolated test instance before retrying.'}
        $path=Join-Path $env:CLAXE_DATA_DIR 'verification/results.json'
        if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).LastWriteTime -lt $started){throw 'No fresh results'}
        $report=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $report.checks | Format-Table name,passed
        if(!$report.success){throw $report.error}
        if(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')){throw 'Unclean exit'}
        Write-Host "Integration: $($report.checks.Count)/$($report.checks.Count) passed; clean exit."
    } finally {$env:CLAXE_DATA_DIR=$previousData;$env:CLAXE_PROBE_EXE=$previousProbe}
} finally {Pop-Location}
