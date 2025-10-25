param(
  [ValidateSet('install','update','uninstall','run','status')]
  [string]$Action = 'install',

  [string]$TaskName         = 'YCC.AutomationCli',
  [string]$Project          = 'src/AutomationCli/YCC.SapAutomation.AutomationCli.csproj',
  [string]$OutputDir        = 'C:\Apps\YCC.AutomationCli',
  [string]$Configuration    = 'Release',
  [string]$Runtime          = 'win-x64',
  [switch]$SelfContained    = $false,

  # Usuario interactivo (opcional). Si no resuelve o va vacío, usamos el usuario actual de la sesión.
  [string]$User             = $env:USERNAME,

  [string]$Arguments        = '--respect-cron',
  [string]$WorkingDirectory = $null
)

$ErrorActionPreference = 'Stop'

# ---------- Helpers ----------
function Publish-Cli {
  param(
    [string]$project,
    [string]$config,
    [string]$rid,
    [string]$outDir,
    [bool]$selfContained
  )

  if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
  }

  $sc = if ($selfContained) { 'true' } else { 'false' }
  Write-Host "Publicando CLI => $outDir"

  $dotnetArgs = @('publish', $project, '-c', $config, '-r', $rid, '--self-contained', $sc, '-o', $outDir)
  $proc = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs -NoNewWindow -PassThru -Wait
  if ($proc.ExitCode -ne 0) { throw "dotnet publish falló ($($proc.ExitCode))" }
}

function Get-CliPath { param([string]$outDir) Join-Path $outDir 'YCC.SapAutomation.AutomationCli.exe' }

# Evita choque con $args automático y con parámetro $Action
function New-TaskAction {
  param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [string]$TaskArguments,
    [string]$WorkingDir
  )
  $supportsWD = (Get-Command New-ScheduledTaskAction).Parameters.ContainsKey('WorkingDirectory')
  $hasArgs = -not [string]::IsNullOrWhiteSpace($TaskArguments)

  if ($supportsWD -and $WorkingDir) {
    if     ($hasArgs) { return New-ScheduledTaskAction -Execute $Exe -Argument $TaskArguments -WorkingDirectory $WorkingDir }
    else              { return New-ScheduledTaskAction -Execute $Exe -WorkingDirectory $WorkingDir }
  } else {
    if     ($hasArgs) { return New-ScheduledTaskAction -Execute $Exe -Argument $TaskArguments }
    else              { return New-ScheduledTaskAction -Execute $Exe }
  }
}

# Resuelve usuario a formato aceptado por el Programador de Tareas; si falla, usa el usuario actual.
function Resolve-UserId {
  param([string]$UserInput)

  $current = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name   # "DOMINIO\usuario" o "EQUIPO\usuario"
  if ([string]::IsNullOrWhiteSpace($UserInput)) { return $current }

  $cands = @()
  if ($UserInput.Contains('\') -or $UserInput.Contains('@')) { $cands += $UserInput }
  $cands += "$env:USERDOMAIN\$UserInput"
  $cands += "$env:COMPUTERNAME\$UserInput"
  $cands += ".\$UserInput"

  foreach ($cand in ($cands | Select-Object -Unique)) {
    try {
      [void]([System.Security.Principal.NTAccount]$cand).Translate([System.Security.Principal.SecurityIdentifier])
      return $cand
    } catch { }
  }

  Write-Warning "No se pudo resolver el usuario '$UserInput'. Usaré el usuario actual '$current'."
  return $current
}

# ---------- Main ----------
switch ($Action) {
  'install' {
    Publish-Cli $Project $Configuration $Runtime $OutputDir $SelfContained.IsPresent

    $exe = Get-CliPath $OutputDir
    if (-not (Test-Path $exe)) { throw "No se encontró $exe" }
    if (-not $WorkingDirectory) { $WorkingDirectory = Split-Path $exe }

    $effectiveUser = Resolve-UserId $User

    $taskAction    = New-TaskAction -Exe $exe -TaskArguments $Arguments -WorkingDir $WorkingDirectory
    $taskTrigger   = New-ScheduledTaskTrigger -AtLogOn -User $effectiveUser
    # En PS 5.1 / Win10+ el nombre válido es "Interactive"
    $taskPrincipal = New-ScheduledTaskPrincipal -UserId $effectiveUser -LogonType Interactive -RunLevel Highest

    try { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction Stop } catch {}

    Register-ScheduledTask `
      -TaskName $TaskName `
      -Action $taskAction `
      -Trigger $taskTrigger `
      -Principal $taskPrincipal `
      -Description "YCC Automation CLI (modo residente)" | Out-Null

    # Ejecutar inmediatamente
    try { Start-ScheduledTask -TaskName $TaskName } catch { schtasks /Run /TN $TaskName | Out-Null }

    Start-Sleep -Seconds 2
    $info = Get-ScheduledTaskInfo -TaskName $TaskName
    Write-Host ("Tarea '{0}' instalada e iniciada. Estado={1}, ÚltimaEj={2}, Resultado={3}" -f `
                $TaskName, $info.State, $info.LastRunTime, $info.LastTaskResult)
  }

  'update' {
    Publish-Cli $Project $Configuration $Runtime $OutputDir $SelfContained.IsPresent
    Write-Host "CLI actualizado en $OutputDir. (Si la tarea ya existe, no es necesario recrearla)"
  }

  'uninstall' {
    try { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false } catch { Write-Warning $_ }
  }

  'run' {
    try { Start-ScheduledTask -TaskName $TaskName } catch { schtasks /Run /TN $TaskName | Out-Null }
  }

  'status' {
    schtasks /Query /TN $TaskName /V /FO LIST
  }
}
