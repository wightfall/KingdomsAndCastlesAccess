# Regenerates src/KCAccess/Languages/template.txt from every Loc.T / Loc.F / Loc.N / Loc.P text in the sources and
# rewrites the shipped language files in the template's order (new texts get an empty translation, removed texts
# are dropped, existing translations are kept).
#   powershell -ExecutionPolicy Bypass -File tools\loc-extract.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$env:KCACCESS_UPDATE_LANGUAGES = "1"
try {
    dotnet test tests/KCAccess.Tests -nologo --filter "FullyQualifiedName~LocalizationFilesTests.TemplateListsEveryTextInTheSources"
    if ($LASTEXITCODE -ne 0) { throw "Extraction failed" }
} finally {
    Remove-Item Env:\KCACCESS_UPDATE_LANGUAGES
}
Write-Host "Updated src/KCAccess/Languages (template.txt and the language files)."
