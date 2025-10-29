using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sql;

internal sealed class Lx03SqlUploader
{
    public int Upload(string filePath, AppConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            ConsoleLogger.Warn("Ruta de archivo LX03 vacía. Omitiendo carga.");
            return 0;
        }
        if (!File.Exists(filePath))
        {
            ConsoleLogger.Warn($"Archivo LX03 no encontrado: {filePath}.");
            return 0;
        }

        var connStr = cfg.SqlConnection;
        var table = string.IsNullOrWhiteSpace(cfg.Lx03SqlTable) ? "dbo.LX03_Tran" : cfg.Lx03SqlTable!;
        if (string.IsNullOrWhiteSpace(connStr))
        {
            ConsoleLogger.Warn("Sin cadena SQL (appsettings: SqlConnection). Omitiendo carga de LX03.");
            return 0;
        }

        var dt = BuildSchema();
        int rows = ParseFileToDataTable(filePath, dt);
        if (rows == 0)
        {
            ConsoleLogger.Warn("Archivo LX03 sin filas de datos parseables.");
            return 0;
        }

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
        ConsoleLogger.Success($"Carga LX03 a SQL exitosa: {rows} filas -> {table}");
        return rows;
    }

    private static DataTable BuildSchema()
    {
        var dt = new DataTable();
        dt.Columns.Add("Typ", typeof(string));
        dt.Columns.Add("Storage Bin", typeof(string));
        dt.Columns.Add("Duration", typeof(string));
        dt.Columns.Add("Quants", typeof(string));
        dt.Columns.Add("S", typeof(string));
        dt.Columns.Add("Storage Unit", typeof(string));
        dt.Columns.Add("Material", typeof(string));
        dt.Columns.Add("Total Stock", typeof(string));
        dt.Columns.Add("Delivery", typeof(string));
        dt.Columns.Add("Plant", typeof(string));
        dt.Columns.Add("SLoc", typeof(string));
        dt.Columns.Add("Last mvmnt", typeof(string));
        dt.Columns.Add("GR Date", typeof(string));
        dt.Columns.Add("GR Number", typeof(string));
        dt.Columns.Add("Last change", typeof(string));
        dt.Columns.Add("DocumentNo", typeof(string));
        return dt;
    }

    private static int ParseFileToDataTable(string path, DataTable dt)
    {
        int count = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw?.TrimEnd() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("|---")) continue; // separador
            if (!line.StartsWith("|")) continue;   // solo filas tipo tabla
            if (line.IndexOf("Storage Bin", StringComparison.OrdinalIgnoreCase) >= 0 &&
                line.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
                continue; // encabezado

            var payload = line.Trim('|');
            var parts = payload.Split('|').Select(p => p.Trim()).ToArray();
            if (parts.Length > 0 && parts[0] == string.Empty) parts = parts.Skip(1).ToArray();
            if (parts.Length > 0 && parts[^1] == string.Empty) parts = parts.Take(parts.Length - 1).ToArray();
            if (parts.Length != 16) continue;

            var row = dt.NewRow();
            for (int i = 0; i < 16; i++) row[i] = parts[i];
            dt.Rows.Add(row);
            count++;
        }
        return count;
    }
}

