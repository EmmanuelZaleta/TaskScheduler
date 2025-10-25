using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Sap.Options;

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

      return services;
    }
  }
}
