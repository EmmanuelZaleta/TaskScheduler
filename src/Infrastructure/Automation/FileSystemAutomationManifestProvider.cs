using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Application.Automation;
using YCC.SapAutomation.Application.Options;

namespace YCC.SapAutomation.Infrastructure.Automation
{
  public sealed class FileSystemAutomationManifestProvider : IAutomationManifestProvider
  {
    private readonly AutomationOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FileSystemAutomationManifestProvider> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
      PropertyNameCaseInsensitive = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    };

    public FileSystemAutomationManifestProvider(
      IOptions<AutomationOptions> options,
      IHostEnvironment environment,
      ILogger<FileSystemAutomationManifestProvider> logger)
    {
      _options = options.Value;
      _environment = environment;
      _logger = logger;
      _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<IReadOnlyCollection<AutomationManifest>> LoadAsync(CancellationToken cancellationToken = default)
    {
      var manifestsPath = ResolvePath(_options.ManifestsPath);
      if (!Directory.Exists(manifestsPath))
      {
        _logger.LogWarning("La carpeta de manifiestos {Path} no existe.", manifestsPath);
        return Array.Empty<AutomationManifest>();
      }

      var manifests = new List<AutomationManifest>();
      foreach (var file in Directory.EnumerateFiles(manifestsPath, "*.json", SearchOption.TopDirectoryOnly))
      {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
          await using var stream = File.OpenRead(file);
          var manifest = await JsonSerializer.DeserializeAsync<AutomationManifest>(stream, _serializerOptions, cancellationToken);
          if (manifest is null)
          {
            _logger.LogWarning("El manifiesto {File} no pudo deserializarse.", file);
            continue;
          }

          manifest = manifest with
          {
            SourcePath = file,
            AssemblyPath = NormalizeOptionalPath(manifest.AssemblyPath, file),
            WorkingDirectory = NormalizeOptionalPath(manifest.WorkingDirectory, file),
            Command = NormalizeOptionalPath(manifest.Command, file)
          };

          if (string.IsNullOrWhiteSpace(manifest.Name))
          {
            manifest = manifest with { Name = Path.GetFileNameWithoutExtension(file) };
          }

          manifests.Add(manifest);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "No se pudo leer el manifiesto {File}.", file);
        }
      }

      return manifests;
    }

    private string ResolvePath(string path)
    {
      return Path.IsPathRooted(path)
        ? path
        : Path.Combine(_environment.ContentRootPath, path);
    }

    private static string? NormalizeOptionalPath(string? path, string manifestFile)
    {
      if (string.IsNullOrWhiteSpace(path))
      {
        return path;
      }

      if (Path.IsPathRooted(path))
      {
        return path;
      }

      var manifestDirectory = Path.GetDirectoryName(manifestFile);
      return string.IsNullOrEmpty(manifestDirectory)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(manifestDirectory, path));
    }
  }
}
