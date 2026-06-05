using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace backend.Services;

public class WorkspaceCatalogBuilder : IWorkspaceCatalogBuilder
{
    public async Task<List<string>> GetAvailableDocumentsAsync(string connectionString)
    {
        var documents = new List<string>();
        
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        string sql = "SELECT FileName FROM Documents WHERE Status = 'Latest'";
        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            documents.Add(reader.GetString(0));
        }
        
        return documents;
    }
}
