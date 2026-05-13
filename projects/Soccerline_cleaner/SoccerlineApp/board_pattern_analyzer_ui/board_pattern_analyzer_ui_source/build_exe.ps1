$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

python -m PyInstaller `
  --onefile `
  --windowed `
  --clean `
  --name BoardPatternAnalyzer `
  --paths .. `
  --add-data "sample_topics.json;." `
  app.py

Write-Host ""
Write-Host "Build complete:"
Write-Host (Join-Path $ScriptDir "dist\BoardPatternAnalyzer.exe")

