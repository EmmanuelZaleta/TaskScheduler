using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using YCC.SapAutomation.Application.DependencyInjection;
using YCC.SapAutomation.Infrastructure.DependencyInjection;
using YCC.SapAutomation.Host.Views;
using YCC.SapAutomation.Host.Utilities;

namespace YCC.SapAutomation.Host;

public partial class App : Application
{
  private IHost? _host;
  private bool _isShuttingDown = false;

  public App()
  {
    InitializeComponent();
  }

  protected override async void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    try
    {
      // Registrar en Startup de Windows
      StartupHelper.RegisterInWindowsStartup();

      // Construir host
      var builder = Host.CreateApplicationBuilder();

      builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                     optional: true, reloadOnChange: false)
        .AddEnvironmentVariables();

      builder.Logging.ClearProviders();
      builder.Logging.AddSerilog(new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.File("logs/jobhost-.log", rollingInterval: RollingInterval.Day)
        .WriteTo.Debug()
        .CreateLogger(), dispose: true);

      builder.Services
        .AddApplicationLayer(builder.Configuration)
        .AddSqlInfrastructure(builder.Configuration);

      // Agregar MainWindow como servicio
      builder.Services.AddSingleton<MainWindow>();
      builder.Services.AddSingleton<MainWindowViewModel>();

      var source = builder.Configuration.GetValue<string>("Automation:Source") ?? "Database";
      if (string.Equals(source, "Database", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddDbAutomationRuntime(builder.Configuration);
      else
        builder.Services.AddAutomationRuntime(builder.Configuration);

      _host = builder.Build();

      // Mostrar ventana
      var mainWindow = _host.Services.GetRequiredService<MainWindow>();
      mainWindow.Show();

      // Ejecutar host
      _ = _host.RunAsync().ContinueWith(_ =>
      {
        if (!_isShuttingDown)
        {
          Dispatcher.Invoke(() => Shutdown());
        }
      });
    }
    catch (Exception ex)
    {
      MessageBox.Show($"Error al iniciar: {ex.Message}\n\n{ex.StackTrace}",
        "Error Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
      Shutdown();
    }
  }

  protected override async void OnExit(ExitEventArgs e)
  {
    _isShuttingDown = true;

    if (_host != null)
    {
      await _host.StopAsync(TimeSpan.FromSeconds(5));
      _host.Dispose();
    }

    base.OnExit(e);
  }
}
