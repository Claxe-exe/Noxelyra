$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet build src/ClaxeDesktop.App -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    dotnet build tests/ClaxeDesktop.WindowProbe -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Window probe build failed'}
    $previousData=$env:CLAXE_DATA_DIR;$previousProbe=$env:CLAXE_WINDOW_PROBE_EXE
    try {
        $env:CLAXE_DATA_DIR=Join-Path $root ('artifacts/advanced-runtime/'+[Guid]::NewGuid().ToString('N'))
        $env:CLAXE_WINDOW_PROBE_EXE=Join-Path $root 'tests/ClaxeDesktop.WindowProbe/bin/Release/net10.0-windows/ClaxeDesktop.WindowProbe.exe'
        New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR|Out-Null
        '{"SchemaVersion":3,"FirstRunComplete":true}'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
        $process=Start-Process -FilePath (Join-Path $root 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe') -ArgumentList '--verify-advanced' -WindowStyle Hidden -PassThru
        if(!$process.WaitForExit(40000)){throw 'Advanced verification timed out; close the isolated instance before retrying.'}
        $path=Join-Path $env:CLAXE_DATA_DIR 'advanced-verification/results.json'
        if(!(Test-Path -LiteralPath $path)){throw 'No results'}
        $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
        $report.checks|Format-Table name,passed
        Copy-Item -LiteralPath $path -Destination (Join-Path $root 'artifacts/phase78-results.json')
        if(!$report.success){throw $report.error}
        if(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')){throw 'Unclean exit'}
        New-Item -ItemType Directory -Force (Join-Path $root 'artifacts/phase78-screenshots')|Out-Null
        Copy-Item -Path (Join-Path $env:CLAXE_DATA_DIR 'advanced-verification/Advanced-*.png') -Destination (Join-Path $root 'artifacts/phase78-screenshots')
        Write-Host "Phases 7–8: $($report.checks.Count)/$($report.checks.Count) passed; clean exit."
    } finally {$env:CLAXE_DATA_DIR=$previousData;$env:CLAXE_WINDOW_PROBE_EXE=$previousProbe}
} finally {Pop-Location}
