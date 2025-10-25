using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;
using YCC.SapAutomation.Abstractions.Options;
using YCC.SapAutomation.Application.DependencyInjection;
using YCC.SapAutomation.Infrastructure.DependencyInjection;
using YCC.SapAutomation.Sap.DependencyInjection;

namespace YCC.SapAutomation.AutomationCli;

internal static class Program
{
  public static async Task<int> Main(string[] args)
  {
    var (hostArgs, cliOptions) = CliArgumentParser.Parse(args);

    var builder = Host.CreateApplicationBuilder(hostArgs);

    builder.Services.AddSingleton(cliOptions);

    builder.Configuration
      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
      .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
                   optional: true, reloadOnChange: false)
      .AddEnvironmentVariables();

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
      .WriteTo.Console()
      .CreateLogger(), dispose: true);

    builder.Services
      .AddApplicationLayer(builder.Configuration)
      .AddSqlInfrastructure(builder.Configuration)
      .AddSapAdapters(builder.Configuration);

    // Permite sobreescribir la carpeta de manifiestos desde CLI (--manifestsPath)
      builder.Services.PostConfigure<AutomationOptions>(opt =>
    {
      if (!string.IsNullOrWhiteSpace(cliOptions.ManifestsPath))
      {
        opt.ManifestsPath = cliOptions.ManifestsPath!;
      }
    });

    var schedulerOptions = builder.Configuration
      .GetSection(SchedulerOptions.SectionName)
      .Get<SchedulerOptions>() ?? new SchedulerOptions();

    var source = builder.Configuration.GetValue<string>("Automation:Source") ?? "Database";
    if (!string.IsNullOrWhiteSpace(cliOptions.ManifestsPath))
    {
      source = "Files";
    }

    if (string.Equals(source, "Database", StringComparison.OrdinalIgnoreCase))
    {
      builder.Services.AddQuartz(q =>
      {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Math.Max(1, schedulerOptions.MaxConcurrency));
      });
      builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
      builder.Services.AddHostedService<YCC.SapAutomation.Application.DbScheduling.DbAutomationSchedulingHostedService>();
    }
    else
    {
      builder.Services.AddQuartz(q =>
      {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Math.Max(1, schedulerOptions.MaxConcurrency));
      });
      builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
      builder.Services.AddHostedService<AutomationRunOnceHostedService>();
    }

    using var host = builder.Build();
    await host.StartAsync();
    await host.WaitForShutdownAsync();
    return 0;
  }
}
