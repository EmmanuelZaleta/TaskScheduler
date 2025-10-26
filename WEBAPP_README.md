# Task Scheduler - Aplicación Web

Aplicación web desarrollada con Angular y ASP.NET Core WebAPI para la gestión de tareas programadas con estilo visual WPF.

## Características Principales

✨ **Gestión Completa de Tareas**
- Crear, editar, eliminar y visualizar tareas programadas
- Configurar programaciones (por minutos, diaria, semanal)
- Habilitar/deshabilitar tareas sin eliminarlas
- Ver historial de ejecuciones

📦 **Carga de Archivos**
- Subir archivos ZIP con ejecutables
- Extracción automática en carpetas organizadas
- Detección automática de archivos .exe
- Gestión de directorios de trabajo

🎨 **Estilo Visual WPF**
- Interfaz inspirada en Windows Presentation Foundation
- Gradientes y bordes característicos de WPF
- Paleta de colores profesional
- Componentes tipo GroupBox, DataGrid, etc.

📊 **Dashboard de Monitoreo**
- Estadísticas en tiempo real
- Tareas activas/inactivas
- Ejecuciones del día
- Historial de ejecuciones recientes

## Estructura del Proyecto

```
TaskScheduler/
├── src/
│   ├── WebAPI/                    # API REST ASP.NET Core
│   │   ├── Controllers/           # Controladores de la API
│   │   │   ├── JobsController.cs
│   │   │   └── FilesController.cs
│   │   ├── Services/              # Servicios de negocio
│   │   │   ├── JobManagementService.cs
│   │   │   └── FileStorageService.cs
│   │   ├── DTOs/                  # Data Transfer Objects
│   │   ├── Program.cs
│   │   └── appsettings.json
│   └── ...                        # Otros proyectos existentes
├── webapp/                        # Aplicación Angular
│   ├── src/
│   │   ├── app/
│   │   │   ├── components/        # Componentes Angular
│   │   │   │   ├── dashboard/
│   │   │   │   ├── job-list/
│   │   │   │   └── job-form/
│   │   │   ├── models/            # Modelos TypeScript
│   │   │   ├── services/          # Servicios HTTP
│   │   │   └── styles.scss        # Estilos WPF globales
│   │   └── ...
├── sql/
│   └── 01_schema.sql              # Schema de base de datos (actualizado)
└── JobFiles/                      # Archivos cargados (generado automáticamente)
```

## Requisitos Previos

### Backend (WebAPI)
- .NET 8.0 SDK
- SQL Server (local o remoto)
- Visual Studio 2022 o VS Code

### Frontend (Angular)
- Node.js 18+ y npm
- Angular CLI 17

## Instalación y Configuración

### 1. Configurar Base de Datos

Ejecuta el script SQL para crear las tablas necesarias:

```bash
# En SQL Server Management Studio o Azure Data Studio
# Ejecutar: sql/01_schema.sql
```

El script creará las siguientes tablas:
- `Job` - Definiciones de tareas
- `JobSchedule` - Configuración de programación
- `JobRuns` - Historial de ejecuciones
- `JobParam` - Parámetros de configuración
- `ExcludedHU` - Unidades de manejo excluidas

### 2. Configurar WebAPI

1. Editar `src/WebAPI/appsettings.json` con tu cadena de conexión:

```json
{
  "ConnectionStrings": {
    "Sql": "Data Source=TU_SERVIDOR;Initial Catalog=NCDR_YCC_Moldeo;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
  },
  "FileStorage": {
    "BasePath": "JobFiles"
  }
}
```

2. Compilar y ejecutar la API:

```bash
cd src/WebAPI
dotnet restore
dotnet run
```

La API estará disponible en:
- **HTTP**: http://localhost:5000
- **Swagger UI**: http://localhost:5000 (documentación interactiva)

### 3. Configurar Aplicación Angular

1. Instalar dependencias:

```bash
cd webapp
npm install
```

2. Si necesitas cambiar la URL de la API, edita `webapp/src/app/services/job.service.ts`:

```typescript
private apiUrl = 'http://localhost:5000/api';
```

3. Ejecutar la aplicación:

```bash
npm start
# o
ng serve
```

La aplicación estará disponible en: **http://localhost:4200**

## Uso de la Aplicación

### Crear una Nueva Tarea

1. **Navegar a "Nueva Tarea"** desde el menú superior
2. **Completar la información general**:
   - Nombre de la tarea (único)
   - Código de operación
   - Estado (Habilitada/Deshabilitada)

3. **Cargar archivo ejecutable (opcional)**:
   - Clic en "Seleccionar Archivo"
   - Elegir un archivo ZIP que contenga tu ejecutable
   - Clic en "Cargar Archivo"
   - El sistema extraerá automáticamente los archivos
   - Si hay un solo .exe, se configurará automáticamente

4. **Configurar ejecución**:
   - Comando/Ejecutable: ruta al .exe o comando
   - Argumentos: parámetros del comando
   - Directorio de trabajo: se configura automáticamente al cargar ZIP
   - Mostrar ventana: si quieres ver la ventana del proceso

5. **Programar la tarea**:
   - **Minutos**: ejecutar cada X minutos
   - **Diaria**: ejecutar a una hora específica cada día
   - **Semanal**: ejecutar ciertos días de la semana

6. **Variables de entorno (opcional)**:
   - Agregar variables que necesite tu proceso
   - Ejemplo: `API_KEY=valor`, `ENV=production`

7. **Guardar** la tarea

### Gestionar Tareas

**Lista de Tareas** (`/jobs`):
- Ver todas las tareas configuradas
- **Editar**: modificar configuración
- **Activar/Desactivar**: cambiar estado sin eliminar
- **Historial**: ver ejecuciones pasadas
- **Eliminar**: borrar tarea permanentemente

**Dashboard** (`/dashboard`):
- Ver estadísticas generales
- Total de tareas
- Tareas activas/inactivas
- Ejecuciones del día (exitosas/fallidas)
- Historial de ejecuciones recientes

## API Endpoints

### Jobs

```
GET    /api/jobs              # Obtener todas las tareas
GET    /api/jobs/{id}         # Obtener tarea por ID
POST   /api/jobs              # Crear nueva tarea
PUT    /api/jobs/{id}         # Actualizar tarea
DELETE /api/jobs/{id}         # Eliminar tarea
GET    /api/jobs/{id}/runs    # Obtener ejecuciones de una tarea
GET    /api/jobs/runs         # Obtener todas las ejecuciones
```

### Files

```
POST   /api/files/upload      # Subir archivo ZIP
DELETE /api/files/{jobName}   # Eliminar archivos de una tarea
GET    /api/files/{jobName}/executable  # Obtener ruta del ejecutable
```

## Ejemplos de Uso

### Ejemplo 1: Tarea que ejecuta un script PowerShell

```json
{
  "name": "BackupDatabase",
  "operationCode": "BACKUP_DB",
  "command": "powershell.exe",
  "arguments": "-File C:\\Scripts\\backup.ps1",
  "workingDirectory": "C:\\Scripts",
  "showWindow": false,
  "scheduleType": "DAILY",
  "runAtTime": "02:00:00",
  "enabled": true
}
```

### Ejemplo 2: Tarea con ejecutable cargado desde ZIP

1. Crear un ZIP con tu aplicación:
   ```
   MiApp.zip
   ├── MiApp.exe
   ├── config.json
   └── libs/
       └── dependencias.dll
   ```

2. Crear la tarea con nombre "MiApp"
3. Cargar el ZIP a través de la interfaz
4. El sistema:
   - Crea carpeta `JobFiles/MiApp/`
   - Extrae todos los archivos
   - Configura automáticamente `command` como `JobFiles/MiApp/MiApp.exe`
   - Configura `workingDirectory` como `JobFiles/MiApp/`

### Ejemplo 3: Tarea con variables de entorno

```json
{
  "name": "APISync",
  "operationCode": "API_SYNC",
  "command": "C:\\Apps\\ApiSync.exe",
  "environment": {
    "API_URL": "https://api.example.com",
    "API_KEY": "secret-key-123",
    "TIMEOUT": "30000"
  },
  "scheduleType": "MINUTES",
  "intervalMinutes": 15,
  "enabled": true
}
```

## Programación de Tareas

### Por Minutos
- Ejecuta cada X minutos
- Ejemplo: `intervalMinutes: 5` → cada 5 minutos

### Diaria
- Ejecuta a una hora específica cada día
- Ejemplo: `runAtTime: "14:30"` → todos los días a las 2:30 PM

### Semanal
- Ejecuta ciertos días de la semana usando una máscara de bits
- Valores:
  - 1 = Lunes
  - 2 = Martes
  - 4 = Miércoles
  - 8 = Jueves
  - 16 = Viernes
  - 32 = Sábado
  - 64 = Domingo

- Ejemplo: `daysOfWeekMask: 31` (1+2+4+8+16) → Lunes a Viernes

## Características del Estilo WPF

La aplicación Angular implementa un sistema de estilos que replica la apariencia de WPF:

### Componentes Disponibles

- **wpf-window**: Ventana principal con borde y sombra
- **wpf-title-bar**: Barra de título con gradiente
- **wpf-button**: Botones con variantes (primary, secondary, success, warning, danger)
- **wpf-textbox**: Campos de texto estilo WPF
- **wpf-combobox**: Select/dropdown
- **wpf-checkbox**: Checkbox estándar
- **wpf-group-box**: Agrupador de controles con header
- **wpf-datagrid**: Tablas estilo DataGrid
- **wpf-toolbar**: Barra de herramientas
- **wpf-alert**: Alertas/notificaciones
- **wpf-progress-bar**: Barra de progreso

### Paleta de Colores

- **Principal**: #5b9bd5 (azul WPF clásico)
- **Éxito**: #5cb85c
- **Advertencia**: #f0ad4e
- **Error**: #d9534f
- **Fondo**: #f0f0f0
- **Bordes**: #acacac, #d0d0d0

## Troubleshooting

### Error de conexión a la API

**Problema**: La aplicación Angular no puede conectarse a la API

**Solución**:
1. Verificar que la WebAPI esté corriendo en `http://localhost:5000`
2. Verificar CORS en `src/WebAPI/Program.cs`
3. Verificar la URL en `webapp/src/app/services/job.service.ts`

### Error al cargar archivos

**Problema**: "Error al cargar archivo ZIP"

**Solución**:
1. Verificar que el archivo sea un ZIP válido
2. Verificar permisos de escritura en carpeta `JobFiles`
3. Verificar límite de tamaño (máx 100MB por defecto)

### Error de base de datos

**Problema**: "Cannot open database"

**Solución**:
1. Verificar cadena de conexión en `appsettings.json`
2. Verificar que la base de datos existe
3. Ejecutar el script `sql/01_schema.sql`
4. Verificar permisos del usuario de SQL

### Las tareas no se ejecutan

**Problema**: La tarea aparece pero no se ejecuta

**Nota**: Esta aplicación web solo **configura** las tareas en la base de datos. Para que se ejecuten, necesitas tener corriendo la aplicación JobHost (WPF) que es la que realmente ejecuta las tareas según la programación.

**Solución**:
1. Ejecutar la aplicación `JobHost` (el proyecto WPF existente)
2. Verificar que JobHost esté configurado con `"Source": "Database"` en su appsettings.json
3. Verificar que la tarea esté habilitada (`enabled: true`)

## Integración con JobHost

Esta aplicación web es un **frontend de gestión** para el sistema existente:

```
┌─────────────┐      ┌──────────────┐      ┌─────────────┐
│   Angular   │─────▶│   WebAPI     │─────▶│  SQL Server │
│   WebApp    │      │  REST API    │      │  Database   │
└─────────────┘      └──────────────┘      └─────────────┘
                                                   │
                                                   ▼
                                            ┌─────────────┐
                                            │   JobHost   │
                                            │ (WPF App)   │
                                            │  Ejecutor   │
                                            └─────────────┘
```

1. **WebApp**: Interfaz para crear/editar tareas
2. **WebAPI**: Servicio REST para operaciones CRUD
3. **Database**: Almacena configuración de tareas
4. **JobHost**: Lee la DB y ejecuta las tareas (ya existente)

## Desarrollo

### Agregar nuevos campos a Job

1. Actualizar modelo en `src/WebAPI/DTOs/JobDefinitionDto.cs`
2. Actualizar schema SQL en `sql/01_schema.sql`
3. Actualizar servicio en `src/WebAPI/Services/JobManagementService.cs`
4. Actualizar modelo TypeScript en `webapp/src/app/models/job.model.ts`
5. Actualizar formulario en `webapp/src/app/components/job-form/`

### Agregar nuevos componentes Angular

```bash
cd webapp
ng generate component components/nuevo-componente
```

## Licencia

Este proyecto es parte del sistema Task Scheduler de YCC.SapAutomation.

## Contacto y Soporte

Para soporte o consultas sobre el proyecto, contactar al equipo de desarrollo.

---

**Versión**: 1.0.0
**Última actualización**: 2025
