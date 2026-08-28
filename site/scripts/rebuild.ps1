# One-click PowerShell script to re-extract turret & ammunition stats from XML files into the web app dataset

$scriptPath = Join-Path $PSScriptRoot "extract_turrets.py"
Write-Host "Rebuilding Turret & Ammunition dataset from XML files..." -ForegroundColor Cyan

if (Get-Command python -ErrorAction SilentlyContinue) {
    python $scriptPath
} elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    python3 $scriptPath
} else {
    Write-Error "Python executable not found! Please install Python or ensure it is in your PATH."
    exit 1
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "Turret dataset successfully rebuilt!" -ForegroundColor Green
} else {
    Write-Error "Data extraction failed with exit code $LASTEXITCODE"
}
