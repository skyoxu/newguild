param(
  [string]$GodotBin = $env:GODOT_BIN,
  [string]$Preset = 'Windows Desktop',
  [string]$Output = 'build/Game.exe'
)

 $ErrorActionPreference = 'Stop'

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
New-Item -ItemType File -Force -Path $glog | Out-Null
Write-Host ("Log file: " + $glog)

if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
  $msg = "GODOT_BIN is not set or file not found: '$GodotBin'"
  Add-Content -Encoding UTF8 -Path $glog -Value $msg
  Write-Error $msg
}

# Resolve export preset name from export_presets.cfg to avoid 'Invalid export preset name'
function Resolve-Preset([string]$requested) {
  $cfgCandidates = @('export_presets.cfg','Game.Godot/export_presets.cfg')
  $names = @()
  foreach ($c in $cfgCandidates) {
    if (Test-Path $c) {
      try {
        $lines = Get-Content $c -ErrorAction SilentlyContinue
        foreach ($ln in $lines) {
          if ($ln -match '^\s*name\s*=\s*"([^"]+)"') { $names += $Matches[1] }
        }
      } catch {}
    }
  }
  if ($names.Count -eq 0) { return $requested }
  # exact match
  if ($names -contains $requested) { return $requested }
  # common alias mapping: 'Windows' -> first name containing 'Windows'
  $win = $names | Where-Object { $_ -match '(?i)windows' } | Select-Object -First 1
  if ($requested -eq 'Windows' -and $win) { return $win }
  # prefer 'Windows Desktop' if present
  $wd = $names | Where-Object { $_ -eq 'Windows Desktop' } | Select-Object -First 1
  if ($wd) { return $wd }
  # fallback to first preset
  return ($names | Select-Object -First 1)
}

# Quote arguments for Start-Process -ArgumentList to preserve spaces
function Quote-Arg([string]$a) {
  if ($null -eq $a) { return '""' }
  if ($a -match '^[A-Za-z0-9_./\\:-]+$') { return $a }
  $q = '"' + ($a -replace '"', '\"') + '"'
  return $q
}

function Invoke-BuildSolutions() {
  Write-Host "Pre-building C# solutions via Godot (--build-solutions)"
  $out = Join-Path $dest ("godot_build_solutions.out.log")
  $err = Join-Path $dest ("godot_build_solutions.err.log")
  $args = @('--headless','--verbose','--path','.', '--build-solutions', '--quit')
  $argStr = ($args | ForEach-Object { Quote-Arg $_ }) -join ' '
  try {
    $p = Start-Process -FilePath $GodotBin -ArgumentList $argStr -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
  } catch {
    Add-Content -Encoding UTF8 -Path $glog -Value ("Start-Process failed (build-solutions): " + $_.Exception.Message)
    throw
  }
  $ok = $p.WaitForExit(600000)
  if (-not $ok) { Write-Warning 'Godot build-solutions timed out; killing process'; Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  Add-Content -Encoding UTF8 -Path $glog -Value ("=== build-solutions @ " + (Get-Date).ToString('o'))
  if (Test-Path $out) { Get-Content $out -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  if (Test-Path $err) { Get-Content $err -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  # Log quick check for built assembly
  try {
    $binDir = Join-Path $PSScriptRoot '../../.godot/mono/temp/bin'
    if (Test-Path $binDir) {
      $dlls = Get-ChildItem -Path $binDir -Filter '*.dll' -ErrorAction SilentlyContinue
      Add-Content -Encoding UTF8 -Path $glog -Value ("Built DLLs: " + ($dlls | Select-Object -ExpandProperty Name | Sort-Object | Out-String))
    } else {
      Add-Content -Encoding UTF8 -Path $glog -Value 'Warning: .godot/mono/temp/bin not found after build-solutions.'
    }
    # Try to capture MSBuild detailed log Godot writes under Roaming\Godot\mono\build_logs
    $blRoot = Join-Path $env:APPDATA 'Godot/mono/build_logs'
    if (Test-Path $blRoot) {
      $latest = Get-ChildItem -Directory $blRoot | Sort-Object LastWriteTime -Descending | Select-Object -First 1
      if ($latest) {
        $logPath = Join-Path $latest.FullName 'msbuild_log.txt'
        if (Test-Path $logPath) {
          $msbLocal = Join-Path $dest 'msbuild_log.txt'
          Copy-Item -Force $logPath $msbLocal -ErrorAction SilentlyContinue
          # Extract errors: match 'error <code>', 'CSxxxx', 'GD010x' (case-insensitive)
          try {
            $pattern = '(?i)\berror\b\s+[A-Z]?[0-9]{3,5}\b|CS[0-9]{4}\b|GD010[0-9]\b'
            $matches = Select-String -Path $logPath -Pattern $pattern -AllMatches -ErrorAction SilentlyContinue
            $outFile = Join-Path $dest 'msbuild_errors.txt'
            if ($matches -and $matches.Count -gt 0) {
              $lines = $matches | Select-Object -ExpandProperty Line
              $lines | Set-Content -Path $outFile -Encoding UTF8
            } else {
              'No errors matched (pattern: error|CSxxxx|GD010x).' | Set-Content -Path $outFile -Encoding UTF8
            }
          } catch {
            ('Failed to extract msbuild errors: ' + $_.Exception.Message) | Set-Content -Path (Join-Path $dest 'msbuild_errors.txt') -Encoding UTF8
          }
        }
      }
    }
  } catch {}
  return $p.ExitCode
}

function Invoke-Export([string]$mode) {
  Write-Host "Invoking export: $mode"
  $out = Join-Path $dest ("godot_export.$mode.out.log")
  $err = Join-Path $dest ("godot_export.$mode.err.log")
  $resolved = Resolve-Preset $Preset
  Add-Content -Encoding UTF8 -Path $glog -Value ("Using preset: '" + $resolved + "' output: '" + $Output + "'")
  if ($resolved -ne $Preset) { Add-Content -Encoding UTF8 -Path $glog -Value ("Requested preset '" + $Preset + "' resolved to '" + $resolved + "'") }
  $args = @('--headless','--verbose','--path','.', "--export-$mode", $resolved, $Output)
  $argStr = ($args | ForEach-Object { Quote-Arg $_ }) -join ' '
  try {
    $p = Start-Process -FilePath $GodotBin -ArgumentList $argStr -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
  } catch {
    Add-Content -Encoding UTF8 -Path $glog -Value ("Start-Process failed (export-"+$mode+"): " + $_.Exception.Message)
    throw
  }
  $ok = $p.WaitForExit(600000)
  if (-not $ok) { Write-Warning 'Godot export timed out; killing process'; Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
  Add-Content -Encoding UTF8 -Path $glog -Value ("=== export-$mode @ " + (Get-Date).ToString('o'))
  if (Test-Path $out) { Get-Content $out -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  if (Test-Path $err) { Get-Content $err -ErrorAction SilentlyContinue | Add-Content -Encoding UTF8 -Path $glog }
  return $p.ExitCode
}

$buildCode = Invoke-BuildSolutions
if ($buildCode -ne 0) {
  Write-Error "Godot --build-solutions failed with exit code $buildCode. See log: $glog"
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
    $resolved = Resolve-Preset $Preset
    Add-Content -Encoding UTF8 -Path $glog -Value ("Using preset (pack): '" + $resolved + "' output: '" + $pck + "'")
    $args = @('--headless','--verbose','--path','.', '--export-pack', $resolved, $pck)
    $argStr = ($args | ForEach-Object { Quote-Arg $_ }) -join ' '
    $p = Start-Process -FilePath $GodotBin -ArgumentList $argStr -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
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

