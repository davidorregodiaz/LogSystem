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
    string? backupPath = _configuration["Database:BackupPath"];
    string? databaseName = _configuration["Database:Name"];
    string? connectionString = _configuration["Database:ConnectionStrings:DefaultConnection"];

    if (!Directory.Exists(backupPath))
    {
        Directory.CreateDirectory(backupPath!);
    }

    try
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        string backupFile = Path.Combine(backupPath, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{databaseName}.sql");
        await using StreamWriter writer = new StreamWriter(backupFile);

        // Obtener tablas
        var tableCommand = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
        using var tableReader = await tableCommand.ExecuteReaderAsync();

        var tableNames = new List<string>();
        while (await tableReader.ReadAsync())
        {
            tableNames.Add(tableReader.GetString(0));
        }
        tableReader.Close();

        foreach (var tableName in tableNames)
        {
            var colQuery = $@"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.is_nullable,
                    c.is_identity,
                    c.max_length,
                    c.precision,
                    c.scale
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE object_id = OBJECT_ID('{tableName}')";

            var colCommand = new SqlCommand(colQuery, connection);
            using var colReader = await colCommand.ExecuteReaderAsync();

            var columnDefs = new List<string>();
            var identityColumns = new HashSet<string>();

            while (await colReader.ReadAsync())
            {
                string colName = colReader.GetString(0);
                string dataType = colReader.GetString(1);
                bool isNullable = colReader.GetBoolean(2);
                bool isIdentity = colReader.GetBoolean(3);
                short maxLength = colReader.GetInt16(4);
                byte precision = colReader.IsDBNull(5) ? (byte)0 : colReader.GetByte(5);
                byte scale = colReader.IsDBNull(6) ? (byte)0 : colReader.GetByte(6);

                string finalType;

                if (dataType is "nvarchar" or "varchar" or "char" or "nchar")
                {
                    int actualLength = dataType.StartsWith("n") ? maxLength / 2 : maxLength;
                    finalType = actualLength > 0 && actualLength < 4000
                        ? $"{dataType}({actualLength})"
                        : $"{dataType}(MAX)";
                }
                else if (dataType == "decimal")
                {
                    finalType = $"DECIMAL({precision}, {scale})";
                }
                else
                {
                    finalType = dataType;
                }

                string nullable = isNullable ? "NULL" : "NOT NULL";

                if (isIdentity)
                {
                    identityColumns.Add(colName);
                    columnDefs.Add($"[{colName}] {finalType} IDENTITY(1,1) {nullable}");
                }
                else
                {
                    columnDefs.Add($"[{colName}] {finalType} {nullable}");
                }
            }
            colReader.Close();

            string tableScript = 
                $@"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}')
                BEGIN
                    CREATE TABLE {tableName} (
                        {string.Join(",\n        ", columnDefs)}
                    );
                    PRINT 'Tabla {tableName} creada.';
                END
                ELSE
                BEGIN
                    PRINT 'La tabla {tableName} ya existe.';
                END;";
            await writer.WriteLineAsync(tableScript);

            // INSERTS
            var insertCommand = new SqlCommand($"SELECT * FROM {tableName}", connection);
            using var reader = await insertCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var values = new List<string>();
                var columns = new List<string>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    if (identityColumns.Contains(colName)) continue;

                    object val = reader.GetValue(i);
                    string value = val == DBNull.Value ? "NULL" : $"'{val.ToString().Replace("'", "''")}'";

                    values.Add(value);
                    columns.Add($"[{colName}]");
                }

                if (columns.Count > 0)
                {
                    string insert = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});";
                    await writer.WriteLineAsync(insert);
                }
            }
            reader.Close();
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex.Message);
    }
}

}