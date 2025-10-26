import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { JobDefinition, CreateJobDto, UpdateJobDto, JobRun, FileUploadResult } from '../models/job.model';

@Injectable({
  providedIn: 'root'
})
export class JobService {
  private apiUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) { }

  // Job Management
  getAllJobs(): Observable<JobDefinition[]> {
    return this.http.get<JobDefinition[]>(`${this.apiUrl}/jobs`);
  }

  getJobById(id: number): Observable<JobDefinition> {
    return this.http.get<JobDefinition>(`${this.apiUrl}/jobs/${id}`);
  }

  createJob(job: CreateJobDto): Observable<JobDefinition> {
    return this.http.post<JobDefinition>(`${this.apiUrl}/jobs`, job);
  }

  updateJob(id: number, job: UpdateJobDto): Observable<JobDefinition> {
    return this.http.put<JobDefinition>(`${this.apiUrl}/jobs/${id}`, job);
  }

  deleteJob(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/jobs/${id}`);
  }

  // Job Runs
  getJobRuns(jobId?: number, limit: number = 50): Observable<JobRun[]> {
    if (jobId) {
      return this.http.get<JobRun[]>(`${this.apiUrl}/jobs/${jobId}/runs`, {
        params: new HttpParams().set('limit', limit.toString())
      });
    } else {
      return this.http.get<JobRun[]>(`${this.apiUrl}/jobs/runs`, {
        params: new HttpParams().set('limit', limit.toString())
      });
    }
  }

  // File Upload
  uploadZipFile(file: File, jobName: string, jobId?: number): Observable<FileUploadResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    let params = new HttpParams().set('jobName', jobName);
    if (jobId) {
      params = params.set('jobId', jobId.toString());
    }

    return this.http.post<FileUploadResult>(`${this.apiUrl}/files/upload`, formData, { params });
  }

  deleteJobFiles(jobName: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/files/${jobName}`);
  }

  getExecutablePath(jobName: string, exeFileName: string): Observable<{ executablePath: string }> {
    const params = new HttpParams().set('exeFileName', exeFileName);
    return this.http.get<{ executablePath: string }>(`${this.apiUrl}/files/${jobName}/executable`, { params });
  }
}
