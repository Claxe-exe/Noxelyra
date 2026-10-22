param([switch]$Upload)
# Builds Noxelyra-win-Setup.exe (one-click per-user install, shortcuts, uninstall entry, self-update) and a portable zip with Velopack.
# The app is published self-contained, so the target PC needs no separate .NET runtime.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=([xml](Get-Content -LiteralPath (Join-Path $root 'src/ClaxeDesktop.App/ClaxeDesktop.App.csproj') -Raw)).Project.PropertyGroup.Version
$publish=Join-Path $root 'artifacts/setup-publish'
$output=Join-Path $root "artifacts/velopack/$version"
if(Test-Path -LiteralPath $output){throw "Output $output already exists; inspect before regenerating."}
if(Test-Path -LiteralPath $publish){Remove-Item -LiteralPath $publish -Recurse -Force}
# ContinuousIntegrationBuild writes repo-relative (/_/) source paths, so shipped PDBs never carry this machine's folders.
dotnet publish (Join-Path $root 'src/ClaxeDesktop.App') -c Release -r win-x64 --self-contained true -o $publish --nologo -p:ContinuousIntegrationBuild=true
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $publish
$repo='https://github.com/Claxe-exe/Noxelyra'
New-Item -ItemType Directory -Force $output|Out-Null
# Fetch the previous release so vpk can build a small delta package for existing installs.
vpk download github --repoUrl $repo --outputDir $output 2>$null
vpk pack --runtime win-x64 --packId Noxelyra --packVersion $version --packDir $publish --mainExe Noxelyra.exe --packTitle Noxelyra --packAuthors Claxe-exe --icon (Join-Path $root 'src/ClaxeDesktop.App/Assets/Noxelyra.ico') --outputDir $output
if($LASTEXITCODE -ne 0){throw 'vpk pack failed'}
Get-ChildItem -LiteralPath $output -File|ForEach-Object{"{0,-45} {1,10:N0} KB  {2}" -f $_.Name,($_.Length/1KB),(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
if($Upload){
    vpk upload github --repoUrl $repo --outputDir $output --publish --releaseName "Noxelyra $version" --tag "v$version" --token (gh auth token)
    if($LASTEXITCODE -ne 0){throw 'vpk upload failed'}
}
