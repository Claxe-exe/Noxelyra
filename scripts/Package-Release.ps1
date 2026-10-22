$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Version comes from the app project so the package name cannot drift from the EXE metadata.
$version=([xml](Get-Content -LiteralPath (Join-Path $root 'src/ClaxeDesktop.App/ClaxeDesktop.App.csproj') -Raw)).Project.PropertyGroup.Version
$short=($version -split '\.')[0..1] -join '.'
$publish=Join-Path $root "artifacts/publish-v$short"
$package=Join-Path $root "artifacts/Noxelyra-$version-win-x64"
# ContinuousIntegrationBuild writes repo-relative (/_/) source paths, so published PDBs never carry the build machine's folders.
dotnet publish (Join-Path $root 'src/ClaxeDesktop.App') -c Release -r win-x64 --self-contained false -o $publish --nologo -p:ContinuousIntegrationBuild=true
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
if(Test-Path -LiteralPath $package){throw 'Package directory already exists; inspect before regenerating.'}
New-Item -ItemType Directory -Force (Join-Path $package 'app')|Out-Null
Copy-Item -Path (Join-Path $publish '*') -Destination (Join-Path $package 'app') -Recurse
Copy-Item -Path (Join-Path $root 'packaging/*') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'docs/DEPENDENCIES.md') -Destination $package
$prefix=(Join-Path $package 'app')+[IO.Path]::DirectorySeparatorChar
$entries=@(Get-ChildItem -LiteralPath (Join-Path $package 'app') -File -Recurse|ForEach-Object{
    [ordered]@{path=$_.FullName.Substring($prefix.Length);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
ConvertTo-Json -InputObject $entries -Depth 3|Set-Content -LiteralPath (Join-Path $package 'payload-sha256.json') -Encoding UTF8
Compress-Archive -Path (Join-Path $package '*') -DestinationPath ($package+'.zip')
Get-FileHash -LiteralPath ($package+'.zip') -Algorithm SHA256|Format-List
((Get-FileHash -LiteralPath ($package+'.zip') -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($package+'.zip'))|Set-Content -LiteralPath ($package+'.zip.sha256') -Encoding ASCII
