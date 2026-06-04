using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class RepairProgram
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== RAG Document Repair Script ===");
        
        string envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2) Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
        
        string connStr = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connStr))
        {
            Console.WriteLine("ERROR: SQL_CONNECTION_STRING not found in environment.");
            return;
        }

        string uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Uploads");
        if (!Directory.Exists(uploadsPath))
        {
            Console.WriteLine($"ERROR: Uploads directory not found at {uploadsPath}");
            return;
        }

        var diskFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(uploadsPath))
        {
            diskFiles.Add(Path.GetFileName(file));
        }
        
        var dbFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orphanedDbEntries = new List<(int Id, string FileName)>();

        using SqlConnection conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        
        string sql = "SELECT Id, FileName FROM Documents WHERE Status = 'Latest'";
        using SqlCommand cmd = new SqlCommand(sql, conn);
        using SqlDataReader reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string fileName = reader.GetString(1);
            dbFiles.Add(fileName);
            
            if (!diskFiles.Contains(fileName))
            {
                orphanedDbEntries.Add((id, fileName));
            }
        }
        
        var orphanedDiskFiles = new List<string>();
        foreach (var diskFile in diskFiles)
        {
            if (!dbFiles.Contains(diskFile))
            {
                orphanedDiskFiles.Add(diskFile);
            }
        }
        
        Console.WriteLine("\n--- DIAGNOSTIC RESULTS ---");
        Console.WriteLine($"Total DB 'Latest' Documents: {dbFiles.Count}");
        Console.WriteLine($"Total Disk Files in Uploads: {diskFiles.Count}");
        
        Console.WriteLine("\n[1] Orphaned Database Entries (In DB, missing from disk):");
        if (orphanedDbEntries.Count == 0) Console.WriteLine("    None.");
        foreach (var entry in orphanedDbEntries)
        {
            Console.WriteLine($"    - ID: {entry.Id} | File: {entry.FileName}");
        }
        
        Console.WriteLine("\n[2] Orphaned Disk Files (On disk, missing from DB):");
        if (orphanedDiskFiles.Count == 0) Console.WriteLine("    None.");
        foreach (var file in orphanedDiskFiles)
        {
            Console.WriteLine($"    - File: {file}");
        }
        
        Console.WriteLine("\nAction required: If there are orphaned database entries, you should manually delete them from the database, or upload the missing files. If there are orphaned disk files, you can safely delete them from the Uploads folder to save space.");
    }
}
