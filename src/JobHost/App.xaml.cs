using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using YCC.SapAutomation.Application.DependencyInjection;
using YCC.SapAutomation.Infrastructure.DependencyInjection;

namespace YCC.SapAutomation.Host
{
  public partial class App : System.Windows.Application
  {
    private IHost? _host;
    private bool _startMinimized = false;

    protected override async void OnStartup(StartupEventArgs e)
    {
      base.OnStartup(e);

      // Verificar si se debe iniciar minimizado (parámetro de línea de comandos)
      _startMinimized = e.Args.Contains("--minimized") || e.Args.Contains("/minimized");

      var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

      var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
      builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

      Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        .WriteTo.Debug()
        .WriteTo.File("logs/jobhost-.log", rollingInterval: RollingInterval.Day)
        .CreateLogger();

      builder.Logging.ClearProviders();
      builder.Logging.AddSerilog(Log.Logger, dispose: true);

      builder.Services
        .AddApplicationLayer(builder.Configuration)
        .AddSqlInfrastructure(builder.Configuration);

      var source = builder.Configuration.GetValue<string>("Automation:Source") ?? "Database";
      if (string.Equals(source, "Database", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddDbAutomationRuntime(builder.Configuration);
      else
        builder.Services.AddAutomationRuntime(builder.Configuration);

      // Agregar MainWindow como servicio
      builder.Services.AddSingleton<Views.MainWindow>();
      builder.Services.AddSingleton<Views.MainWindowViewModel>();

      _host = builder.Build();
      await _host.StartAsync();

      // Mostrar ventana principal
      var win = _host.Services.GetRequiredService<Views.MainWindow>();
      MainWindow = win;

      if (_startMinimized)
      {
        // Iniciar minimizado a la bandeja del sistema
        win.WindowState = WindowState.Minimized;
        win.ShowInTaskbar = false;
      }
      else
      {
        win.Show();
      }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
      try
      {
        if (_host is not null)
        {
          await _host.StopAsync(TimeSpan.FromSeconds(5));
          _host.Dispose();
        }
      }
      finally
      {
        Log.CloseAndFlush();
        base.OnExit(e);
      }
    }
  }
}
