using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using backend.Models;

namespace backend.Services;

public class VaultAnalysisService : IVaultAnalysisService
{
    public async Task<(string VaultContext, List<SourceInfo> AnalyzedSources)> BuildVaultContextAsync(SqlConnection connection, int chunksPerDocument = 3)
    {
        Console.WriteLine("VAULT ANALYSIS MODE ENABLED");
        
        var sources = new List<SourceInfo>();
        var vaultContextLines = new List<string>();
        
        string sql = @"
            WITH RankedChunks AS (
                SELECT 
                    d.Id as DocId, 
                    d.FileName, 
                    c.ChunkText, 
                    m.PageNumber,
                    ROW_NUMBER() OVER(PARTITION BY d.Id ORDER BY c.Id) as rn
                FROM Documents d
                JOIN DocumentChunkMapping m ON d.Id = m.DocumentId
                JOIN Chunks c ON m.ChunkId = c.Id
                WHERE d.Status = 'Latest'
            )
            SELECT DocId, FileName, ChunkText, PageNumber
            FROM RankedChunks
            WHERE rn <= @ChunksPerDoc
            ORDER BY DocId, rn;
        ";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ChunksPerDoc", chunksPerDocument);

        using var reader = await command.ExecuteReaderAsync();

        var documentChunks = new Dictionary<int, (string FileName, List<string> Chunks, List<int> Pages)>();

        while (await reader.ReadAsync())
        {
            int docId = reader.GetInt32(0);
            string fileName = reader.GetString(1);
            string content = reader.GetString(2);
            int pageNumber = reader.IsDBNull(3) ? 1 : reader.GetInt32(3);

            if (!documentChunks.ContainsKey(docId))
            {
                documentChunks[docId] = (fileName, new List<string>(), new List<int>());
                sources.Add(new SourceInfo
                {
                    ReferenceId = sources.Count + 1,
                    DocumentId = docId,
                    FileName = fileName,
                    DownloadUrl = $"/download/{docId}"
                });
            }

            documentChunks[docId].Chunks.Add($"-> {content}");
            if (!documentChunks[docId].Pages.Contains(pageNumber) && documentChunks[docId].Pages.Count < 5)
            {
                documentChunks[docId].Pages.Add(pageNumber);
            }
        }

        Console.WriteLine($"DOCUMENT COUNT: {documentChunks.Count}");
        Console.WriteLine($"DOCUMENTS ANALYZED: {documentChunks.Count}");
        Console.WriteLine("DOCUMENT NAMES:");
        
        foreach (var kvp in documentChunks)
        {
            Console.WriteLine($"- {kvp.Value.FileName}");
        }

        Console.WriteLine("CHUNKS PER DOCUMENT:");
        foreach (var kvp in documentChunks)
        {
            Console.WriteLine($"{kvp.Value.FileName}: {kvp.Value.Chunks.Count}");
            
            vaultContextLines.Add($"Document {kvp.Value.FileName}");
            vaultContextLines.AddRange(kvp.Value.Chunks);
            vaultContextLines.Add(""); // blank line
        }

        Console.WriteLine($"INSIGHT ENGINE DOCUMENT COUNT: {documentChunks.Count}");

        string vaultContext = string.Join("\n", vaultContextLines);
        
        return (vaultContext, sources);
    }
}
