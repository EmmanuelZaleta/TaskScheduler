Automatizaciones externas
==========================

La capa de orquestacion del servicio lee archivos JSON dentro de la carpeta `automations/`
y registra cada job en Quartz en tiempo de arranque. Gracias a esto, para agregar o
modificar automatizaciones no hace falta recompilar: basta con crear o editar un
manifiesto.

Estructura basica del manifiesto
--------------------------------

```jsonc
{
  "name": "NombreUnico",
  "description": "Texto opcional",
  "cron": "0 0/5 * * * ?",
  "kind": "DotNet | ExternalProcess",
  "type": "Namespace.Clase, Ensamblado",   // requerido cuando kind = DotNet
  "command": "ruta/al/script.cmd",         // requerido cuando kind = ExternalProcess
  "arguments": "--lo-que-necesites",
  "workingDirectory": "..",
  "environment": { "KEY": "VALUE" },
  "enabled": true
}
```

Reglas importantes:

- Las rutas relativas (`command`, `workingDirectory`, `assemblyPath`) se normalizan con
  respecto al archivo de manifiesto. Si apuntas a `../scripts/foo.cmd`, el servicio
  resolvera la ruta absoluta en tiempo de arranque.
- Para jobs externos se serializa el diccionario `environment` y se pasa al proceso
  como variables de entorno.
- `cron` acepta expresiones Quartz. Puedes validar expresiones en
  https://www.cronmaker.com/ o herramientas similares.

Ejecutar manifiestos con el CLI
-------------------------------

Cuando quieres disparar una automatizacion fuera del servicio Windows (por ejemplo
desde un scheduler externo), puedes usar el proyecto `src/AutomationCli`. El manifiesto
`automations/sample-manifest.json` es un ejemplo que ejecuta un script de prueba a traves
del comando `scripts/run-sample-automation.cmd`:

```json
{
  "name": "SampleAutomation",
  "description": "Ejecuta un proceso externo de ejemplo mediante el Automation CLI.",
  "cron": "0 0/5 * * * ?",
  "kind": "ExternalProcess",
  "command": "../scripts/run-sample-automation.cmd",
  "workingDirectory": "..",
  "enabled": true
}
```

El script detecta el ejecutable publicado (`YCC.SapAutomation.AutomationCli.exe`) y,
si no recibe argumentos, invoca el manifiesto `automations/sample-manifest.json`.
Puedes sobrescribir la ruta del ejecutable via `AUTOMATION_CLI_EXE` o pasar cualquier
argumento admitido por el CLI (por ejemplo `--manifest otra.json --respect-cron`).

```cmd
set "AUTOMATION_CLI_EXE=C:\Tools\AutomationCli\YCC.SapAutomation.AutomationCli.exe"
set "PIPELINE_ARGS=--run Sample --from 2024-10-01 --to 2024-10-02"
```

Luego reinicia el servicio Windows (o ejecuta la aplicacion en consola) para que la
automatizacion quede registrada. Si prefieres no reiniciar, puedes detener/iniciar la
aplicacion manualmente, ya que Quartz relee manifiestos en el arranque.

Agregar mas pipelines externas
-------------------------------

1. Publica el proyecto CLI: `dotnet publish src/AutomationCli -c Release -r win-x64`.
   (puedes cambiar el RuntimeIdentifier segun el servidor donde se ejecute).
2. Copia `automations/sample-manifest.json` y renombralo (por ejemplo
   `automations/mi-pipeline.json`).
3. Ajusta el `cron`, `command`, `arguments` y `description` segun corresponda. El script
   por defecto invoca el ejecutable publicado en `src\\AutomationCli\\bin\\Release\\...`.
4. Si la pipeline requiere parametros especiales, pasalos al script (se reenvian al exe).
5. Reinicia el host para que los manifiestos se vuelvan a registrar.

Si necesitas ejecutar varios pasos en cascada, puedes preparar un script `.cmd` o `.ps1`
que encadene las llamadas y referenciarlo en el manifiesto.

Observa que la infraestructura sigue siendo la misma: Quartz garantiza el control de
concurrencia (`Scheduler.MaxConcurrency`), el host aplica la politica de logs via
Serilog, y los procesos externos heredaran las variables de entorno del servicio.

Linea de comandos del Automation CLI
------------------------------------

El CLI acepta estas opciones:

- `--manifest <ruta>`: agrega un manifiesto especifico (puedes repetir la opcion para varios archivos).
- `--manifestsPath <carpeta>`: sobreescribe la carpeta desde la cual se leen manifiestos.
- `--respect-cron`: en lugar de ejecutar "run once", registra los jobs respetando su cron y deja el proceso corriendo.

Ejemplos:

- Ejecutar un manifiesto puntual y terminar:
  `YCC.SapAutomation.AutomationCli.exe --manifest "C:\manifests\sample.json"`

- Ejecutar todo lo que este en una carpeta de manifiestos:
  `YCC.SapAutomation.AutomationCli.exe --manifestsPath "C:\manifests"`

- Registrar con cron (daemon):
  `YCC.SapAutomation.AutomationCli.exe --manifestsPath "C:\manifests" --respect-cron`
