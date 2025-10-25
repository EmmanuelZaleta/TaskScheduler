using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Application.Options;
using YCC.SapAutomation.Sap.Adapters;

namespace YCC.SapAutomation.Sap.DependencyInjection
{
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection AddSapAdapters(this IServiceCollection services, IConfiguration configuration)
    {
      var sapSection = configuration.GetSection(SapOptions.SectionName);

      services.AddOptions<SapOptions>()
              .Bind(sapSection);

      services.AddSingleton<SapRfcTqmbulkSource>();
      services.AddSingleton<SapGuiAgentTqmbulkSource>();

      services.AddSingleton<ITqmbulkSource>(sp =>
      {
        var options = sp.GetRequiredService<IOptions<SapOptions>>().Value;

        return options.Mode.Equals("Gui", StringComparison.OrdinalIgnoreCase)
          ? sp.GetRequiredService<SapGuiAgentTqmbulkSource>()
          : sp.GetRequiredService<SapRfcTqmbulkSource>();
      });

      return services;
    }
  }
}
