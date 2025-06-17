$connectionString = "Server=.;Database=RagPocDb;Integrated Security=true;TrustServerCertificate=true;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()

Write-Host "=== Documents in Database ==="
$command = $connection.CreateCommand()
$command.CommandText = "SELECT Id, FileName, FileType, CreatedAt FROM Documents"
$reader = $command.ExecuteReader()
while($reader.Read()) {
    Write-Host "ID: $($reader['Id']), File: $($reader['FileName']), Type: $($reader['FileType']), Created: $($reader['CreatedAt'])"
}
$reader.Close()

Write-Host "`n=== Chunk Counts per Document ==="
$command.CommandText = "SELECT d.FileName, COUNT(dc.Id) as ChunkCount FROM Documents d LEFT JOIN DocumentChunks dc ON d.Id = dc.DocumentId GROUP BY d.Id, d.FileName"
$reader = $command.ExecuteReader()
while($reader.Read()) {
    Write-Host "$($reader['FileName']): $($reader['ChunkCount']) chunks"
}
$reader.Close()

Write-Host "`n=== Sample Chunk Content ==="
$command.CommandText = "SELECT TOP 3 ChunkText, LEN(ChunkText) as Length FROM DocumentChunks ORDER By Id"
$reader = $command.ExecuteReader()
$i = 1
while($reader.Read()) {
    Write-Host "Chunk $i (Length: $($reader['Length'])):"
    Write-Host $reader['ChunkText'].Substring(0, [Math]::Min(200, $reader['ChunkText'].Length)) + "..."
    Write-Host ""
    $i++
}
$reader.Close()

$connection.Close()
