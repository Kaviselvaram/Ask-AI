using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class Program
{
    static async Task Main(string[] args)
    {
        string connectionString = "Server=tcp:aistart-sql-server.database.windows.net,1433;Initial Catalog=AIChatDB;Persist Security Info=False;User ID=kaviselvaram;Password=Vk642004@kavi;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        
        using SqlConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        string deleteSql = @"
            DELETE FROM GraphRelationships WHERE DocumentId IN (SELECT Id FROM Documents WHERE FileName LIKE '%.txt');
            DELETE FROM DocumentChunkMapping WHERE DocumentId IN (SELECT Id FROM Documents WHERE FileName LIKE '%.txt');
            DELETE FROM Documents WHERE FileName LIKE '%.txt';
        ";
        
        using SqlCommand deleteCmd = new SqlCommand(deleteSql, connection);
        int rows = await deleteCmd.ExecuteNonQueryAsync();
        
        Console.WriteLine($"Deleted rows related to .txt documents from the database.");
    }
}
