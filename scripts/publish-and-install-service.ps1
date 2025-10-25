param(
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [string]$OutputDir = 'C:\Apps\YCC.JobHost',
  [string]$ServiceName = 'YCC.JobHost',
  [string]$User = $null,
  [string]$Password = $null
)

Write-Host "Publicando JobHost ($Configuration, $Runtime) ..."
$publishCmd = "dotnet publish ..\src\JobHost\JobHost.csproj -c $Configuration -r $Runtime --self-contained false -o `"$OutputDir`""
cmd /c $publishCmd
if ($LASTEXITCODE -ne 0) { throw "Fallo la publicacion (exit $LASTEXITCODE)" }

$exe = Join-Path $OutputDir 'JobHost.exe'
if (-not (Test-Path $exe)) { throw "No se encontro $exe" }

Write-Host "Instalando servicio $ServiceName ..."
if ($User -and $Password) {
  sc.exe create $ServiceName binPath= "`"$exe`"" start= auto obj= "$User" password= "$Password" | Out-Null
} else {
  sc.exe create $ServiceName binPath= "`"$exe`"" start= auto | Out-Null
}
sc.exe failure $ServiceName reset= 86400 actions= restart/5000 | Out-Null
Write-Host "Instalado. Inicia con: sc start $ServiceName"
