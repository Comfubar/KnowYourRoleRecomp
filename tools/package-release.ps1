# Builds the release zip in the player's layout:
#   KnowYourRole.exe      the one program to start (setup on the first run, then the launcher; one self-contained file)
#   README.txt
#   KnowYourRole-files\   everything else:
#     runtime\            the game runtime without any recompiled code, the port's config and function maps, the
#                         controller mapping database; the build adds the recompiled game here on the player's PC
#     LICENSE.txt, THIRD-PARTY-NOTICES.txt
#     (user\ with saves, settings and logs is made on the player's PC)
# No game data goes in; the audit at the end fails the script if any does.
# Output: dist\KnowYourRole-v<version>-win-x64.zip and dist\SHA256SUMS.txt (LF line ending)
param(
    [string]$Version = "0.2.0"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
# the words no file name or text in the release may contain are kept out of the repository, in the local file
# .git\info\banned-words.txt (one per line, # comments, re: lines are regular expressions); no list, no release
$listFile = Join-Path (git -C $root rev-parse --absolute-git-dir) "info\banned-words.txt"
if (-not (Test-Path $listFile)) { throw "no word list at $listFile: the release audit cannot run, so nothing is packaged" }
$bannedPatterns = @(Get-Content $listFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith("#") } |
    ForEach-Object { if ($_.StartsWith("re:")) { $_.Substring(3) } else { [regex]::Escape($_) } })
if ($bannedPatterns.Count -eq 0) { throw "the word list $listFile is empty" }
$banned = $bannedPatterns -join '|'
$name = "KnowYourRole-v$Version-win-x64"
$dist = Join-Path $root "dist"
$stage = Join-Path $dist $name
$files = Join-Path $stage "KnowYourRole-files"
$runtime = Join-Path $files "runtime"
$tmp = Join-Path $dist "_launcher"

foreach ($d in $stage, $tmp) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
New-Item -ItemType Directory -Force $stage | Out-Null

function Invoke-Step([string]$what, [scriptblock]$cmd) {
    Write-Host "== $what"
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)" }
}

Invoke-Step "publish the game runtime (no recompiled code)" {
    dotnet publish (Join-Path $root "KnowYourRole.csproj") -c Release -r win-x64 --self-contained true -p:Launcher=true `
        -p:Version=$Version -o $runtime -v minimal
}
Invoke-Step "publish KnowYourRole.exe (setup + launcher, one file)" {
    dotnet publish (Join-Path $root "Launcher\KnowYourRole.Launcher.csproj") -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none -p:Version=$Version -o $tmp -v minimal
}
# content of referenced projects the one-file launcher does not use: the runtime's controller mapping files (shipped in
# runtime\), the recompiler's runtimeconfig and its library signatures (only its autoconfigure/sweep modes read them;
# the player's build runs from the port's function maps)
foreach ($x in "gamecontrollerdb.txt", "gamecontrollerdb.LICENSE.txt", "recompone.runtimeconfig.json", "AutoConfigure") {
    $p = Join-Path $tmp $x
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
$extra = Get-ChildItem $tmp | Where-Object { $_.Name -ne "KnowYourRole.exe" }
if ($extra) { throw "the launcher publish left more than one file: $($extra.Name -join ', ')" }
Move-Item (Join-Path $tmp "KnowYourRole.exe") (Join-Path $stage "KnowYourRole.exe")
Remove-Item $tmp -Recurse -Force

$config = Join-Path $runtime "port\config"
New-Item -ItemType Directory -Force $config | Out-Null
Copy-Item (Join-Path $root "config\KnowYourRole.json") $config
Copy-Item (Join-Path $root "config\funcmaps") $config -Recurse
Copy-Item (Join-Path $root "tools\release-readme.txt") (Join-Path $stage "README.txt")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $files "LICENSE.txt")
Copy-Item (Join-Path $root "tools\third-party-notices.txt") (Join-Path $files "THIRD-PARTY-NOTICES.txt")
# a note in the files folder for the curious
Set-Content (Join-Path $files "START KnowYourRole.exe ONE FOLDER UP.txt") `
    "These are the game's own files. Start KnowYourRole.exe in the folder above this one." -Encoding utf8

# --- audit ------------------------------------------------------------------------------------------------------------
# nothing from the disc and nothing built from it may ship
$forbidden = '\.(bin|cue|iso|chd|img|pac|sav|mcr|str|xa|png)$|^Recompiled\.|^SLUS|^settings\.json$|^interface\.ini$|^build\.json$'
$bad = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -match $forbidden }
if ($bad) { throw "game content in the release: $($bad.FullName -join ', ')" }
# an OpenGL driver file in the program folder would replace the player's own driver (Windows loads the program
# folder's copy first)
$driver = '^(opengl32|libgallium_wgl)\.dll$'
$gl = Get-ChildItem $stage -Recurse -File | Where-Object { $_.Name -match $driver }
if ($gl) { throw "an OpenGL driver file in the release: $($gl.FullName -join ', ')" }
$named = Get-ChildItem $stage -Recurse | Where-Object { $_.Name -match $banned }
if ($named) { throw "a banned word in a file name in the release: $($named.FullName -join ', ')" }
foreach ($t in Get-ChildItem $stage -Recurse -File -Include *.txt, *.json, *.md, *.ini, *.xml, *.config) {
    if ((Get-Content $t.FullName -Raw) -match $banned) { throw "a banned word in $($t.FullName): '$($Matches[0])'" }
}
# the top of the folder: the program, its readme and one folder with everything else
$top = Get-ChildItem $stage | ForEach-Object { $_.Name } | Sort-Object
$expected = @("KnowYourRole.exe", "KnowYourRole-files", "README.txt") | Sort-Object
if (Compare-Object $top $expected) { throw "unexpected files at the top of the release: $($top -join ', ')" }
$inner = Get-ChildItem $files | ForEach-Object { $_.Name } | Sort-Object
$expectedInner = @("LICENSE.txt", "runtime", "START KnowYourRole.exe ONE FOLDER UP.txt", "THIRD-PARTY-NOTICES.txt") | Sort-Object
if (Compare-Object $inner $expectedInner) { throw "unexpected files in KnowYourRole-files: $($inner -join ', ')" }
foreach ($need in "KnowYourRole.Game.exe", "RecompOne.Runtime.dll", "gamecontrollerdb.txt", "gamecontrollerdb.LICENSE.txt",
        "SDL2.dll", "glfw3.dll", "soft_oal.dll", "cimgui.dll", "port\config\KnowYourRole.json") {
    if (-not (Get-ChildItem $runtime -Recurse -File | Where-Object { $_.FullName.EndsWith("\" + $need) })) {
        throw "runtime\$need is missing from the release"
    }
}
# no absolute paths of this PC, no user name in any text file
$user = $env:USERNAME
$texts = Get-ChildItem $stage -Recurse -File -Include *.txt, *.json, *.md, *.ini, *.xml, *.config
foreach ($f in $texts) {
    $t = Get-Content $f.FullName -Raw
    if ($t -match [regex]::Escape($env:USERPROFILE) -or ($user.Length -ge 3 -and $t -match "\b$([regex]::Escape($user))\b")) {
        throw "$($f.FullName) contains a path or name of this PC"
    }
}

# the zip keeps one name across versions, so the README can link to the latest release's download directly; the
# version is in the folder inside it and in the release title
$zipName = "KnowYourRole-win-x64.zip"
$zip = Join-Path $dist $zipName
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
# the zip itself: the same audit on its entries
Add-Type -AssemblyName System.IO.Compression.FileSystem
$entries = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $zbad = $entries.Entries | Where-Object { $_.Name -match $forbidden -or $_.Name -match $driver -or $_.FullName -match $banned }
    if ($zbad) { throw "game content in the zip: $($zbad.FullName -join ', ')" }
    $big = $entries.Entries | Where-Object { $_.Length -gt 1MB -and $_.Name -notmatch '\.(dll|exe)$' }
    if ($big) { throw "unexpected large files in the zip: $($big.FullName -join ', ')" }
    Write-Host "== zip audit: $($entries.Entries.Count) entries, no game content"
}
finally { $entries.Dispose() }

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $dist "SHA256SUMS.txt"), "$hash  $zipName`n")
Write-Host "== $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB) sha256 $hash"
