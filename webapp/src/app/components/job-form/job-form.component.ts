import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { JobService } from '../../services/job.service';
import { CreateJobDto, JobDefinition } from '../../models/job.model';

@Component({
  selector: 'app-job-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './job-form.component.html',
  styleUrls: ['./job-form.component.scss']
})
export class JobFormComponent implements OnInit {
  job: CreateJobDto = {
    name: '',
    operationCode: '',
    command: '',
    arguments: '',
    workingDirectory: '',
    showWindow: false,
    environment: {},
    scheduleType: 'MINUTES',
    intervalMinutes: 5,
    enabled: true
  };

  isEditMode = false;
  jobId?: number;
  loading = false;
  error = '';
  success = '';

  selectedFile?: File;
  uploadProgress = 0;
  showFileUpload = true;

  environmentKeys: string[] = [];
  environmentValues: string[] = [];

  constructor(
    private jobService: JobService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      if (params['id']) {
        this.isEditMode = true;
        this.jobId = +params['id'];
        this.loadJob();
      }
    });
  }

  loadJob(): void {
    if (!this.jobId) return;

    this.loading = true;
    this.jobService.getJobById(this.jobId).subscribe({
      next: (job) => {
        this.job = {
          name: job.name,
          operationCode: job.operationCode,
          command: job.command,
          arguments: job.arguments,
          workingDirectory: job.workingDirectory,
          showWindow: job.showWindow,
          environment: job.environment,
          scheduleType: job.scheduleType,
          intervalMinutes: job.intervalMinutes,
          runAtTime: job.runAtTime,
          daysOfWeekMask: job.daysOfWeekMask,
          enabled: job.enabled
        };

        // Load environment variables
        if (job.environment) {
          this.environmentKeys = Object.keys(job.environment);
          this.environmentValues = Object.values(job.environment);
        }

        this.loading = false;
      },
      error: (err) => {
        this.error = 'Error al cargar la tarea: ' + err.message;
        this.loading = false;
      }
    });
  }

  onFileSelected(event: any): void {
    const file: File = event.target.files[0];
    if (file) {
      if (!file.name.endsWith('.zip')) {
        this.error = 'Solo se permiten archivos ZIP';
        return;
      }
      this.selectedFile = file;
    }
  }

  uploadFile(): void {
    if (!this.selectedFile || !this.job.name) {
      this.error = 'Debes proporcionar un nombre de tarea y seleccionar un archivo';
      return;
    }

    this.loading = true;
    this.uploadProgress = 0;

    this.jobService.uploadZipFile(this.selectedFile, this.job.name, this.jobId).subscribe({
      next: (result) => {
        if (result.success) {
          this.success = `Archivo cargado exitosamente. ${result.extractedFiles.length} archivos extraídos.`;
          this.job.workingDirectory = result.extractedPath;

          // Auto-detect .exe file if only one exists
          const exeFiles = result.extractedFiles.filter(f => f.toLowerCase().endsWith('.exe'));
          if (exeFiles.length === 1) {
            this.job.command = result.extractedPath + '\\' + exeFiles[0];
          }
        } else {
          this.error = 'Error al cargar archivo: ' + result.errorMessage;
        }
        this.loading = false;
        this.uploadProgress = 100;
      },
      error: (err) => {
        this.error = 'Error al cargar archivo: ' + err.message;
        this.loading = false;
      }
    });
  }

  addEnvironmentVariable(): void {
    this.environmentKeys.push('');
    this.environmentValues.push('');
  }

  removeEnvironmentVariable(index: number): void {
    this.environmentKeys.splice(index, 1);
    this.environmentValues.splice(index, 1);
  }

  onSubmit(): void {
    this.error = '';
    this.success = '';

    if (!this.job.name || !this.job.operationCode) {
      this.error = 'Nombre y código de operación son requeridos';
      return;
    }

    // Build environment object
    const environment: { [key: string]: string } = {};
    for (let i = 0; i < this.environmentKeys.length; i++) {
      if (this.environmentKeys[i]) {
        environment[this.environmentKeys[i]] = this.environmentValues[i] || '';
      }
    }
    this.job.environment = Object.keys(environment).length > 0 ? environment : undefined;

    this.loading = true;

    if (this.isEditMode && this.jobId) {
      this.jobService.updateJob(this.jobId, this.job).subscribe({
        next: () => {
          this.success = 'Tarea actualizada exitosamente';
          this.loading = false;
          setTimeout(() => this.router.navigate(['/jobs']), 1500);
        },
        error: (err) => {
          this.error = 'Error al actualizar tarea: ' + err.message;
          this.loading = false;
        }
      });
    } else {
      this.jobService.createJob(this.job).subscribe({
        next: () => {
          this.success = 'Tarea creada exitosamente';
          this.loading = false;
          setTimeout(() => this.router.navigate(['/jobs']), 1500);
        },
        error: (err) => {
          this.error = 'Error al crear tarea: ' + err.message;
          this.loading = false;
        }
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/jobs']);
  }
}
