using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.WebAPI.DTOs;

namespace YCC.SapAutomation.WebAPI.Services;

public interface IJobManagementService
{
    Task<IEnumerable<JobDefinitionDto>> GetAllJobsAsync(CancellationToken cancellationToken = default);
    Task<JobDefinitionDto?> GetJobByIdAsync(int jobId, CancellationToken cancellationToken = default);
    Task<JobDefinitionDto> CreateJobAsync(CreateJobDto createDto, CancellationToken cancellationToken = default);
    Task<JobDefinitionDto?> UpdateJobAsync(int jobId, UpdateJobDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteJobAsync(int jobId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobRunDto>> GetJobRunsAsync(int? jobId = null, int limit = 50, CancellationToken cancellationToken = default);
}
