using System.IO.Compression;
using Microsoft.Extensions.Options;

namespace YCC.SapAutomation.WebAPI.Services;

public class FileStorageOptions
{
    public string BaseStoragePath { get; set; } = "JobFiles";
}

public sealed class FileStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IOptions<FileStorageOptions> options, ILogger<FileStorageService> logger)
    {
        _baseStoragePath = options.Value.BaseStoragePath;
        _logger = logger;

        // Ensure base directory exists
        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<(bool Success, string ExtractedPath, List<string> Files, string? Error)> UploadAndExtractZipAsync(
        Stream fileStream,
        string fileName,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create job-specific directory
            var jobDirectory = Path.Combine(_baseStoragePath, SanitizeFileName(jobName));

            // Delete existing directory if it exists
            if (Directory.Exists(jobDirectory))
            {
                Directory.Delete(jobDirectory, recursive: true);
                _logger.LogInformation("Deleted existing directory for job: {JobName}", jobName);
            }

            Directory.CreateDirectory(jobDirectory);

            // Save ZIP temporarily
            var tempZipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            try
            {
                await using (var fileOutputStream = File.Create(tempZipPath))
                {
                    await fileStream.CopyToAsync(fileOutputStream, cancellationToken);
                }

                // Extract ZIP
                ZipFile.ExtractToDirectory(tempZipPath, jobDirectory, overwriteFiles: true);

                // Get list of extracted files
                var extractedFiles = Directory.GetFiles(jobDirectory, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(jobDirectory, f))
                    .ToList();

                _logger.LogInformation("Successfully extracted {Count} files for job {JobName} to {Path}",
                    extractedFiles.Count, jobName, jobDirectory);

                return (true, jobDirectory, extractedFiles, null);
            }
            finally
            {
                // Clean up temp ZIP
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting ZIP for job {JobName}: {Error}", jobName, ex.Message);
            return (false, string.Empty, new List<string>(), ex.Message);
        }
    }

    public Task<bool> DeleteJobFilesAsync(string jobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobDirectory = Path.Combine(_baseStoragePath, SanitizeFileName(jobName));

            if (Directory.Exists(jobDirectory))
            {
                Directory.Delete(jobDirectory, recursive: true);
                _logger.LogInformation("Deleted files for job: {JobName}", jobName);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting files for job {JobName}: {Error}", jobName, ex.Message);
            return Task.FromResult(false);
        }
    }

    public string GetJobExecutablePath(string jobName, string exeFileName)
    {
        var jobDirectory = Path.Combine(_baseStoragePath, SanitizeFileName(jobName));
        return Path.Combine(jobDirectory, exeFileName);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
