using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SysLog.Service.Interfaces;

namespace SysLog.Repository.Services;

public class SqlServerBackup : IBackup
{
    private readonly   ILogger<SqlServerBackup> _logger;
    private readonly IConfiguration _configuration;

    public SqlServerBackup(IConfiguration configuration, ILogger<SqlServerBackup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task BackupAsync()
    {
        
        string? backupPath = _configuration["Database:BackupPath"];;

        if (!Directory.Exists(backupPath))
        {
            Directory.CreateDirectory(backupPath);
        }
        
        string? databaseName = _configuration["Database:Name"];; 
        string? connectionString =
            _configuration["Database:ConnectionStrings:DefaultConnection"];
        
        
        

        string databaseCheckQuery =  
            @$"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SysLogDb') BEGIN CREATE DATABASE SysLogDb; PRINT 'Base de datos SysLogDb creada.'; END ELSE BEGIN PRINT 'La base de datos SysLogDb ya existe.'; END; USE SysLogDb;";
        
                                         
        
        
        try
        {
            using var connection = new SqlConnection(connectionString);
            
            await connection.OpenAsync();
            
            using StreamWriter writter = new StreamWriter(Path.Combine(backupPath, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{databaseName}.sql"));
            await writter.WriteLineAsync(databaseCheckQuery);
            
            DataTable tables = await connection.GetSchemaAsync("Tables");
            string tableNameChange = string.Empty;
            
            foreach(DataRow row in tables.Rows)
            {
                string tableName = row["TABLE_NAME"].ToString();
                
                string tableCheckQuery =
                    $@"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}')BEGIN CREATE TABLE {tableName} (LogId INT IDENTITY(1,1) PRIMARY KEY,Type NVARCHAR(50) NOT NULL,Action NVARCHAR(100) NOT NULL,Interface NVARCHAR(100) NOT NULL,Protocol NVARCHAR(50) NOT NULL,IpOut NVARCHAR(45) NOT NULL,IpDestiny NVARCHAR(45) NOT NULL,Signature NVARCHAR(200) NULL,DateTime DATETIME NOT NULL);PRINT 'Tabla Logs creada.';END ELSE BEGIN PRINT 'La tabla Logs ya existe.';END;";
                await writter.WriteLineAsync(tableCheckQuery);
                
                string query = $"SELECT * FROM {tableName}";
                using var readTableCommand = new SqlCommand(query, connection);
                
                using var reader = await readTableCommand.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    List<string> values = new List<string>();
                    List<string> columnNames = new List<string>();
                    
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        values.Add($"'{reader.GetValue(i).ToString().Replace("'","''")}'");
                        columnNames.Add(reader.GetName(i).ToString());
                    }
                    
                    string line = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", values)})";
                    await writter.WriteLineAsync(line);
                }
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
        


    }
}