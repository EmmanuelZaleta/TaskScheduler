using Microsoft.AspNetCore.Mvc;
using YCC.SapAutomation.WebAPI.DTOs;
using YCC.SapAutomation.WebAPI.Services;

namespace YCC.SapAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IJobManagementService _jobService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileStorageService fileStorageService,
        IJobManagementService jobService,
        ILogger<FilesController> logger)
    {
        _fileStorageService = fileStorageService;
        _jobService = jobService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResultDto>> UploadZip(
        IFormFile file,
        [FromQuery] string jobName,
        [FromQuery] int? jobId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            if (string.IsNullOrWhiteSpace(jobName))
            {
                return BadRequest(new { error = "Job name is required" });
            }

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only ZIP files are supported" });
            }

            // Validate file size (max 100MB)
            const long maxFileSize = 100 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest(new { error = $"File size exceeds maximum of {maxFileSize / 1024 / 1024}MB" });
            }

            await using var stream = file.OpenReadStream();

            var (success, extractedPath, files, error) = await _fileStorageService.UploadAndExtractZipAsync(
                stream,
                file.FileName,
                jobName,
                cancellationToken);

            if (!success)
            {
                return StatusCode(500, new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = error ?? "Unknown error occurred"
                });
            }

            // If jobId is provided, update the job's working directory
            if (jobId.HasValue)
            {
                var updateDto = new UpdateJobDto
                {
                    WorkingDirectory = extractedPath
                };

                await _jobService.UpdateJobAsync(jobId.Value, updateDto, cancellationToken);
                _logger.LogInformation("Updated job {JobId} working directory to {Path}", jobId.Value, extractedPath);
            }

            return Ok(new FileUploadResultDto
            {
                FileName = file.FileName,
                ExtractedPath = extractedPath,
                ExtractedFiles = files,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new FileUploadResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    [HttpDelete("{jobName}")]
    public async Task<ActionResult> DeleteJobFiles(string jobName, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _fileStorageService.DeleteJobFilesAsync(jobName, cancellationToken);

            if (!deleted)
            {
                return NotFound(new { error = "Job files not found", jobName });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting files for job {JobName}", jobName);
            return StatusCode(500, new { error = "Error deleting job files", message = ex.Message });
        }
    }

    [HttpGet("{jobName}/executable")]
    public ActionResult<string> GetExecutablePath(
        string jobName,
        [FromQuery] string exeFileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exeFileName))
            {
                return BadRequest(new { error = "Executable file name is required" });
            }

            var path = _fileStorageService.GetJobExecutablePath(jobName, exeFileName);
            return Ok(new { executablePath = path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting executable path for job {JobName}", jobName);
            return StatusCode(500, new { error = "Error getting executable path", message = ex.Message });
        }
    }
}
