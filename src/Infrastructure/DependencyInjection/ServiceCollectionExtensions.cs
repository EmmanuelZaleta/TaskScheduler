using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YCC.SapAutomation.Domain.Common;
using YCC.SapAutomation.Infrastructure.Common;
using YCC.SapAutomation.Infrastructure.Persistence;
using YCC.SapAutomation.Infrastructure.Sql;
using YCC.SapAutomation.Infrastructure.Automation;
using YCC.SapAutomation.Abstractions.Automation;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Abstractions.Storage;
using YCC.SapAutomation.Abstractions.Tqmbulk;

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
      services.AddSingleton<ITqmbulkRepository, SqlTqmbulkRepository>();
      services.AddSingleton<IClock, UtcClock>();

      return services;
    }
  }
}
