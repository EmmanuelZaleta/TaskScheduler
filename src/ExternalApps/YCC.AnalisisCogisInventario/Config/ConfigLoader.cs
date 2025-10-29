using System;
using System.IO;
using System.Text.Json;

namespace YCC.AnalisisCogisInventario.Config;

internal static class ConfigLoader
{
    public static AppConfig Load(string[] args)
    {
        var cfg = LoadFromFile();

        // env overrides
        cfg.ConnectionName = FirstNonEmpty(
            Arg(args, "--conn"),
            Environment.GetEnvironmentVariable("SAP_CONN_NAME"),
            cfg.ConnectionName);

        cfg.ConnectionString = FirstNonEmpty(
            Arg(args, "--connstr"),
            Environment.GetEnvironmentVariable("SAP_CONN_STR"),
            cfg.ConnectionString);

        cfg.Username = FirstNonEmpty(
            Arg(args, "--user"),
            Environment.GetEnvironmentVariable("SAP_USER"),
            cfg.Username);

        cfg.Password = FirstNonEmpty(
            Arg(args, "--pass"),
            Environment.GetEnvironmentVariable("SAP_PASS"),
            cfg.Password);

        cfg.ExportDirectory = FirstNonEmpty(
            Arg(args, "--outdir"),
            Environment.GetEnvironmentVariable("SAP_EXPORT_DIR"),
            cfg.ExportDirectory);

        cfg.ExportFileName = FirstNonEmpty(
            Arg(args, "--outfile"),
            Environment.GetEnvironmentVariable("SAP_EXPORT_FILE"),
            cfg.ExportFileName);

        var plantArg = Arg(args, "--plant");
        if (!string.IsNullOrWhiteSpace(plantArg)) cfg.Plant = plantArg!;

        cfg.SqlTable = FirstNonEmpty(
            Arg(args, "--sql-table"),
            Environment.GetEnvironmentVariable("SQL_TABLE"),
            cfg.SqlTable);

        return cfg;
    }

    private static AppConfig LoadFromFile()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return new AppConfig();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    private static string? Arg(string[] args, string key)
    {
        if (args == null || args.Length == 0) return null;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length ? args[i + 1] : null;
            if (args[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return args[i].Substring(key.Length + 1);
        }
        return null;
    }

    private static string? FirstNonEmpty(params string?[] v)
        => Array.Find(v, s => !string.IsNullOrWhiteSpace(s));
}
