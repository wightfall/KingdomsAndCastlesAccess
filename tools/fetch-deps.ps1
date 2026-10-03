# Downloads the native Prism library into lib/prism (needed to build a release package).
param([string]$PrismVersion = "0.18.3")
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$root = Split-Path -Parent $PSScriptRoot
$lib = Join-Path $root "lib\prism"
if (Test-Path (Join-Path $lib "prism.dll")) { Write-Host "Prism already present in $lib"; exit 0 }
New-Item -ItemType Directory -Force $lib | Out-Null
$zip = Join-Path $env:TEMP "prism-windows-x64-$PrismVersion.zip"
$url = "https://github.com/ethindp/prism/releases/download/v$PrismVersion/prism-windows-x64.zip"
Write-Host "Downloading $url"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($e in $archive.Entries) {
        $name = $e.FullName
        if ($name -eq "dynamic/release/bin/prism.dll") { [IO.Compression.ZipFileExtensions]::ExtractToFile($e, (Join-Path $lib "prism.dll"), $true) }
        elseif ($name -eq "NOTICE" -or $name -like "LICENSES/*/*") {
            if ($name.EndsWith("/")) { continue }
            $dest = Join-Path $lib ("licenses\" + $name.Replace("/", "\"))
            New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
        }
    }
} finally { $archive.Dispose() }
Write-Host "Prism $PrismVersion extracted to $lib"
