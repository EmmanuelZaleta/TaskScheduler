using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Sap.Contracts;
using YCC.SapAutomation.Sap.Options;
using YCC.SapAutomation.Sap.Services;

namespace YCC.SapAutomation.Sap.DependencyInjection
{
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection AddSapAdapters(this IServiceCollection services, IConfiguration configuration)
    {
      services.AddOptions<SapOptions>()
              .Bind(configuration.GetSection(SapOptions.SectionName))
              .ValidateDataAnnotations()
              .ValidateOnStart();

      // Registrar el conector de SAP GUI como Singleton
      // Usamos Singleton porque mantiene el estado de la conexión COM
      if (OperatingSystem.IsWindows())
      {
        services.AddSingleton<ISapGuiConnector, SapGuiConnector>();
      }

      return services;
    }
  }
}
