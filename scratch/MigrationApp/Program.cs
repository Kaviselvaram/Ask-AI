using System;
using Microsoft.Data.SqlClient;
using dotenv.net;

class RAGAdvancedMigration
{
    static void Main()
    {
        DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { "/Users/kaviselvaramkathirvel/Desktop/AIstart/backend/.env" }));
        string connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")! + ";Pooling=false";
        
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
