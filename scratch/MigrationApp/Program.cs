using System;
using Microsoft.Data.SqlClient;

class RAGAdvancedMigration
{
    static void Main()
    {
        string connectionString = "Server=tcp:aistart-sql-server.database.windows.net,1433;Initial Catalog=AIChatDB;Persist Security Info=False;User ID=kaviselvaram;Password=Vk642004@kavi;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        using SqlConnection conn = new SqlConnection(connectionString);
        conn.Open();

        string sql = @"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunkMapping') AND name = 'PageNumber')
            BEGIN
                ALTER TABLE DocumentChunkMapping ADD PageNumber INT NULL;
            END

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GraphEntities' and xtype='U')
            BEGIN
                CREATE TABLE GraphEntities (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    Type NVARCHAR(100) NOT NULL
                );
            END

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GraphRelationships' and xtype='U')
            BEGIN
                CREATE TABLE GraphRelationships (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SourceEntityId INT NOT NULL,
                    TargetEntityId INT NOT NULL,
                    RelationType NVARCHAR(255) NOT NULL,
                    DocumentId INT NOT NULL,
                    FOREIGN KEY (SourceEntityId) REFERENCES GraphEntities(Id),
                    FOREIGN KEY (TargetEntityId) REFERENCES GraphEntities(Id),
                    FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
                );
            END
        ";

        using SqlCommand cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine("Database Schema Migration Complete.");
    }
}
