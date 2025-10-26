using Microsoft.AspNetCore.Mvc;
using YCC.SapAutomation.WebAPI.DTOs;
using YCC.SapAutomation.WebAPI.Services;

namespace YCC.SapAutomation.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobManagementService _jobService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobManagementService jobService, ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<JobDefinitionDto>>> GetAllJobs(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _jobService.GetAllJobsAsync(cancellationToken);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving jobs");
            return StatusCode(500, new { error = "Error retrieving jobs", message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobDefinitionDto>> GetJobById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobService.GetJobByIdAsync(id, cancellationToken);

            if (job == null)
            {
                return NotFound(new { error = "Job not found", jobId = id });
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job {JobId}", id);
            return StatusCode(500, new { error = "Error retrieving job", message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<JobDefinitionDto>> CreateJob(
        [FromBody] CreateJobDto createDto,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(createDto.Name))
            {
                return BadRequest(new { error = "Job name is required" });
            }

            if (string.IsNullOrWhiteSpace(createDto.OperationCode))
            {
                return BadRequest(new { error = "Operation code is required" });
            }

            var job = await _jobService.CreateJobAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetJobById), new { id = job.JobId }, job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job");
            return StatusCode(500, new { error = "Error creating job", message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<JobDefinitionDto>> UpdateJob(
        int id,
        [FromBody] UpdateJobDto updateDto,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobService.UpdateJobAsync(id, updateDto, cancellationToken);

            if (job == null)
            {
                return NotFound(new { error = "Job not found", jobId = id });
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {JobId}", id);
            return StatusCode(500, new { error = "Error updating job", message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteJob(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _jobService.DeleteJobAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound(new { error = "Job not found", jobId = id });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job {JobId}", id);
            return StatusCode(500, new { error = "Error deleting job", message = ex.Message });
        }
    }

    [HttpGet("{id}/runs")]
    public async Task<ActionResult<IEnumerable<JobRunDto>>> GetJobRuns(
        int id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken)
    {
        try
        {
            var runs = await _jobService.GetJobRunsAsync(id, limit, cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job runs for {JobId}", id);
            return StatusCode(500, new { error = "Error retrieving job runs", message = ex.Message });
        }
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IEnumerable<JobRunDto>>> GetAllJobRuns(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken)
    {
        try
        {
            var runs = await _jobService.GetJobRunsAsync(null, limit, cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all job runs");
            return StatusCode(500, new { error = "Error retrieving job runs", message = ex.Message });
        }
    }
}
