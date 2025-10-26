using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YCC.SapAutomation.Abstractions.Automation;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Abstractions.Options;
using YCC.SapAutomation.Abstractions.ResourceThrottling;
using YCC.SapAutomation.Abstractions.Storage;
using YCC.SapAutomation.Domain.Common;
using YCC.SapAutomation.Infrastructure.Automation;
using YCC.SapAutomation.Infrastructure.Common;
using YCC.SapAutomation.Infrastructure.Persistence;
using YCC.SapAutomation.Infrastructure.ResourceThrottling;
using YCC.SapAutomation.Infrastructure.Sql;

namespace YCC.SapAutomation.Infrastructure.DependencyInjection
{
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection AddSqlInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
      var connectionString = configuration.GetConnectionString("Sql");
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        throw new InvalidOperationException("No se encontro la cadena de conexion 'Sql'.");
      }

      services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
      services.AddSingleton<IJobDefinitionStore, SqlJobDefinitionStore>();
      services.AddSingleton<IAutomationManifestProvider, FileSystemAutomationManifestProvider>();
      services.AddSingleton<IJobRunStore, SqlJobRunStore>();
      services.AddSingleton<IExcludedHuStore, SqlExcludedHuStore>();
      services.AddSingleton<IClock, UtcClock>();

      // Resource throttling
      services.Configure<ResourceThrottlingOptions>(configuration.GetSection(ResourceThrottlingOptions.SectionName));
      services.AddSingleton<IResourceThrottlingManager, ResourceThrottlingManager>();

      // Job configuration cache
      services.Configure<JobConfigurationCacheOptions>(configuration.GetSection(JobConfigurationCacheOptions.SectionName));
      services.AddSingleton<IJobConfigurationCache, FileSystemJobConfigurationCache>();

      return services;
    }
  }
}
