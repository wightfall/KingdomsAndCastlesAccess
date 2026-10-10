# Builds, tests and creates the release zips in dist/.
#   powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-GameDir "D:\Games\Kingdoms and Castles"]
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Kingdoms and Castles",
    [string]$BepInExVersion = "5.4.23.5"
)
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$version = ([xml](Get-Content "Directory.Build.props")).Project.PropertyGroup.Version
if (-not $version) { $version = "1.0.0" }

& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "fetch-deps.ps1")
if ($LASTEXITCODE -ne 0) { throw "fetch-deps failed" }

dotnet test tests/KCAccess.Tests -c Release -nologo
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }
dotnet build src/KCAccess -c Release -nologo -p:DeployToGame=false "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dist = Join-Path $root "dist"
$stage = Join-Path $dist "stage"
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
$plugin = Join-Path $stage "BepInEx\plugins\KCAccess"
New-Item -ItemType Directory -Force $plugin | Out-Null
$bin = Join-Path $root "src\KCAccess\bin\Release\net472"
Copy-Item (Join-Path $bin "KCAccess.dll"), (Join-Path $bin "KCAccess.Core.dll") $plugin
Copy-Item (Join-Path $root "lib\prism\prism.dll") $plugin
Copy-Item README.md, LICENSE, THIRD-PARTY-NOTICES.md $plugin
# Shipped language files: the mod copies them to Languages\<code>.txt (or merges updates into the player's copy).
$langDefault = Join-Path $plugin "Languages\default"
New-Item -ItemType Directory -Force $langDefault | Out-Null
Copy-Item (Join-Path $root "src\KCAccess\Languages\*.txt"), (Join-Path $root "src\KCAccess\Languages\retired.keys") $langDefault
if (-not (Test-Path (Join-Path $langDefault "template.txt"))) { throw "Language files missing" }
$lic = Join-Path $plugin "licenses\prism"
New-Item -ItemType Directory -Force $lic | Out-Null
Copy-Item -Recurse (Join-Path $root "lib\prism\licenses\*") $lic

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
# Zip with forward slashes (ZipFile.CreateFromDirectory on .NET Framework writes backslashes).
function New-Zip([string]$source, [string]$zipPath) {
    $src = (Resolve-Path $source).Path.TrimEnd([char]92)
    $fs = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
    $zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Recurse -File $src | ForEach-Object {
            $rel = $_.FullName.Substring($src.Length + 1).Replace([char]92, [char]47)
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose(); $fs.Dispose() }
}
$modZip = Join-Path $dist "KCAccess-v$version.zip"
New-Zip $stage $modZip

# Full bundle: BepInEx + mod, extract straight into the game folder.
$bepZip = Join-Path $env:TEMP "BepInEx_win_x64_$BepInExVersion.zip"
if (-not (Test-Path $bepZip)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -OutFile $bepZip "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"
}
$bundle = Join-Path $dist "bundle"
[IO.Compression.ZipFile]::ExtractToDirectory($bepZip, $bundle)
Copy-Item -Recurse -Force (Join-Path $stage "*") $bundle
Copy-Item README.md (Join-Path $bundle "KCAccess-README.md")
$bundleZip = Join-Path $dist "KCAccess-v$version-with-BepInEx.zip"
New-Zip $bundle $bundleZip

# Setup / updater program with the bundle built in (works offline, checks GitHub for newer versions).
dotnet build src/KCAccess.Installer -c Release -nologo "-p:PayloadZip=$bundleZip"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Copy-Item (Join-Path $root 'src/KCAccess.Installer/bin/Release/net472/KCAccessSetup.exe') (Join-Path $dist "KCAccess-Setup-v$version.exe")

Remove-Item -Recurse -Force $stage, $bundle
Write-Host "Created:" ; Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name)  $([math]::Round($_.Length / 1KB)) KB" }
