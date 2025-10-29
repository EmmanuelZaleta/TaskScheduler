using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sql;

internal sealed class Ficq312SqlUploader
{
    public int Upload(string filePath, AppConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            ConsoleLogger.Warn("Ruta de archivo FICQ312 vacía. Omitiendo carga.");
            return 0;
        }
        if (!File.Exists(filePath))
        {
            ConsoleLogger.Warn($"Archivo FICQ312 no encontrado: {filePath}. Configure 'ExportDirectory' para apuntar a la carpeta usada por SAP.");
            return 0;
        }

        var connStr = cfg.SqlConnection; // Debe venir desde appsettings.json
        var table = string.IsNullOrWhiteSpace(cfg.Ficq312SqlTable) ? "dbo.FICQ312_Tran" : cfg.Ficq312SqlTable!;

        if (string.IsNullOrWhiteSpace(connStr))
        {
            ConsoleLogger.Warn("Sin cadena SQL (appsettings: SqlConnection). Omitiendo carga de FICQ312.");
            return 0;
        }

        try
        {
            var dt = BuildSchema();
            int rows = ParseFile(filePath, dt);
            if (rows == 0)
            {
                ConsoleLogger.Warn("Archivo FICQ312 sin filas de datos.");
                return 0;
            }

            // Asegurar que todas las columnas existen en la tabla de destino
            EnsureColumnsExist(connStr!, table, dt);

            BulkInsert(dt, connStr!, table);
            ConsoleLogger.Success($"Carga FICQ312 a SQL exitosa: {rows} filas -> {table}");
            return rows;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error al cargar FICQ312 a SQL: {ex.Message}");
            ConsoleLogger.Debug($"Stack trace: {ex.StackTrace}");
            return 0;
        }
    }

    private static DataTable BuildSchema()
    {
        var dt = new DataTable();
        dt.Columns.Add("Plnt", typeof(string));
        dt.Columns.Add("Material", typeof(string));
        dt.Columns.Add("SPT", typeof(string));
        dt.Columns.Add("Created", typeof(string));
        dt.Columns.Add("Time", typeof(string));
        dt.Columns.Add("CreatedBy", typeof(string));
        dt.Columns.Add("SLoc", typeof(string));
        dt.Columns.Add("Order", typeof(string));
        dt.Columns.Add("MvT", typeof(string));
        dt.Columns.Add("Quantity", typeof(string));
        dt.Columns.Add("EUn", typeof(string));
        dt.Columns.Add("Amount", typeof(string));
        dt.Columns.Add("Crcy", typeof(string));
        dt.Columns.Add("ErrorMessage", typeof(string));
        dt.Columns.Add("Assembly", typeof(string));
        dt.Columns.Add("AssemblyMaterialNumber", typeof(string));
        dt.Columns.Add("Backflush", typeof(string));
        dt.Columns.Add("MStatus", typeof(string));
        return dt;
    }

    private static int ParseFile(string path, DataTable dt)
    {
        int count = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("|")) continue;
            if (line.StartsWith("|---")) continue; // separador
            if (line.IndexOf("Plnt", StringComparison.OrdinalIgnoreCase) >= 0 &&
                line.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
                continue; // encabezado

            var payload = line.Trim('|');
            var parts = payload.Split('|').Select(p => p.Trim()).ToArray();
            if (parts.Length < 18) continue;

            var row = dt.NewRow();
            row["Plnt"] = parts[0];
            row["Material"] = parts[1];
            row["SPT"] = parts[2];
            row["Created"] = parts[3];
            row["Time"] = parts[4];
            row["CreatedBy"] = parts[5];
            row["SLoc"] = parts[6];
            row["Order"] = parts[7];
            row["MvT"] = parts[8];
            row["Quantity"] = parts[9];
            row["EUn"] = parts[10];
            row["Amount"] = parts[11];
            row["Crcy"] = parts[12];
            row["ErrorMessage"] = parts[13];
            row["Assembly"] = parts[14];
            row["AssemblyMaterialNumber"] = parts[15];
            row["Backflush"] = parts[16];
            row["MStatus"] = parts[17];
            dt.Rows.Add(row);
            count++;
        }
        return count;
    }

    private static void BulkInsert(DataTable dt, string connStr, string table)
    {
        try
        {
            using var cn = new SqlConnection(connStr);
            cn.Open();
            using var bulk = new SqlBulkCopy(cn)
            {
                DestinationTableName = table,
                BatchSize = 5000,
                BulkCopyTimeout = 0
            };
            foreach (DataColumn c in dt.Columns)
                bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            bulk.WriteToServer(dt);
        }
        catch (SqlException ex)
        {
            ConsoleLogger.Error($"Error de SQL durante BulkInsert: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error durante BulkInsert: {ex.Message}");
            throw;
        }
    }

    private static void EnsureColumnsExist(string connStr, string tableName, DataTable schema)
    {
        try
        {
            using var cn = new SqlConnection(connStr);
            cn.Open();

            // Obtener columnas existentes en la tabla
            var existingColumns = GetExistingColumns(cn, tableName);

            // Determinar columnas faltantes
            var missingColumns = new List<DataColumn>();
            foreach (DataColumn col in schema.Columns)
            {
                if (!existingColumns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    missingColumns.Add(col);
                }
            }

            // Crear columnas faltantes
            if (missingColumns.Count > 0)
            {
                ConsoleLogger.Info($"Creando {missingColumns.Count} columna(s) faltante(s) en {tableName}...");
                CreateMissingColumns(cn, tableName, missingColumns);
                ConsoleLogger.Success($"{missingColumns.Count} columna(s) creada(s) exitosamente.");
            }
            else
            {
                ConsoleLogger.Debug($"Todas las columnas ya existen en {tableName}.");
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error al verificar/crear columnas: {ex.Message}");
            throw;
        }
    }

    private static HashSet<string> GetExistingColumns(SqlConnection cn, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Parsear el nombre de la tabla para extraer schema y nombre
        var parts = tableName.Split('.');
        string schema = parts.Length > 1 ? parts[0] : "dbo";
        string table = parts.Length > 1 ? parts[1] : tableName;

        // Limpiar corchetes si existen
        schema = schema.Trim('[', ']');
        table = table.Trim('[', ']');

        var query = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table";

        using var cmd = new SqlCommand(query, cn);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static void CreateMissingColumns(SqlConnection cn, string tableName, List<DataColumn> missingColumns)
    {
        foreach (var col in missingColumns)
        {
            try
            {
                // Determinar el tipo SQL basado en el tipo .NET
                string sqlType = GetSqlType(col.DataType);

                var alterSql = $"ALTER TABLE {tableName} ADD [{col.ColumnName}] {sqlType} NULL";

                using var cmd = new SqlCommand(alterSql, cn);
                cmd.ExecuteNonQuery();

                ConsoleLogger.Debug($"  - Columna creada: {col.ColumnName} ({sqlType})");
            }
            catch (SqlException ex)
            {
                ConsoleLogger.Warn($"No se pudo crear columna {col.ColumnName}: {ex.Message}");
                // Continuar con las demás columnas
            }
        }
    }

    private static string GetSqlType(Type netType)
    {
        // Mapeo de tipos .NET a tipos SQL
        if (netType == typeof(string))
            return "NVARCHAR(MAX)";
        if (netType == typeof(int))
            return "INT";
        if (netType == typeof(long))
            return "BIGINT";
        if (netType == typeof(decimal))
            return "DECIMAL(18,2)";
        if (netType == typeof(double) || netType == typeof(float))
            return "FLOAT";
        if (netType == typeof(DateTime))
            return "DATETIME";
        if (netType == typeof(bool))
            return "BIT";
        if (netType == typeof(Guid))
            return "UNIQUEIDENTIFIER";

        // Por defecto, usar NVARCHAR(MAX) para tipos desconocidos
        return "NVARCHAR(MAX)";
    }
}

