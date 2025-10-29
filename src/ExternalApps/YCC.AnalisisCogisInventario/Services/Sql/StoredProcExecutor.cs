using System;
using System.Data;
using System.Data.SqlClient;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sql;

internal static class StoredProcExecutor
{
    public static void Execute(string? connectionString, string procName, int timeoutSeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ConsoleLogger.Warn($"SP '{procName}' omitido: no hay SqlConnection en appsettings.");
            return;
        }
        if (string.IsNullOrWhiteSpace(procName)) return;

        try
        {
            ConsoleLogger.Info($"Ejecutando SP: {procName}...");
            using var cn = new SqlConnection(connectionString);
            cn.Open();
            using var cmd = new SqlCommand(procName, cn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = timeoutSeconds <= 0 ? 0 : timeoutSeconds
            };
            cmd.ExecuteNonQuery();
            ConsoleLogger.Success($"SP completado: {procName}");
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"SP '{procName}' fallo: {ex.Message}");
            throw;
        }
    }
}

