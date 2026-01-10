param(
  [string]$ProjectPath = 'Tests.Godot',
  [string]$RuntimeDir = 'Game.Godot'
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\\..')).Path
Set-Location $root

# Delegate to the Python hard gate to avoid drift (no copy fallback).
$cmd = @(
  'py', '-3', 'scripts/python/prepare_gd_tests.py',
  '--project', $ProjectPath,
  '--runtime', $RuntimeDir
)

Write-Host ("Running: " + ($cmd -join ' '))
& $cmd[0] $cmd[1] $cmd[2] $cmd[3] $cmd[4] $cmd[5] $cmd[6]
if ($LASTEXITCODE -ne 0) {
  throw "prepare_gd_tests failed rc=$LASTEXITCODE"
}
