import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { JobService } from '../../services/job.service';
import { JobDefinition } from '../../models/job.model';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './job-list.component.html',
  styleUrls: ['./job-list.component.scss']
})
export class JobListComponent implements OnInit {
  jobs: JobDefinition[] = [];
  loading = false;
  error = '';

  constructor(
    private jobService: JobService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.loading = true;
    this.error = '';

    this.jobService.getAllJobs().subscribe({
      next: (jobs) => {
        this.jobs = jobs;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error al cargar las tareas: ' + err.message;
        this.loading = false;
      }
    });
  }

  createNewJob(): void {
    this.router.navigate(['/jobs/new']);
  }

  editJob(jobId: number): void {
    this.router.navigate(['/jobs/edit', jobId]);
  }

  deleteJob(job: JobDefinition): void {
    if (!confirm(`¿Estás seguro de eliminar la tarea "${job.name}"?`)) {
      return;
    }

    this.jobService.deleteJob(job.jobId).subscribe({
      next: () => {
        this.loadJobs();
      },
      error: (err) => {
        this.error = 'Error al eliminar tarea: ' + err.message;
      }
    });
  }

  toggleJobEnabled(job: JobDefinition): void {
    const updateDto = { enabled: !job.enabled };

    this.jobService.updateJob(job.jobId, updateDto).subscribe({
      next: () => {
        this.loadJobs();
      },
      error: (err) => {
        this.error = 'Error al actualizar tarea: ' + err.message;
      }
    });
  }

  viewJobRuns(jobId: number): void {
    this.router.navigate(['/jobs', jobId, 'runs']);
  }

  getScheduleDescription(job: JobDefinition): string {
    switch (job.scheduleType) {
      case 'MINUTES':
        return `Cada ${job.intervalMinutes} minutos`;
      case 'DAILY':
        return `Diaria a las ${job.runAtTime}`;
      case 'WEEKLY':
        return `Semanal - ${this.getDaysFromMask(job.daysOfWeekMask || 0)}`;
      default:
        return job.scheduleType;
    }
  }

  getDaysFromMask(mask: number): string {
    const days = [];
    const dayNames = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

    for (let i = 0; i < 7; i++) {
      if (mask & (1 << i)) {
        days.push(dayNames[i]);
      }
    }

    return days.length > 0 ? days.join(', ') : 'Ninguno';
  }
}
