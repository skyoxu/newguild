param(
  [string]$GodotBin = $env:GODOT_BIN,
  [string]$Preset = 'Windows Desktop',
  [string]$Output = 'build/Game.exe'
)

$ErrorActionPreference = 'Stop'
if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
  Write-Error "GODOT_BIN is not set or file not found. Pass -GodotBin or set env var."
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
Write-Host "Exporting $Preset to $Output"
# Backend detection message
if (Test-Path "$PSScriptRoot/../../addons/godot-sqlite") {
  Write-Host "Detected addons/godot-sqlite plugin: export will prefer plugin backend."
} else {
  Write-Host "No addons/godot-sqlite found: export relies on Microsoft.Data.Sqlite managed fallback. If runtime missing native e_sqlite3, add SQLitePCLRaw.bundle_e_sqlite3."
}
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
  $env:GODOT_DOTNET_CLI = (Get-Command dotnet).Path
  Write-Host "GODOT_DOTNET_CLI=$env:GODOT_DOTNET_CLI"
}

# Prepare log dir and capture Godot output
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$dest = Join-Path $PSScriptRoot ("../../logs/ci/$ts/export")
New-Item -ItemType Directory -Force -Path $dest | Out-Null
$glog = Join-Path $dest 'godot_export.log'

function Invoke-Export([string]$mode) {
  Write-Host "Invoking export: $mode"
  $out = Join-Path $dest ("godot_export.$mode.out.log")
  $err = Join-Path $dest ("godot_export.$mode.err.log")
  $args = @('--headless','--verbose','--path','.', "--export-$mode", $Preset, $Output)
  $p = Start-Process -FilePath $GodotBin -ArgumentList $args -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
  $ok = $p.WaitForExit(600000)
  if (-not $ok) { Write-Warning 'Godot export timed out; killing process'; Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  Add-Content -Encoding UTF8 -Path $glog -Value ("=== export-$mode @ " + (Get-Date).ToString('o'))
  if (Test-Path $out) { Get-Content $out -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  if (Test-Path $err) { Get-Content $err -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  return $p.ExitCode
}

$exitCode = Invoke-Export 'release'
if ($exitCode -ne 0) {
  Write-Warning "Export-release failed with exit code $exitCode. Trying export-debug as fallback."
  $exitCode = Invoke-Export 'debug'
  if ($exitCode -ne 0) {
    Write-Warning "Both release and debug export failed, trying export-pack as fallback."
    $pck = ($Output -replace '\.exe$','.pck')
    $out = Join-Path $dest ("godot_export.pack.out.log")
    $err = Join-Path $dest ("godot_export.pack.err.log")
    $args = @('--headless','--verbose','--path','.', '--export-pack', $Preset, $pck)
    $p = Start-Process -FilePath $GodotBin -ArgumentList $args -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
    $ok = $p.WaitForExit(600000)
    if (-not $ok) { Write-Warning 'Godot export-pack timed out'; Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    Add-Content -Encoding UTF8 -Path $glog -Value ("=== export-pack @ " + (Get-Date).ToString('o'))
    if (Test-Path $out) { Get-Content $out -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
    if (Test-Path $err) { Get-Content $err -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
    $exitCode = $p.ExitCode
    if ($exitCode -eq 0) {
      Write-Warning "EXE export failed but PCK fallback succeeded: $pck"
    } else {
      Write-Error "Export failed (release & debug & pack) with exit code $exitCode. See log: $glog"
    }
  }
}

# Collect artifacts
if (Test-Path $Output) { Copy-Item -Force $Output $dest 2>$null }
$maybePck = ($Output -replace '\.exe$','.pck')
if (Test-Path $maybePck) { Copy-Item -Force $maybePck $dest 2>$null }
if (Test-Path $glog) { Write-Host "--- godot_export.log (tail) ---"; Get-Content $glog -Tail 200 }
Write-Host "Export artifacts copied to $dest (log: $glog)"
exit $exitCode
