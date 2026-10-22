param([switch]$SafeMode)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet build ClaxeDesktop.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $testData = Join-Path $projectRoot 'artifacts/runtime'
    New-Item -ItemType Directory -Path $testData -Force | Out-Null
    $settingsPath = Join-Path $testData 'settings.json'
    if (!(Test-Path -LiteralPath $settingsPath)) { '{"SchemaVersion":1,"FirstRunComplete":true}' | Set-Content -LiteralPath $settingsPath -Encoding UTF8 }
    $previousData = $env:CLAXE_DATA_DIR
    $previousProbe = $env:CLAXE_PROBE_EXE
    $previousWindowProbe = $env:CLAXE_WINDOW_PROBE_EXE
    try {
        $env:CLAXE_DATA_DIR = $testData
        $env:CLAXE_PROBE_EXE = Join-Path $projectRoot 'tests/ClaxeDesktop.Core.Tests/bin/Release/net10.0-windows/ClaxeDesktop.Core.Tests.exe'
        $env:CLAXE_WINDOW_PROBE_EXE = Join-Path $projectRoot 'tests/ClaxeDesktop.WindowProbe/bin/Release/net10.0-windows/ClaxeDesktop.WindowProbe.exe'
        $arguments = @('--verify')
        if ($SafeMode) { $arguments += '--safe-mode' }
        $started = Get-Date
        $process = Start-Process -FilePath (Join-Path $projectRoot 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(60000)) { throw 'Verification timed out. Close the Claxe test instance before retrying.' }
        $reportPath = Join-Path $testData 'verification/results.json'
        if (!(Test-Path $reportPath) -or (Get-Item $reportPath).LastWriteTime -lt $started) { throw 'No fresh report; another Claxe instance may already be running.' }
        $report = Get-Content $reportPath -Raw | ConvertFrom-Json
        $report.checks | Format-Table name,passed,informational
        if (!$report.success -or ($report.checks | Where-Object { !$_.informational -and !$_.passed })) { throw 'WPF integration verification failed.' }
        if (Test-Path (Join-Path $testData 'session.lock')) { throw 'Clean exit marker was not removed.' }
        Write-Host 'WPF integration verification passed; clean exit confirmed.'
    } finally { $env:CLAXE_DATA_DIR = $previousData; $env:CLAXE_PROBE_EXE = $previousProbe; $env:CLAXE_WINDOW_PROBE_EXE = $previousWindowProbe }
} finally { Pop-Location }


