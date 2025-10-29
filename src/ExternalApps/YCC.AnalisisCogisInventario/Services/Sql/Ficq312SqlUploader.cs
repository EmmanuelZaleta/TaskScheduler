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
            // Primero leer el encabezado del archivo para crear el esquema dinámicamente
            var columnHeaders = ReadColumnHeaders(filePath);
            if (columnHeaders == null || columnHeaders.Count == 0)
            {
                ConsoleLogger.Warn("No se pudo leer el encabezado del archivo FICQ312.");
                return 0;
            }

            var dt = BuildDynamicSchema(columnHeaders);
            int rows = ParseFileByColumnName(filePath, dt, columnHeaders);
            if (rows == 0)
            {
                ConsoleLogger.Warn("Archivo FICQ312 sin filas de datos.");
                return 0;
            }

            // Asegurar que todas las columnas existen en AMBAS tablas: FICQ312_Tran y FICQ312
            EnsureColumnsExist(connStr!, "dbo.FICQ312_Tran", dt);
            EnsureColumnsExist(connStr!, "dbo.FICQ312", dt);

            // Insertar en la tabla configurada
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

    /// <summary>
    /// Lee el encabezado del archivo SAP para obtener los nombres de las columnas
    /// </summary>
    private static List<string>? ReadColumnHeaders(string path)
    {
        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("|")) continue;
                if (line.StartsWith("|---")) continue; // separador

                // Buscar la línea de encabezado
                if (line.IndexOf("Plnt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var payload = line.Trim('|');
                    var headers = payload.Split('|')
                        .Select(h => h.Trim())
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .ToList();

                    ConsoleLogger.Debug($"Encabezados encontrados: {string.Join(", ", headers)}");
                    return headers;
                }
            }

            ConsoleLogger.Warn("No se encontró línea de encabezado en el archivo FICQ312");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error al leer encabezados: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Construye el esquema del DataTable basado en las columnas del archivo
    /// </summary>
    private static DataTable BuildDynamicSchema(List<string> columnHeaders)
    {
        var dt = new DataTable();

        // Crear una columna para cada encabezado encontrado
        foreach (var header in columnHeaders)
        {
            // Todas las columnas son strings (NVARCHAR(MAX) en SQL)
            dt.Columns.Add(header, typeof(string));
        }

        ConsoleLogger.Debug($"Esquema creado con {dt.Columns.Count} columnas");
        return dt;
    }

    /// <summary>
    /// Parsea el archivo usando los nombres de columna en lugar de posiciones fijas
    /// </summary>
    private static int ParseFileByColumnName(string path, DataTable dt, List<string> columnHeaders)
    {
        int count = 0;
        bool headerFound = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("|")) continue;
            if (line.StartsWith("|---")) continue; // separador

            // Saltar el encabezado
            if (!headerFound && (line.IndexOf("Plnt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                headerFound = true;
                continue;
            }

            if (!headerFound) continue; // Seguir buscando el encabezado

            var payload = line.Trim('|');
            var parts = payload.Split('|').Select(p => p.Trim()).ToArray();

            // Validar que tengamos al menos tantas partes como columnas
            if (parts.Length < columnHeaders.Count)
            {
                ConsoleLogger.Debug($"Línea omitida: tiene {parts.Length} columnas, se esperaban {columnHeaders.Count}");
                continue;
            }

            var row = dt.NewRow();

            // Mapear cada valor a su columna correspondiente por nombre
            for (int i = 0; i < columnHeaders.Count && i < parts.Length; i++)
            {
                var columnName = columnHeaders[i];
                if (dt.Columns.Contains(columnName))
                {
                    row[columnName] = parts[i];
                }
            }

            dt.Rows.Add(row);
            count++;
        }

        ConsoleLogger.Debug($"Parseadas {count} filas de datos");
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

