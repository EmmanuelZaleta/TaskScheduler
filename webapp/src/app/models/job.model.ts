export interface JobDefinition {
  jobId: number;
  name: string;
  operationCode: string;
  command?: string;
  arguments?: string;
  workingDirectory?: string;
  showWindow: boolean;
  environment?: { [key: string]: string };
  scheduleType: string;
  intervalMinutes?: number;
  runAtTime?: string;
  daysOfWeekMask?: number;
  enabled: boolean;
}

export interface CreateJobDto {
  name: string;
  operationCode: string;
  command?: string;
  arguments?: string;
  workingDirectory?: string;
  showWindow: boolean;
  environment?: { [key: string]: string };
  scheduleType: string;
  intervalMinutes?: number;
  runAtTime?: string;
  daysOfWeekMask?: number;
  enabled: boolean;
}

export interface UpdateJobDto {
  name?: string;
  operationCode?: string;
  command?: string;
  arguments?: string;
  workingDirectory?: string;
  showWindow?: boolean;
  environment?: { [key: string]: string };
  scheduleType?: string;
  intervalMinutes?: number;
  runAtTime?: string;
  daysOfWeekMask?: number;
  enabled?: boolean;
}

export interface JobRun {
  jobRunId: number;
  jobName: string;
  correlationId: string;
  startedUtc: Date;
  finishedUtc?: Date;
  status: string;
  message?: string;
}

export interface FileUploadResult {
  fileName: string;
  extractedPath: string;
  extractedFiles: string[];
  success: boolean;
  errorMessage?: string;
}
