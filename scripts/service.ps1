param(
  [ValidateSet('install','update','uninstall','restart','status','logs')]
  [string]$Action = 'install',

  [string]$ServiceName = 'YCC.JobHost',
  [string]$Project = 'src/JobHost/JobHost.csproj',
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [string]$OutputDir = 'C:\Apps\YCC.JobHost',
  [switch]$SelfContained = $false,

  [string]$User,
  [string]$Password,

  [string[]]$Preserve = @('appsettings*.json','automations\*.json','logs\*'),
  [int]$LogLines = 200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $p = New-Object Security.Principal.WindowsPrincipal($id)
  if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning 'Ejecuta esta consola como Administrador.'
  }
}

function Publish-Host([string]$project,[string]$config,[string]$rid,[string]$outDir,[bool]$selfContained){
  $sc = if($selfContained){'true'} else {'false'}
  Write-Host "Publicando: $project => $outDir"
  $args = @('publish', $project, '-c', $config, '-r', $rid, '--self-contained', $sc, '-o', $outDir)
  $psi = New-Object System.Diagnostics.ProcessStartInfo 'dotnet', ($args -join ' ')
  $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false
  $p = [System.Diagnostics.Process]::Start($psi)
  $p.WaitForExit()
  if($p.ExitCode -ne 0){ throw "dotnet publish fallo con codigo $($p.ExitCode)" }
}

function Service-Exists([string]$name){
  $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
  return $null -ne $svc
}

function Wait-ServiceStatus([string]$name,[string]$status,[int]$timeoutSec=30){
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  while($sw.Elapsed.TotalSeconds -lt $timeoutSec){
    $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
    if($svc -and $svc.Status.ToString().ToLower() -eq $status.ToLower()){ return }
    Start-Sleep -Milliseconds 300
  }
}

function Copy-TreePreserve($src,$dst,$patterns){
  New-Item -ItemType Directory -Force -Path $dst | Out-Null
  $files = Get-ChildItem -Path $src -Recurse -File
  foreach($f in $files){
    $rel = $f.FullName.Substring($src.Length).TrimStart('\\','/')
    $skip = $false
    foreach($pat in $patterns){ if($rel -like $pat){ $skip = $true; break } }
    $target = Join-Path $dst $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    if($skip -and (Test-Path $target)){ continue }
    Copy-Item $f.FullName $target -Force
  }
}

switch ($Action) {
  'install' {
    Assert-Admin
    Publish-Host $Project $Configuration $Runtime $OutputDir $SelfContained.IsPresent
    if(Service-Exists $ServiceName){ Write-Host "Servicio $ServiceName ya existe. Usa 'update' si solo quieres actualizar binarios."; break }
    $bin = Join-Path $OutputDir 'JobHost.exe'
    if(-not (Test-Path $bin)){ throw "No se encontro $bin" }
    if([string]::IsNullOrEmpty($User)){
      sc.exe create $ServiceName binPath= "`"$bin`"" start= auto | Out-Null
    } else {
      sc.exe create $ServiceName binPath= "`"$bin`"" start= auto obj= "$User" password= "$Password" | Out-Null
    }
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000 | Out-Null
    Write-Host "Servicio $ServiceName instalado. Inicia con: sc start $ServiceName"
  }
  'update' {
    Assert-Admin
    $staging = Join-Path $env:TEMP ("jobhost-publish-" + [Guid]::NewGuid().ToString('N'))
    Publish-Host $Project $Configuration $Runtime $staging $SelfContained.IsPresent
    if(Service-Exists $ServiceName){ sc.exe stop $ServiceName | Out-Null; Wait-ServiceStatus $ServiceName 'stopped' 60 }
    Copy-TreePreserve $staging $OutputDir $Preserve
    Remove-Item -Recurse -Force $staging
    if(Service-Exists $ServiceName){ sc.exe start $ServiceName | Out-Null }
    Write-Host "Actualizacion aplicada en $OutputDir"
  }
  'uninstall' {
    Assert-Admin
    if(Service-Exists $ServiceName){ sc.exe stop $ServiceName | Out-Null; Wait-ServiceStatus $ServiceName 'stopped' 60; sc.exe delete $ServiceName | Out-Null; Write-Host "Servicio $ServiceName eliminado." } else { Write-Host "Servicio $ServiceName no existe." }
  }
  'restart' {
    Assert-Admin
    if(Service-Exists $ServiceName){ sc.exe stop $ServiceName | Out-Null; Wait-ServiceStatus $ServiceName 'stopped' 60; sc.exe start $ServiceName | Out-Null } else { Write-Host "Servicio $ServiceName no existe." }
  }
  'status' {
    sc.exe query $ServiceName
  }
  'logs' {
    $log = Get-ChildItem (Join-Path $OutputDir 'logs\jobhost-*.log') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if($null -eq $log){ Write-Host "No hay logs en $OutputDir\logs"; break }
    Get-Content $log.FullName -Tail $LogLines -Wait
  }
  default { Write-Host "Accion no reconocida: $Action" }
}

