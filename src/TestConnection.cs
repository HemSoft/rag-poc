using Microsoft.Data.SqlClient;
using System;

namespace RagPoc.Test
{
    class TestConnection
    {
        public static void TestSqlConnection()
        {
            var connectionStrings = new[]
            {
                "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10;",
                "Server=.\\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10;",
                "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10;",
                "Server=.;Database=master;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10;"
            };

            Console.WriteLine("🔍 Testing SQL Server connections with Windows authentication...\n");

            foreach (var connStr in connectionStrings)
            {
                try
                {
                    var builder = new SqlConnectionStringBuilder(connStr);
                    Console.Write($"Testing {builder.DataSource}... ");
                    
                    using var connection = new SqlConnection(connStr);
                    connection.Open();
                    
                    using var command = new SqlCommand("SELECT @@VERSION", connection);
                    var version = command.ExecuteScalar()?.ToString();
                    
                    Console.WriteLine($"✅ SUCCESS!");
                    Console.WriteLine($"   Version: {version?.Split('\n')[0]}");
                    Console.WriteLine($"   Connection String: {connStr}");
                    Console.WriteLine();
                    
                    connection.Close();
                    return; // Stop at first successful connection
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ FAILED: {ex.Message}");
                }
            }
            
            Console.WriteLine("\nNo connections succeeded. Please:");
            Console.WriteLine("1. Install SQL Server LocalDB: https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb");
            Console.WriteLine("2. Or install SQL Server Express: https://www.microsoft.com/sql-server/sql-server-downloads");
            Console.WriteLine("3. Ensure Windows authentication is enabled");
        }
    }
}
