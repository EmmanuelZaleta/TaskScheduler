namespace YCC.AnalisisCogisInventario.Config;

internal sealed class AppConfig
{
    public string? ConnectionName { get; set; }
    public string? ConnectionString { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int LoginTimeoutSeconds { get; set; } = 90;

    public string? ExportDirectory { get; set; }
    public string? ExportFileName { get; set; } = "cogi_{timestamp}.txt";
    public string Plant { get; set; } = "1815";
    public string? SqlConnection { get; set; }
    public string? SqlTable { get; set; }
    public string? Ficq312SqlTable { get; set; }
    public bool OpenNewSession { get; set; } = true;
    public bool CloseSessionOnExit { get; set; } = true;
    public string Ficq312TCode { get; set; } = "/n/YZKNA/FICQ312";
    public string? Ficq312From { get; set; }
    public string? Ficq312To { get; set; }
    public int Ficq312DaysBack { get; set; } = 30;
    public string Ficq312ExportFileName { get; set; } = "FICQ312_{timestamp}.txt";
    // LX03 options
    public string? Lx03Warehouse { get; set; } = "181";
    public string? Lx03ExportFileName { get; set; } = "LX03.txt";
    public string? Lx03SqlTable { get; set; }
    // SE16N options
    public string? Se16nPlant { get; set; } = "1815";
    public string? Se16nDate { get; set; }
    public string? Se16nExportFileName { get; set; } = "TQMR1600.txt";
    public string? Se16nSqlTable { get; set; }
    // TQMR1600 (SE16N) preferred naming
    public string? Tqmr1600TableName { get; set; } = "/YZKNA/TQMR1600";
    public string? Tqmr1600Plant { get; set; }
    public string? Tqmr1600Date { get; set; }
    public string? Tqmr1600ExportFileName { get; set; }
    public string? Tqmr1600SqlTable { get; set; }
}
