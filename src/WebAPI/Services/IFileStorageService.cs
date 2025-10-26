namespace YCC.SapAutomation.WebAPI.Services;

public interface IFileStorageService
{
    Task<(bool Success, string ExtractedPath, List<string> Files, string? Error)> UploadAndExtractZipAsync(
        Stream fileStream,
        string fileName,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteJobFilesAsync(string jobName, CancellationToken cancellationToken = default);

    string GetJobExecutablePath(string jobName, string exeFileName);
}
