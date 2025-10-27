using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using YCC.SapAutomation.Abstractions.Automation;
using YCC.SapAutomation.Application.Jobs.ExternalProcess;

namespace YCC.SapAutomation.Application.Automation
{
  public sealed class AutomationJobFactory : IDisposable
  {
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AutomationJobFactory> _logger;
    // NOTA: Los assemblies cargados en AssemblyLoadContext.Default no pueden ser descargados
    // Esto significa que permanecerán en memoria durante toda la vida de la aplicación.
    // Para aplicaciones de larga duración, considerar usar AssemblyLoadContext coleccionables.
    private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new();

    public AutomationJobFactory(
      IHostEnvironment environment,
      ILogger<AutomationJobFactory> logger)
    {
      _environment = environment;
      _logger = logger;
    }

    public IJobDetail CreateJobDetail(AutomationManifest manifest)
    {
      return manifest.Kind switch
      {
        AutomationKind.DotNet => JobBuilder.Create(ResolveDotNetType(manifest))
          .WithIdentity(manifest.Name)
          .WithDescription(manifest.Description)
          .Build(),
        AutomationKind.ExternalProcess => JobBuilder.Create<ExternalProcessJob>()
          .WithIdentity(manifest.Name)
          .WithDescription(manifest.Description)
          .UsingJobData(ExternalProcessJob.CommandKey, EnsureNotNull(manifest.Command, manifest.Name, "command"))
          .UsingJobData(ExternalProcessJob.ArgumentsKey, manifest.Arguments ?? string.Empty)
          .UsingJobData(ExternalProcessJob.WorkingDirectoryKey, manifest.WorkingDirectory ?? ResolveDefaultWorkingDirectory(manifest))
          .UsingJobData(ExternalProcessJob.ShowWindowKey, (manifest.ShowWindow ?? false).ToString())
          .UsingJobData(ExternalProcessJob.EnvironmentKey, manifest.Environment is null
            ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(manifest.Environment))
          .UsingJobData(ExternalProcessJob.ResourceTypeKey, manifest.ResourceType ?? string.Empty)
          .Build(),
        _ => throw new NotSupportedException($"Tipo de automatizacion no soportado: {manifest.Kind}")
      };
    }

    private Type ResolveDotNetType(AutomationManifest manifest)
    {
      if (string.IsNullOrWhiteSpace(manifest.Type))
        throw new InvalidOperationException($"El manifiesto {manifest.Name} no especifica el tipo del job.");

      if (!string.IsNullOrWhiteSpace(manifest.AssemblyPath))
      {
        var absolutePath = ResolveAbsolutePath(manifest.AssemblyPath);
        var assembly = _assemblyCache.GetOrAdd(absolutePath, path =>
        {
          _logger.LogDebug("Cargando ensamblado de automatizacion desde {Path}", path);
          return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        });

        var type = assembly.GetType(manifest.Type, throwOnError: false, ignoreCase: false);
        if (type == null)
          throw new InvalidOperationException($"No se encontro el tipo {manifest.Type} dentro de {absolutePath}.");
        EnsureJobType(type, manifest.Name);
        return type;
      }

      var resolved = Type.GetType(manifest.Type, throwOnError: false, ignoreCase: false);
      if (resolved == null)
        throw new InvalidOperationException($"No se pudo cargar el tipo {manifest.Type}. Asegurate de usar el nombre de tipo totalmente calificado (Namespace.Clase, Ensamblado).");

      EnsureJobType(resolved, manifest.Name);
      return resolved;
    }

    private string ResolveDefaultWorkingDirectory(AutomationManifest manifest)
    {
      if (!string.IsNullOrWhiteSpace(manifest.SourcePath))
      {
        return Path.GetDirectoryName(manifest.SourcePath)!;
      }

      return _environment.ContentRootPath;
    }

    private static void EnsureJobType(Type type, string manifestName)
    {
      if (!typeof(IJob).IsAssignableFrom(type))
        throw new InvalidOperationException($"El tipo {type.FullName} configurado para {manifestName} no implementa IJob.");
    }

    private static string EnsureNotNull(string? value, string manifestName, string field)
    {
      if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"El manifiesto {manifestName} debe especificar {field}.");
      return value;
    }

    private string ResolveAbsolutePath(string path)
    {
      if (Path.IsPathRooted(path))
      {
        return path;
      }

      return Path.Combine(_environment.ContentRootPath, path);
    }

    public void Dispose()
    {
      // Limpiar el cache de assemblies
      // Nota: Los assemblies cargados en AssemblyLoadContext.Default no se pueden descargar,
      // pero al menos liberamos las referencias del diccionario
      _assemblyCache.Clear();
      _logger.LogDebug("Cache de assemblies limpiado");
    }
  }
}
