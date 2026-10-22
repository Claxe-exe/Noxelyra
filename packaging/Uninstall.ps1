$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Paths.ps1')
$target=Get-NoxelyraInstallRoot
Assert-NoxelyraPath $target
$marker=Get-Content -LiteralPath (Join-Path $target 'noxelyra-install.json') -Raw|ConvertFrom-Json
if($marker.product -ne 'Noxelyra' -or $marker.layout -ne 1){throw 'Noxelyra kurulum işareti geçersiz. Hiçbir dosya silinmedi.'}
$exe=Join-Path $target 'app/Noxelyra.exe'
if(!(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Kurtarma uygulaması bulunamadı. Kurulum korunuyor.'}
$maintenance=Start-Process -FilePath $exe -ArgumentList '--shutdown-for-maintenance' -WindowStyle Hidden -PassThru
if(!$maintenance.WaitForExit(25000)){throw 'Windows görünümü kurtarması zaman aşımına uğradı. Dosyalar korunuyor.'}
if($maintenance.ExitCode -ne 0){throw 'Windows görünümü geri yüklenemedi. Dosyalar ve ayarlar korunuyor.'}
if($env:CLAXE_MAINTENANCE_FIXTURE -ne '1'){
    $shell=New-Object -ComObject WScript.Shell
    try{
        foreach($folder in @('Programs','Startup')){
            $path=Join-Path ([Environment]::GetFolderPath($folder)) 'Noxelyra.lnk'
            if(!(Test-Path -LiteralPath $path)){continue}
            $link=$shell.CreateShortcut($path)
            try{
                if(($link.Description -in @('Noxelyra managed install','Noxelyra managed startup')) -and [string]::Equals($link.TargetPath,$exe,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $path -Force}
            }finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)}
        }
    }finally{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)}
}
# Resolve and check the exact target again immediately before recursive deletion.
Assert-NoxelyraPath $target
Remove-Item -LiteralPath $target -Recurse -Force
Write-Host 'Noxelyra kaldırıldı. Kişisel ayarlar, yerel varlıklar ve kurtarma günlükleri korunuyor.'
