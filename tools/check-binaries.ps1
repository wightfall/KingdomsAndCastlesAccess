# Looks at every .exe / .dll in a folder (also inside its .zip files, read in memory) for signs of a file-infecting
# virus such as Sality: an entry point outside the first code section, an executable+writable last section, unusual
# section names, or a .NET program whose entry stub is not the usual "jmp _CorExeMain". Used by package.ps1 before a
# release; exits with 1 when anything looks suspicious.
#   powershell -ExecutionPolicy Bypass -File tools\check-binaries.ps1 [-Folder dist]
param([string]$Folder = (Join-Path (Split-Path -Parent $PSScriptRoot) "dist"))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$known = '.text', '.rsrc', '.reloc', '.data', '.rdata', '.pdata', '.idata', '.edata', '.tls', '.bss', '.CRT', '.didat', '.gfids',
    '_RDATA', '.sxdata', '.00cfg', '.retplne', '.voltbl', 'CODE', 'DATA', 'BSS', '.textbss', '.orpc', '.gehcont', '.fptable'

function Test-PE([byte[]]$b, [string]$name) {
    if ($b.Length -lt 0x40 -or $b[0] -ne 0x4D -or $b[1] -ne 0x5A) { return $null }
    $pe = [BitConverter]::ToInt32($b, 0x3C)
    if ($pe -le 0 -or $pe + 24 -gt $b.Length -or [BitConverter]::ToUInt32($b, $pe) -ne 0x4550) { return "$name : not a valid PE file" }
    $nsec = [BitConverter]::ToUInt16($b, $pe + 6)
    $opt = $pe + 24
    $magic = [BitConverter]::ToUInt16($b, $opt)
    $ep = [BitConverter]::ToUInt32($b, $opt + 16)
    $dd = if ($magic -eq 0x20b) { $opt + 112 } else { $opt + 96 }
    $clr = [BitConverter]::ToUInt32($b, $dd + 14 * 8)
    $sec = $opt + [BitConverter]::ToUInt16($b, $pe + 20)
    $problems = @(); $epSec = $null; $epOff = -1; $last = $null
    for ($i = 0; $i -lt $nsec; $i++) {
        $o = $sec + 40 * $i
        $sname = [Text.Encoding]::ASCII.GetString($b, $o, 8).TrimEnd([char]0)
        $vsize = [BitConverter]::ToUInt32($b, $o + 8); $va = [BitConverter]::ToUInt32($b, $o + 12)
        $rsize = [BitConverter]::ToUInt32($b, $o + 16); $rptr = [BitConverter]::ToUInt32($b, $o + 20)
        $ch = [BitConverter]::ToUInt32($b, $o + 36)
        if ($ep -ge $va -and $ep -lt $va + [Math]::Max($vsize, $rsize)) { $epSec = $sname; $epOff = $rptr + ($ep - $va) }
        if ($known -notcontains $sname) { $problems += "unusual section '$sname'" }
        $last = @{ Name = $sname; X = ($ch -band 0x20000000) -ne 0; W = ($ch -band 0x80000000) -ne 0 }
    }
    if ($ep -ne 0 -and $epSec -ne '.text' -and $epSec -ne 'CODE') { $problems += "entry point in '$epSec'" }
    if ($last -and $last.X -and $last.W) { $problems += "last section '$($last.Name)' is executable and writable" }
    if ($clr -ne 0 -and $ep -ne 0 -and ($magic -ne 0x20b) -and $epOff -ge 0 -and -not ($b[$epOff] -eq 0xFF -and $b[$epOff + 1] -eq 0x25)) { $problems += ".NET entry stub changed" }
    if ($problems.Count -gt 0) { return "$name : SUSPICIOUS: " + ($problems -join '; ') }
    return $null
}

$checked = 0; $bad = @()
Get-ChildItem $Folder -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $Folder).Path.Length).TrimStart('\')
    if ($_.Extension -in '.exe', '.dll') {
        $checked++; $r = Test-PE ([IO.File]::ReadAllBytes($_.FullName)) $rel; if ($r) { $bad += $r }
    } elseif ($_.Extension -eq '.zip') {
        $zip = [IO.Compression.ZipFile]::OpenRead($_.FullName)
        try {
            foreach ($e in $zip.Entries) {
                if ($e.Name -notmatch '\.(exe|dll)$') { continue }
                $ms = New-Object IO.MemoryStream; $s = $e.Open(); $s.CopyTo($ms); $s.Close()
                $checked++; $r = Test-PE $ms.ToArray() ("$rel!" + $e.FullName); if ($r) { $bad += $r }
            }
        } finally { $zip.Dispose() }
    }
}
if ($bad.Count -gt 0) { $bad | ForEach-Object { Write-Host $_ }; Write-Host "Infection check FAILED: $($bad.Count) of $checked program files look modified."; exit 1 }
Write-Host "Infection check: $checked program files, none look modified."
exit 0
