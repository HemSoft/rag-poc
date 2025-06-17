# PowerShell script to setup SQL Server for RAG POC with Windows Authentication

Write-Host "🔍 Checking SQL Server installations..." -ForegroundColor Yellow

# Check for SQL Server LocalDB
$localDB = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server Local DB\Installed Versions\*" -ErrorAction SilentlyContinue
if ($localDB) {
    Write-Host "✅ SQL Server LocalDB found: $($localDB.Version)" -ForegroundColor Green
    
    # List LocalDB instances
    Write-Host "📋 LocalDB instances:" -ForegroundColor Cyan
    & sqllocaldb info
    
    # Start default instance if not running
    $defaultInstance = & sqllocaldb info MSSQLLocalDB 2>$null
    if ($defaultInstance -match "State: Running") {
        Write-Host "✅ MSSQLLocalDB instance is running" -ForegroundColor Green
    } else {
        Write-Host "🚀 Starting MSSQLLocalDB instance..." -ForegroundColor Yellow
        & sqllocaldb start MSSQLLocalDB
    }
} else {
    Write-Host "❌ SQL Server LocalDB not found" -ForegroundColor Red
}

# Check for SQL Server Express
$sqlExpress = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
if ($sqlExpress) {
    Write-Host "✅ SQL Server Express found: $($sqlExpress.Status)" -ForegroundColor Green
    if ($sqlExpress.Status -ne "Running") {
        Write-Host "🚀 Starting SQL Server Express..." -ForegroundColor Yellow
        Start-Service -Name "MSSQL`$SQLEXPRESS"
    }
} else {
    Write-Host "❌ SQL Server Express not found" -ForegroundColor Red
}

# Check for full SQL Server
$sqlServer = Get-Service -Name "MSSQLSERVER" -ErrorAction SilentlyContinue
if ($sqlServer) {
    Write-Host "✅ SQL Server (full) found: $($sqlServer.Status)" -ForegroundColor Green
    if ($sqlServer.Status -ne "Running") {
        Write-Host "🚀 Starting SQL Server..." -ForegroundColor Yellow
        Start-Service -Name "MSSQLSERVER"
    }
} else {
    Write-Host "ℹ️  Full SQL Server not found (this is optional)" -ForegroundColor Blue
}

Write-Host "`n🔧 Connection string options:" -ForegroundColor Cyan
Write-Host "1. LocalDB: Server=(localdb)\MSSQLLocalDB;Integrated Security=true;" -ForegroundColor White
Write-Host "2. Express: Server=.\SQLEXPRESS;Integrated Security=true;" -ForegroundColor White
Write-Host "3. Default: Server=localhost;Integrated Security=true;" -ForegroundColor White

Write-Host "`n✨ Setup complete! The RAG POC application will try these connections automatically." -ForegroundColor Green
