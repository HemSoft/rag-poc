#!/usr/bin/env pwsh

Write-Host "🔄 Restarting RAG POC Application..." -ForegroundColor Green

# Stop any running dotnet processes
$processes = Get-Process -Name "RagPoc" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "🛑 Stopping existing application..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Navigate to source directory
Set-Location "c:\Users\franz\GitHub\rag-poc\src"

# Build the project
Write-Host "🔨 Building with updated crawler logic..." -ForegroundColor Green
dotnet build --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host "🚀 Starting application..." -ForegroundColor Green
    Write-Host "💡 Try the same Microsoft Learn URL again to see the improved crawling!" -ForegroundColor Cyan
    Write-Host ""
    
    # Start the application
    dotnet run
} else {
    Write-Host "❌ Build failed. Please check the errors above." -ForegroundColor Red
}
