using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sql
{
    internal sealed class Tqmr1600SqlUploader
    {
        public int Upload(string filePath, AppConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ConsoleLogger.Warn("TQMR1600: archivo no encontrado. Omitiendo carga.");
                return 0;
            }
            if (string.IsNullOrWhiteSpace(cfg.SqlConnection))
            {
                ConsoleLogger.Warn("TQMR1600: sin cadena de conexión SQL. Omitiendo carga.");
                return 0;
            }

            string table = !string.IsNullOrWhiteSpace(cfg.Tqmr1600SqlTable)
                ? cfg.Tqmr1600SqlTable!
                : "dbo.TQMR1600_Tran";

            var dt = BuildSchema();
            int rows = 0;

            foreach (var raw in File.ReadLines(filePath))
            {
                var line = raw?.TrimEnd();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("|") || line.StartsWith("|---") || line.Contains("Plnt|Work ctr"))
                    continue;

                // Dividir por tuberías y limpiar
                var parts = line.Split('|').Select(p => p.Trim()).ToList();

                // Remover celdas vacías al inicio y fin (bordes de tabla)
                if (parts.First() == "") parts.RemoveAt(0);
                if (parts.Last() == "") parts.RemoveAt(parts.Count - 1);

                if (parts.Count != 16) continue; // debe haber 16 columnas exactas

                var row = dt.NewRow();
                row["Plnt"] = parts[0];
                row["Work ctr"] = parts[1];
                row["Equipment"] = parts[2];
                row["Func. Loc."] = parts[3];
                row["Material"] = parts[4];
                row["Component"] = parts[5];
                row["POS Create"] = parts[6];
                row["HU"] = parts[7];
                row["ID number"] = parts[8];
                row["RemQty"] = parts[9];
                row["BUn"] = parts[10];
                row["Date"] = parts[11];
                row["Time"] = parts[12];
                row["Name of user from SAP logon"] = parts[13];
                row["FIFO"] = parts[14];
                row["FIFO (2)"] = parts[15];
                dt.Rows.Add(row);
                rows++;
            }

            if (rows == 0)
            {
                ConsoleLogger.Warn("TQMR1600: archivo vacío o sin datos válidos.");
                return 0;
            }

            using var cn = new SqlConnection(cfg.SqlConnection);
            cn.Open();

            using var bulk = new SqlBulkCopy(cn)
            {
                DestinationTableName = table,
                BulkCopyTimeout = 0,
                BatchSize = 5000
            };

            foreach (DataColumn col in dt.Columns)
                bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            bulk.WriteToServer(dt);
            ConsoleLogger.Success($"TQMR1600: {rows} filas cargadas en {table}");
            return rows;
        }

        private static DataTable BuildSchema()
        {
            var dt = new DataTable();
            dt.Columns.Add("Plnt", typeof(string));
            dt.Columns.Add("Work ctr", typeof(string));
            dt.Columns.Add("Equipment", typeof(string));
            dt.Columns.Add("Func. Loc.", typeof(string));
            dt.Columns.Add("Material", typeof(string));
            dt.Columns.Add("Component", typeof(string));
            dt.Columns.Add("POS Create", typeof(string));
            dt.Columns.Add("HU", typeof(string));
            dt.Columns.Add("ID number", typeof(string));
            dt.Columns.Add("RemQty", typeof(string));
            dt.Columns.Add("BUn", typeof(string));
            dt.Columns.Add("Date", typeof(string));
            dt.Columns.Add("Time", typeof(string));
            dt.Columns.Add("Name of user from SAP logon", typeof(string));
            dt.Columns.Add("FIFO", typeof(string));
            dt.Columns.Add("FIFO (2)", typeof(string));
            return dt;
        }
    }
}
