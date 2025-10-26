import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { JobService } from '../../services/job.service';
import { JobDefinition, JobRun } from '../../models/job.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  jobs: JobDefinition[] = [];
  recentRuns: JobRun[] = [];
  loading = false;
  error = '';

  // Statistics
  totalJobs = 0;
  enabledJobs = 0;
  disabledJobs = 0;
  runsToday = 0;
  failedToday = 0;
  successToday = 0;

  constructor(private jobService: JobService) { }

  ngOnInit(): void {
    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.loading = true;
    this.error = '';

    // Load jobs
    this.jobService.getAllJobs().subscribe({
      next: (jobs) => {
        this.jobs = jobs;
        this.calculateStatistics();
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error al cargar datos: ' + err.message;
        this.loading = false;
      }
    });

    // Load recent runs
    this.jobService.getJobRuns(undefined, 20).subscribe({
      next: (runs) => {
        this.recentRuns = runs;
        this.calculateRunStatistics(runs);
      },
      error: (err) => {
        console.error('Error loading runs:', err);
      }
    });
  }

  calculateStatistics(): void {
    this.totalJobs = this.jobs.length;
    this.enabledJobs = this.jobs.filter(j => j.enabled).length;
    this.disabledJobs = this.jobs.filter(j => !j.enabled).length;
  }

  calculateRunStatistics(runs: JobRun[]): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const todayRuns = runs.filter(r => {
      const runDate = new Date(r.startedUtc);
      runDate.setHours(0, 0, 0, 0);
      return runDate.getTime() === today.getTime();
    });

    this.runsToday = todayRuns.length;
    this.successToday = todayRuns.filter(r => r.status === 'Completed').length;
    this.failedToday = todayRuns.filter(r => r.status === 'Failed').length;
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'completed':
        return 'status-success';
      case 'failed':
        return 'status-error';
      case 'running':
        return 'status-running';
      default:
        return 'status-pending';
    }
  }

  formatDuration(run: JobRun): string {
    if (!run.finishedUtc) {
      return 'En ejecución...';
    }

    const start = new Date(run.startedUtc).getTime();
    const end = new Date(run.finishedUtc).getTime();
    const durationMs = end - start;

    const seconds = Math.floor(durationMs / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    } else {
      return `${seconds}s`;
    }
  }

  formatDateTime(date: Date | string): string {
    const d = new Date(date);
    return d.toLocaleString('es-ES');
  }
}
