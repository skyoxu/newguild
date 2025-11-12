Param(
  [string]$ProjectRoot = ".",
  [string]$GodotBin = $env:GODOT_BIN,
  [string]$GodotProject = $env:GODOT_PROJECT,
  [string]$OutDir = ""
)

# English comments and prints only. UTF-8 output. Windows friendly.
$ErrorActionPreference = 'SilentlyContinue'

function New-LogDir {
  Param([string]$BaseOut)
  if ([string]::IsNullOrWhiteSpace($BaseOut)) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $BaseOut = Join-Path -Path "logs/ci/$date/env" -ChildPath ''
  }
  New-Item -ItemType Directory -Force -Path $BaseOut | Out-Null
  return $BaseOut
}

function Test-ExeVersion {
  Param([string]$Exe, [string]$Args = "--version")
  try {
    $p = Start-Process -FilePath $Exe -ArgumentList $Args -NoNewWindow -RedirectStandardOutput "STDOUT.txt" -RedirectStandardError "STDERR.txt" -PassThru -Wait
    $out = Get-Content -Raw "STDOUT.txt"; $err = Get-Content -Raw "STDERR.txt"
    Remove-Item -ErrorAction SilentlyContinue "STDOUT.txt","STDERR.txt"
    return @{ ok = $true; code = $p.ExitCode; stdout = $out; stderr = $err }
  } catch {
    return @{ ok = $false; error = $_.Exception.Message }
  }
}

function Find-Godot {
  Param([string]$Hint)
  if (-not [string]::IsNullOrWhiteSpace($Hint) -and (Test-Path $Hint)) { return $Hint }
  $candidates = @(
    "Godot_v4.5.1-stable_win64.exe",
    "Godot_v4.5-stable_win64.exe",
    "Godot.exe"
  )
  foreach ($c in $candidates) {
    $p = Join-Path -Path $ProjectRoot -ChildPath $c
    if (Test-Path $p) { return $p }
  }
  return $null
}

$result = @{
  os = $env:OS
  machine = $env:COMPUTERNAME
  projectRoot = (Resolve-Path $ProjectRoot).Path
  checks = @{}
}

$OutDir = New-LogDir -BaseOut $OutDir

# 1) Git
$git = Test-Path (Get-Command git -ErrorAction SilentlyContinue).Path
$gitVer = $null
if ($git) { try { $gitVer = (git --version) } catch {} }
$result.checks.git = @{ present = $git; version = $gitVer }

# 2) .NET SDK 8+
$dotnet = Test-Path (Get-Command dotnet -ErrorAction SilentlyContinue).Path
$dotnetVerRaw = $null; $dotnetOk = $false
if ($dotnet) {
  try { $dotnetVerRaw = (dotnet --version).Trim(); $major = [int]($dotnetVerRaw.Split('.')[0]); $dotnetOk = ($major -ge 8) } catch {}
}
$result.checks.dotnet = @{ present = $dotnet; version = $dotnetVerRaw; ok = $dotnetOk }

# 3) Python (prefer py -3)
$pyLauncher = Test-Path (Get-Command py -ErrorAction SilentlyContinue).Path
$py3Ok = $false; $py3Ver = $null
if ($pyLauncher) {
  try { $py3Ver = (py -3 --version) -join ' '; if ($py3Ver) { $py3Ok = $true } } catch {}
} else {
  try { $py3Ver = (python --version) -join ' '; if ($py3Ver) { $py3Ok = $true } } catch {}
}
$result.checks.python = @{ launcher = $pyLauncher; present = $py3Ok; version = $py3Ver }

# 4) Godot
$godotPath = Find-Godot -Hint $GodotBin
$godotOk = $false; $godotVer = $null
if ($godotPath) {
  $gv = Test-ExeVersion -Exe $godotPath -Args "--version"
  if ($gv.ok) { $godotVer = ($gv.stdout + " " + $gv.stderr).Trim(); if ($godotVer -match "4\.5") { $godotOk = $true } }
}
$result.checks.godot = @{ path = $godotPath; version = $godotVer; ok = $godotOk }

# 5) project.godot
if ([string]::IsNullOrWhiteSpace($GodotProject)) { $GodotProject = Join-Path -Path $ProjectRoot -ChildPath 'project.godot' }
$projExists = Test-Path $GodotProject
$result.checks.project = @{ path = $GodotProject; exists = $projExists }

# 6) Recommended env vars
$result.checks.env = @{
  GODOT_BIN = $env:GODOT_BIN
  GODOT_PROJECT = $env:GODOT_PROJECT
  CI = $env:CI
}

# Status summarization
$issues = @()
if (-not $git) { $issues += "Git not found" }
if (-not $dotnetOk) { $issues += "Dotnet SDK 8+ not found or invalid: $dotnetVerRaw" }
if (-not $py3Ok) { $issues += "Python 3 launcher not found (py -3 / python)" }
if (-not $godotOk) { $issues += "Godot 4.5 not found or version mismatch (path=$godotPath; ver=$godotVer)" }
if (-not $projExists) { $issues += "project.godot not found at $GodotProject" }

$status = if ($issues.Count -eq 0) { 'ok' } elseif ($issues.Count -le 2) { 'warn' } else { 'fail' }
$result.status = $status
$result.issues = $issues
$result.generated = (Get-Date).ToString('o')

# Write outputs
$jsonPath = Join-Path $OutDir 'phase1-env-check.json'
$txtPath  = Join-Path $OutDir 'phase1-env-check.txt'

$result | ConvertTo-Json -Depth 8 | Out-File -Encoding utf8 $jsonPath
($result | Out-String) | Out-File -Encoding utf8 $txtPath

Write-Output ("ENV_CHECK status={0} issues={1} out={2}" -f $status, $issues.Count, $OutDir)
if ($status -eq 'fail') { exit 1 } else { exit 0 }

