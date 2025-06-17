#!/usr/bin/env pwsh

# Stop any running dotnet processes
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force

# Navigate to source directory
Set-Location "c:\Users\franz\GitHub\rag-poc\src"

# Build the project
Write-Host "Building project..." -ForegroundColor Green
dotnet build --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Test a simple website crawl
    Write-Host "Testing website crawl functionality..." -ForegroundColor Yellow
    
    # You can run specific tests here when ready
    Write-Host "Ready to test. You can now run 'dotnet run' and select option 2 to test the Abot crawler." -ForegroundColor Cyan
} else {
    Write-Host "Build failed. Please check the errors above." -ForegroundColor Red
}
