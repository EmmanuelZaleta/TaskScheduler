param(
  [string]$ServiceName = "YZ.JobHost",
  [string]$ExePath = "C:\Apps\YZ.JobHost\JobHost.exe",
  [string]$User = ".\svc_yzjob",
  [string]$Password = "P@ssw0rd!"
)

Write-Host "Creando servicio $ServiceName..."
sc.exe create $ServiceName binPath= "\"$ExePath\"" start= auto obj= "$User" password= "$Password"
sc.exe failure $ServiceName reset= 86400 actions= restart/5000
Write-Host "Hecho. Usa 'services.msc' para iniciarlo o 'sc start $ServiceName'"
