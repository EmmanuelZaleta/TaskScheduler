using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using YCC.SapAutomation.Application.DependencyInjection;
using YCC.SapAutomation.Infrastructure.DependencyInjection;
using YCC.SapAutomation.Sap.DependencyInjection;

var builder = Host.CreateDefaultBuilder(args);

var runAsService = WindowsServiceHelpers.IsWindowsService() &&
                   !args.Any(a => string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase));

if (runAsService)
  builder = builder.UseWindowsService();
else
  builder = builder.UseConsoleLifetime();

builder
  .UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration))
  .ConfigureServices((ctx, services) =>
  {
    services
      .AddApplicationLayer(ctx.Configuration)
      .AddSqlInfrastructure(ctx.Configuration)
      .AddSapAdapters(ctx.Configuration);

    var source = ctx.Configuration.GetValue<string>("Automation:Source") ?? "Database";
    if (string.Equals(source, "Database", StringComparison.OrdinalIgnoreCase))
      services.AddDbAutomationRuntime(ctx.Configuration);
    else
      services.AddAutomationRuntime(ctx.Configuration);
  });

await builder.Build().RunAsync();
