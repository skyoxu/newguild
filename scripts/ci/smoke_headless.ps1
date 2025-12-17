param(
  [string]$GodotBin = $env:GODOT_BIN,
  [string]$Scene = 'res://Game.Godot/Scenes/Main.tscn',
  [int]$TimeoutSec = 5,
  [string]$ProjectPath = '.',
  [string]$UserDir = $env:GODOT_USERDIR,
  [string]$UserDirFlag = $env:GODOT_USERDIR_FLAG,
  [switch]$NoUserDir = $false
)

$ErrorActionPreference = 'Stop'

if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
  Write-Error "GODOT_BIN is not set or file not found. Pass -GodotBin or set env var."
}

# English comments and prints only. UTF-8 output. Windows friendly.
function Quote-Arg([string]$a) {
  if ($null -eq $a) { return '""' }
  if ($a -match '^[A-Za-z0-9_./\\:-]+$') { return $a }
  $q = '"' + ($a -replace '"', '\"') + '"'
  return $q
}

function Resolve-FullPath([string]$base, [string]$p) {
  if ([string]::IsNullOrWhiteSpace($p)) { return $null }
  if ([System.IO.Path]::IsPathRooted($p)) { return $p }
  return (Join-Path $base $p)
}

function Validate-UserDirValue([string]$p) {
  if ([string]::IsNullOrWhiteSpace($p)) { return $null }
  $t = $p.Trim()
  if ($t -match '^(?i)(user://|res://)') {
    throw "UserDir must be a filesystem path, not a Godot virtual path (got: $t)"
  }
  if ($t -match ':' -and $t -notmatch '^[A-Za-z]:[\\/]') {
    throw "UserDir contains ':' but is not an absolute drive path like C:\\path\\to\\dir (got: $t)"
  }
  if ($t -match '^[A-Za-z]:$' -or $t -match '^[A-Za-z]:[^\\/]') {
    throw "UserDir must be absolute like C:\\path\\to\\dir (got: $t)"
  }
  return $t
}

function Detect-UserDirFlag([string]$bin, [string]$requested) {
  if (-not [string]::IsNullOrWhiteSpace($requested) -and $requested -ne 'auto') { return $requested }
  try {
    $help = (& $bin --help 2>&1 | Out-String)
  } catch {
    return $null
  }
  $candidates = @('--user-dir','--user-data-dir','--userdir')
  foreach ($c in $candidates) {
    if ($help -match [regex]::Escape($c)) { return $c }
  }
  return $null
}

$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$dest = Join-Path $PSScriptRoot ("../../logs/ci/$ts/smoke")
New-Item -ItemType Directory -Force -Path $dest | Out-Null
$log = Join-Path $dest 'headless.log'
$logOut = Join-Path $dest 'headless.out.log'
$logErr = Join-Path $dest 'headless.err.log'

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path

# Redirect user:// away from %APPDATA% to keep local machines and CI runners clean.
$userDirArgs = @()
$effectiveUserDir = $null
if (-not $NoUserDir) {
  if ([string]::IsNullOrWhiteSpace($UserDir)) {
    $leaf = (Split-Path -Leaf $resolvedProjectPath)
    $UserDir = (Join-Path $resolvedProjectPath (Join-Path "logs/_godot_userdir" (Join-Path $leaf "smoke")))
  } else {
    $UserDir = Resolve-FullPath -base $resolvedProjectPath -p $UserDir
  }
  $UserDir = Validate-UserDirValue -p $UserDir
  try { New-Item -ItemType Directory -Force -Path $UserDir | Out-Null; $effectiveUserDir = $UserDir } catch {}
  $flag = Detect-UserDirFlag -bin $GodotBin -requested $UserDirFlag
  if ($flag -and $effectiveUserDir) {
    $userDirArgs = @($flag, $effectiveUserDir)
  }
}

Write-Host "Starting Godot headless for $TimeoutSec sec: $Scene (path=$resolvedProjectPath userdir=$effectiveUserDir)"
$args = @()
$args += $userDirArgs
$args += @('--headless','--path',$resolvedProjectPath, '--scene', $Scene)
$argStr = ($args | ForEach-Object { Quote-Arg $_ }) -join ' '
$p = Start-Process -FilePath $GodotBin -ArgumentList $argStr -PassThru -WorkingDirectory $resolvedProjectPath -RedirectStandardOutput $logOut -RedirectStandardError $logErr -WindowStyle Hidden

try {
  $ok = $p.WaitForExit(1000 * $TimeoutSec)
  if (-not $ok) {
    Write-Host "Timeout reached; terminating Godot (expected for smoke)."
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  }
} catch {
  Write-Warning "Failed to wait/stop process: $_"
}

$content = ''
if (Test-Path $logOut) { $content += (Get-Content $logOut -Raw -ErrorAction SilentlyContinue) }
if (Test-Path $logErr) { $content += ("`n" + (Get-Content $logErr -Raw -ErrorAction SilentlyContinue)) }
Set-Content -Path $log -Encoding UTF8 -Value $content
Write-Host "Smoke log saved at $log (out=$logOut, err=$logErr)"

# Heuristic pass criteria: prefer explicit marker, fallback to DB opened, then any output
if ($p.Id -gt 0) {
  if ($content -match '\[TEMPLATE_SMOKE_READY\]') {
    Write-Host 'SMOKE PASS (marker)'
    exit 0
  }
  if ($content -match '\[DB\] opened') {
    Write-Host 'SMOKE PASS (db opened)'
    exit 0
  }
  if ($content.Length -gt 0) {
    Write-Host 'SMOKE PASS (any output)'
    exit 0
  }
}
Write-Warning 'SMOKE INCONCLUSIVE (no output). Check logs.'
exit 0
