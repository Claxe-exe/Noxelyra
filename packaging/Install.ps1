param([switch]$NoShortcut)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Paths.ps1')
$destination=Get-NoxelyraInstallRoot
Assert-NoxelyraPath $destination
if(Test-Path -LiteralPath $destination){throw 'Noxelyra kurulum klasörü zaten var. Önce mevcut kurulumun Uninstall.ps1 dosyasını çalıştır. Ayarların korunur.'}
$payload=Join-Path $PSScriptRoot 'app'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'payload-sha256.json') -Raw|ConvertFrom-Json
if($manifest.Count -lt 4){throw 'Paket manifesti eksik.'}
$payloadPrefix=[IO.Path]::GetFullPath($payload)+[IO.Path]::DirectorySeparatorChar
foreach($entry in $manifest){
    $file=[IO.Path]::GetFullPath((Join-Path $payload $entry.path))
    if(!$file.StartsWith($payloadPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Geçersiz paket yolu.'}
    Assert-NoReparse $file
    if(!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Paket sağlama toplamı hatalı.'}
}
$runtimes=& dotnet --list-runtimes
if($LASTEXITCODE -ne 0 -or !($runtimes -match 'Microsoft.WindowsDesktop.App 10\.')){throw '.NET 10 Desktop Runtime x64 gerekli. README içindeki resmi bağlantıdan kur.'}
$stage=$destination+'.stage-'+[Guid]::NewGuid().ToString('N')
if([IO.Path]::GetDirectoryName($stage) -ne [IO.Path]::GetDirectoryName($destination)){throw 'Geçersiz hazırlık yolu.'}
try{
    New-Item -ItemType Directory -Path (Join-Path $stage 'app') -Force|Out-Null
    foreach($entry in $manifest){
        $target=Join-Path (Join-Path $stage 'app') $entry.path
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($target))|Out-Null
        Copy-Item -LiteralPath (Join-Path $payload $entry.path) -Destination $target
    }
    foreach($name in @('Uninstall.ps1','Paths.ps1','README.txt','DEPENDENCIES.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage}
    '{"product":"Noxelyra","version":"0.11.0","layout":1}'|Set-Content -LiteralPath (Join-Path $stage 'noxelyra-install.json') -Encoding UTF8
    Assert-NoReparse $stage
    Move-Item -LiteralPath $stage -Destination $destination
}finally{
    if(Test-Path -LiteralPath $stage){Assert-NoReparse $stage;Remove-Item -LiteralPath $stage -Recurse -Force}
}
if(!$NoShortcut){
    $shell=New-Object -ComObject WScript.Shell
    $link=$null
    try{
        $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'Noxelyra.lnk'
        $link=$shell.CreateShortcut($shortcut)
        if((Test-Path -LiteralPath $shortcut) -and $link.Description -ne 'Noxelyra managed install'){Write-Warning 'Aynı adlı farklı Başlat kısayolu korundu.'}
        else{$link.TargetPath=Join-Path $destination 'app/Noxelyra.exe';$link.WorkingDirectory=Join-Path $destination 'app';$link.Description='Noxelyra managed install';$link.Save()}
    }finally{if($null -ne $link){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
}
Write-Host "Noxelyra kuruldu: $destination"
Write-Host 'Uygulama otomatik başlatılmadı. Alt+K dock barını öne getirir. Ayarlar kaldırmada korunur.'
