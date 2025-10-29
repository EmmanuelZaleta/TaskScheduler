using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sql;

internal sealed class Lx03SqlUploader
{
    // Orden clásico de columnas LX03 (fallback si no se detecta encabezado)
    private static readonly string[] ClassicOrder16 =
    {
        "Typ", "Storage Bin", "Duration", "Quants", "S", "Storage Unit",
        "Material", "Total Stock", "Delivery", "Plant", "SLoc", "Last mvmnt",
        "GR Date", "GR Number", "Last change", "DocumentNo"
    };

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

        // 1) Detectar encabezado y datos
        if (!TryReadHeader(filePath, out var headerCols, out var dataLines))
        {
            ConsoleLogger.Warn("No se detectó encabezado; usando orden clásico LX03 (16 cols).");
            if (!TryReadDataWithClassic(filePath, out headerCols, out dataLines))
            {
                ConsoleLogger.Warn("Archivo LX03 sin filas válidas.");
                return 0;
            }
        }
        headerCols = DeDupe(headerCols);

        // 2) DataTable con columnas del archivo
        var dt = new DataTable();
        foreach (var col in headerCols) dt.Columns.Add(col, typeof(string));

        int rows = 0;
        foreach (var arr in dataLines)
        {
            var row = dt.NewRow();
            var take = Math.Min(headerCols.Count, arr.Length);
            for (int i = 0; i < take; i++)
                row[headerCols[i]] = arr[i]?.Trim() ?? string.Empty;
            dt.Rows.Add(row);
            rows++;
        }
        if (rows == 0)
        {
            ConsoleLogger.Warn("Archivo LX03 sin filas válidas para cargar.");
            return 0;
        }

        // 3) Asegurar tablas/columnas (resiliente)
        using var cn = new SqlConnection(connStr);
        cn.Open();

        var tablesToEnsure = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            table,
            "dbo.LX03",
            "dbo.LX03_Tran"
        };

        foreach (var tbl in tablesToEnsure)
            SafeEnsureTableWithColumns(cn, tbl, headerCols); // no detiene si falla

        // 4) Mapear solo columnas que existan en destino
        var destCols = SafeGetExistingColumns(cn, table);
        var usable = headerCols.Where(destCols.Contains).ToList();
        if (usable.Count == 0)
        {
            ConsoleLogger.Warn("No hay columnas utilizables en la tabla destino.");
            return 0;
        }

        // 5) BulkCopy con mapeo seguro
        using var bulk = new SqlBulkCopy(cn)
        {
            DestinationTableName = table,
            BulkCopyTimeout = 0,
            BatchSize = 5000
        };
        foreach (var c in usable)
            bulk.ColumnMappings.Add(c, c);

        // copiar sólo las columnas mapeadas
        var dtCopy = dt.DefaultView.ToTable(false, usable.ToArray());
        bulk.WriteToServer(dtCopy);

        ConsoleLogger.Success($"LX03 -> SQL: {rows} filas, {usable.Count}/{headerCols.Count} columnas -> {table}");
        return rows;
    }

    // =================== Lectura resiliente del archivo ===================

    private static bool TryReadHeader(string path, out List<string> headerCols, out List<string[]> dataLines)
    {
        headerCols = new();
        dataLines = new();

        var lines = File.ReadAllLines(path);
        int headerIdx = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = (lines[i] ?? string.Empty).TrimEnd();
            if (!IsPipeLine(line) || IsSeparator(line)) continue;

            var cells = SplitCells(TrimPipes(line));
            if (cells.Count >= 5 && IsLikelyHeader(cells))
            {
                headerCols = NormalizeHeaderKeepOriginal(cells);
                headerIdx = i;
                break;
            }
        }
        if (headerIdx < 0) return false;

        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var line = (lines[i] ?? string.Empty).TrimEnd();
            if (!IsPipeLine(line) || IsSeparator(line)) continue;
            if (LooksLikeHeaderAgain(line)) continue;
            var cells = SplitCells(TrimPipes(line));
            if (cells.Count == 0) continue;
            dataLines.Add(cells.ToArray());
        }

        return headerCols.Count > 0 && dataLines.Count > 0;
    }

    private static bool TryReadDataWithClassic(string path, out List<string> headerCols, out List<string[]> dataLines)
    {
        headerCols = ClassicOrder16.ToList();
        dataLines = new();

        foreach (var raw in File.ReadLines(path))
        {
            var line = (raw ?? string.Empty).TrimEnd();
            if (!IsPipeLine(line) || IsSeparator(line)) continue;
            if (line.IndexOf("Storage Bin", StringComparison.OrdinalIgnoreCase) >= 0 &&
                line.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
                continue; // encabezado

            var cells = SplitCells(TrimPipes(line));
            // Aceptar 16 o más columnas (flexible)
            if (cells.Count >= 16)
            {
                // Si tiene más de 16, agregar las columnas extra dinámicamente
                while (headerCols.Count < cells.Count)
                    headerCols.Add($"Extra{headerCols.Count - 15}");
                dataLines.Add(cells.ToArray());
            }
        }
        return dataLines.Count > 0;
    }

    private static bool IsPipeLine(string s) => s.StartsWith("|");
    private static bool IsSeparator(string s) => s.StartsWith("|---");
    private static bool LooksLikeHeaderAgain(string s) =>
        s.IndexOf("Storage Bin", StringComparison.OrdinalIgnoreCase) >= 0 &&
        s.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string TrimPipes(string s)
    {
        var t = s;
        if (t.StartsWith("|")) t = t[1..];
        if (t.EndsWith("|")) t = t[..^1];
        return t;
    }

    private static List<string> SplitCells(string s)
        => s.Split('|').Select(p => p.Trim()).ToList();

    private static bool IsLikelyHeader(List<string> cells)
    {
        int hits = 0;
        string[] hints = { "Storage Bin", "Material", "Plant", "SLoc", "Total Stock", "Storage Unit", "Typ" };
        foreach (var c in cells)
            if (hints.Any(h => c.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)) hits++;
        return hits >= 3;
    }

    private static List<string> NormalizeHeaderKeepOriginal(List<string> cells)
    {
        var list = new List<string>();
        foreach (var c in cells)
        {
            var t = (c ?? string.Empty).Trim();
            if (t.Length == 0) continue;      // descarta vacíos
            list.Add(t);
        }
        return list;
    }

    private static List<string> DeDupe(List<string> cols)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<string>();
        foreach (var c in cols)
        {
            var name = c;
            int k = 2;
            while (!set.Add(name)) name = $"{c}_{k++}";
            deduped.Add(name);
        }
        return deduped;
    }

    // =================== SQL helpers (seguros y que no truenan) ===================

    private static void SafeEnsureTableWithColumns(SqlConnection cn, string tableName, IReadOnlyList<string> cols)
    {
        try
        {
            EnsureTableWithColumns(cn, tableName, cols);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"Ensure columnas falló en {tableName}: {ex.Message}");
            // Sigue: no detenemos el flujo
        }
    }

    private static HashSet<string> SafeGetExistingColumns(SqlConnection cn, string tableName)
    {
        try
        {
            return GetExistingColumns(cn, tableName);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"No se pudieron leer columnas de {tableName}: {ex.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void EnsureTableWithColumns(SqlConnection cn, string tableName, IReadOnlyList<string> cols)
    {
        // Crear tabla si no existe
        var twoPart = QuoteTwoPart(tableName); // [dbo].[LX03_Tran] o [LX03_Tran]
        var colDefs = string.Join(", ", cols.Select(c => $"{QuoteIdent(c)} NVARCHAR(MAX) NULL"));
        var createBody = $"CREATE TABLE {twoPart} ({colDefs})";

        var createSql =
$@"
IF OBJECT_ID({SqlLit(tableName)}, 'U') IS NULL
BEGIN
    EXEC(N'{SqlStr(createBody)}');
END;
";
        using (var cmd = new SqlCommand(createSql, cn)) cmd.ExecuteNonQuery();

        // Agregar columnas faltantes una por una (cada ALTER protegido)
        foreach (var col in cols)
        {
            var alterBody = $"ALTER TABLE {twoPart} ADD {QuoteIdent(col)} NVARCHAR(MAX) NULL";
            var addSql =
$@"
IF COL_LENGTH({SqlLit(tableName)}, {SqlLit(col)}) IS NULL
BEGIN
    BEGIN TRY
        EXEC(N'{SqlStr(alterBody)}');
    END TRY
    BEGIN CATCH
        -- no detenemos el flujo
    END CATCH
END;
";
            using var cmd = new SqlCommand(addSql, cn);
            cmd.ExecuteNonQuery();
        }
    }

    private static HashSet<string> GetExistingColumns(SqlConnection cn, string tableName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string sql =
@"SELECT c.name
  FROM sys.columns c
 WHERE c.object_id = OBJECT_ID(@t)";
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@t", tableName);
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) set.Add(rd.GetString(0));
        return set;
    }

    // ========= Utilities para nombres/literales seguros =========

    private static string QuoteIdent(string name)
    {
        // [A] -> [A]; maneja corchetes y deja cualquier carácter dentro
        var safe = (name ?? string.Empty).Replace("]", "]]");
        return $"[{safe}]";
    }

    private static string QuoteTwoPart(string twoPart)
    {
        // soporta "dbo.LX03_Tran" o "LX03_Tran"
        var parts = (twoPart ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()).ToArray();
        if (parts.Length == 1) return QuoteIdent(parts[0]);
        if (parts.Length >= 2) return $"{QuoteIdent(parts[0])}.{QuoteIdent(parts[1])}";
        return QuoteIdent(twoPart ?? string.Empty);
    }

    // Literal T-SQL: N'...'
    private static string SqlLit(string s) => $"N'{SqlStr(s)}'";

    // Escapa contenido para meter dentro de N'...'
    private static string SqlStr(string s)
    {
        if (s == null) return string.Empty;
        // duplicar comillas simples para no romper el literal
        return s.Replace("'", "''");
    }
}

