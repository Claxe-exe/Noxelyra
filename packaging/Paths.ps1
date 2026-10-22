function Get-NoxelyraInstallRoot {
    if($env:CLAXE_MAINTENANCE_FIXTURE -eq '1' -and $env:CLAXE_INSTALL_TEST_ROOT){return [IO.Path]::GetFullPath($env:CLAXE_INSTALL_TEST_ROOT)}
    return [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/Noxelyra'))
}
function Assert-NoReparse([string]$Path){
    $current=[IO.Path]::GetFullPath($Path)
    while($current){
        if((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Bağlantı/junction içeren kurulum yolu reddedildi.'}
        $current=[IO.Path]::GetDirectoryName($current)
    }
    if(Test-Path -LiteralPath $Path -PathType Container){
        foreach($item in Get-ChildItem -LiteralPath $Path -Recurse -Force){if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Kurulum içinde bağlantı/junction bulundu; işlem durduruldu.'}}
    }
}
function Assert-NoxelyraPath([string]$Path){
    $full=[IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if($full -ne (Get-NoxelyraInstallRoot) -or [IO.Path]::GetFileName($full) -ne 'Noxelyra' -or $full -eq [IO.Path]::GetPathRoot($full)){throw 'Kurulum hedefi doğrulanamadı.'}
    Assert-NoReparse $full
}
