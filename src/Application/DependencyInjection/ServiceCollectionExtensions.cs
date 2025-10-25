using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using YCC.SapAutomation.Application.Automation;
using YCC.SapAutomation.Application.DbScheduling;
using YCC.SapAutomation.Abstractions.Options;
using YCC.SapAutomation.Application.Jobs.ExternalProcess;

namespace YCC.SapAutomation.Application.DependencyInjection
{
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services, IConfiguration configuration)
    {
      services
        .AddOptions<SchedulerOptions>()
        .Bind(configuration.GetSection(SchedulerOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

      services
        .AddOptions<AutomationOptions>()
        .Bind(configuration.GetSection(AutomationOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

      services.AddSingleton<AutomationJobFactory>();
      // No registramos jobs concretos internos; las automatizaciones DotNet
      // pueden cargar tipos via ensamblados externos.
      services.AddTransient<ExternalProcessJob>();

      return services;
    }

    public static IServiceCollection AddAutomationRuntime(this IServiceCollection services, IConfiguration configuration)
    {
      var schedulerOptions = configuration
        .GetSection(SchedulerOptions.SectionName)
        .Get<SchedulerOptions>() ?? new SchedulerOptions();

      services.AddQuartz(q =>
      {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Math.Max(1, schedulerOptions.MaxConcurrency));
      });

      services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
      services.AddHostedService<AutomationSchedulingHostedService>();

      return services;
    }

    public static IServiceCollection AddDbAutomationRuntime(this IServiceCollection services, IConfiguration configuration)
    {
      var schedulerOptions = configuration
        .GetSection(SchedulerOptions.SectionName)
        .Get<SchedulerOptions>() ?? new SchedulerOptions();

      services.AddQuartz(q =>
      {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Math.Max(1, schedulerOptions.MaxConcurrency));
      });

      services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
      services.AddHostedService<DbAutomationSchedulingHostedService>();

      return services;
    }
  }
}
