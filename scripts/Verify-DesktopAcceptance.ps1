param([string]$WallpaperEngine,[string]$Video,[string]$Executable)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WallpaperEngine){
    $candidate=Get-Process wallpaper64,wallpaper32 -ErrorAction SilentlyContinue|Select-Object -First 1
    if($candidate){$WallpaperEngine=$candidate.Path}
}
if(!$WallpaperEngine -or !(Test-Path -LiteralPath $WallpaperEngine -PathType Leaf)){throw 'Start Wallpaper Engine and pass its executable with -WallpaperEngine.'}
if(!$Video){$Video=(Get-ChildItem -LiteralPath (Join-Path $root 'artifacts/noxelyra-runtime') -Filter fixture.mp4 -Recurse -ErrorAction SilentlyContinue|Select-Object -Last 1).FullName}
if(!$Video -or !(Test-Path -LiteralPath $Video -PathType Leaf)){throw 'Pass a local acceptance MP4 using -Video; Verify-Noxelyra generates one.'}
if(!$Executable){
    dotnet build (Join-Path $root 'src/ClaxeDesktop.App') -c Release --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $Executable=Join-Path $root 'src/ClaxeDesktop.App/bin/Release/net10.0-windows10.0.17763.0/Noxelyra.exe'
}
$previousData=$env:CLAXE_DATA_DIR;$previousWe=$env:NOXELYRA_ACCEPTANCE_WE;$previousVideo=$env:NOXELYRA_ACCEPTANCE_VIDEO
try{
    $before=@(Get-Process explorer|ForEach-Object{$_.Id})
    $env:CLAXE_DATA_DIR=Join-Path $root ('artifacts/acceptance-runtime/'+[Guid]::NewGuid().ToString('N'))
    $env:NOXELYRA_ACCEPTANCE_WE=[IO.Path]::GetFullPath($WallpaperEngine);$env:NOXELYRA_ACCEPTANCE_VIDEO=[IO.Path]::GetFullPath($Video)
    New-Item -ItemType Directory -Force $env:CLAXE_DATA_DIR|Out-Null
    '{"SchemaVersion":3,"FirstRunComplete":true}'|Set-Content -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'settings.json') -Encoding UTF8
    $process=Start-Process -FilePath $Executable -ArgumentList '--verify-desktop-acceptance' -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(55000)){throw "Acceptance timed out. Inspect $env:CLAXE_DATA_DIR and recovery journals before terminating anything."}
    $path=Join-Path $env:CLAXE_DATA_DIR 'desktop-acceptance.json'
    $report=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    Copy-Item -LiteralPath $path -Destination (Join-Path $root 'artifacts/desktop-acceptance.json')
    $report.checks|Format-Table name,passed
    if(!$report.success){throw $report.error}
    if(Test-Path -LiteralPath (Join-Path $env:CLAXE_DATA_DIR 'session.lock')){throw 'Unclean exit'}
    $after=@(Get-Process explorer|ForEach-Object{$_.Id})
    if(@(Compare-Object $before $after).Count){throw 'Explorer process set changed; investigate.'}
    Write-Host "Desktop acceptance: $($report.checks.Count)/$($report.checks.Count); real WE restore and crash recovery passed."
}finally{$env:CLAXE_DATA_DIR=$previousData;$env:NOXELYRA_ACCEPTANCE_WE=$previousWe;$env:NOXELYRA_ACCEPTANCE_VIDEO=$previousVideo}
