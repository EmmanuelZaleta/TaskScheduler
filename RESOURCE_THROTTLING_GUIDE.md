# Guía de Resource Throttling y Configuration Cache

## Resumen

Este sistema implementa dos características principales:

1. **Resource Throttling**: Limita el número de jobs que pueden usar un recurso específico (ej: SAP) simultáneamente
2. **Configuration Cache**: Permite que el scheduler continúe operando si la base de datos no está disponible

---

## 1. Resource Throttling

### ¿Qué problema resuelve?

Si tienes 10 jobs SAP programados a la misma hora, todos intentarán conectarse simultáneamente, sobrecargando el sistema SAP. Con Resource Throttling, solo 5 ejecutarán inmediatamente y los otros 5 esperarán.

### Configuración

#### En `appsettings.json`:

```json
{
  "ResourceLimits": {
    "Limits": {
      "SapConnection": 5,         // Máximo 5 jobs SAP simultáneos
      "DatabaseConnection": 10,    // Máximo 10 jobs DB simultáneos
      "Default": 4                 // Jobs sin recurso específico
    }
  }
}
```

#### Para Jobs en Base de Datos:

Agrega un parámetro en la tabla `JobParam`:

```sql
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (123, 'ResourceType', 'SapConnection');
```

**Tipos de recursos disponibles:**
- `SapConnection` - Para jobs que usan SAP
- `DatabaseConnection` - Para jobs que hacen consultas pesadas a BD
- Puedes crear tus propios tipos (ej: `ApiEndpoint`, `FileSystem`, etc.)

#### Para Jobs en JSON (manifests):

```json
{
  "name": "ProcessSapInvoices",
  "kind": "ExternalProcess",
  "command": "apps/SapProcessor.exe",
  "resourceType": "SapConnection",
  "enabled": true
}
```

### Comportamiento

1. Job intenta ejecutarse
2. Verifica si hay slots disponibles del recurso
3. Si hay disponible → ejecuta inmediatamente
4. Si NO hay disponible → **ESPERA** hasta que otro job libere el recurso
5. Cuando el job termina → libera el slot automáticamente

### Logs

```
[INFO] Attempting to acquire resource 'SapConnection' (Current: 4/5)
[INFO] Resource 'SapConnection' acquired (Current: 5/5)
[WARN] Resource 'SapConnection' acquired after waiting 12.5s (Current: 5/5)
[INFO] Resource 'SapConnection' released (Current: 4/5)
```

---

## 2. Configuration Cache (Resiliencia)

### ¿Qué problema resuelve?

Si la base de datos cae o la conexión se pierde, el scheduler continúa operando con la última configuración conocida.

### Configuración

#### En `appsettings.json`:

```json
{
  "JobConfigurationCache": {
    "Enabled": true,
    "Path": "data/job-configuration-cache.json",
    "MaxAgeHours": 24
  }
}
```

### Comportamiento

#### Escenario 1: Base de datos disponible

```
[INFO] ✓ Se encontraron 10 job(s) habilitado(s) en la base de datos.
[INFO] Job configuration snapshot saved to data/job-configuration-cache.json (10 jobs)
```

El sistema:
1. Carga jobs desde BD
2. Guarda snapshot en disco (`data/job-configuration-cache.json`)
3. Opera normalmente

#### Escenario 2: Base de datos NO disponible

```
[WARN] ⚠️ Base de datos no disponible, intentando usar configuración en cache...
[WARN] 🟡 MODO OFFLINE: Usando configuración en cache (10 jobs, Última actualización: 2025-10-26T14:30:00Z)
```

El sistema:
1. Intenta conectar a BD → falla
2. Lee snapshot desde disco
3. Carga los jobs en memoria
4. Continúa ejecutando jobs según programación
5. Cada 60 segundos intenta reconectar a BD

#### Escenario 3: Base de datos se recupera

```
[INFO] ✓ Se encontraron 10 job(s) habilitado(s) en la base de datos.
[INFO] Job configuration snapshot saved (actualizado)
```

El sistema automáticamente vuelve a modo normal.

### Cache obsoleto

Si el cache tiene más de 24 horas (configurable):

```
[WARN] ⚠️ Cached configuration is 48.5 hours old (threshold: 24h)
```

Aún así carga la configuración, pero advierte que puede estar desactualizada.

---

## 3. Ejemplos de Uso

### Ejemplo 1: Configurar 3 jobs SAP con límite de 5 conexiones

```sql
-- Job 1
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (1, 'ResourceType', 'SapConnection');

-- Job 2
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (2, 'ResourceType', 'SapConnection');

-- Job 3
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (3, 'ResourceType', 'SapConnection');
```

En `appsettings.json`:
```json
{
  "ResourceLimits": {
    "Limits": {
      "SapConnection": 5
    }
  }
}
```

**Resultado**: Los 3 jobs ejecutarán simultáneamente (porque 3 < 5).

### Ejemplo 2: 10 jobs SAP con límite de 5

Si tienes 10 jobs programados a las 10:00 AM:

- 10:00:00 → Jobs 1-5 empiezan inmediatamente
- 10:00:00 → Jobs 6-10 quedan **esperando**
- 10:05:00 → Job 1 termina → Job 6 empieza
- 10:06:00 → Job 2 termina → Job 7 empieza
- ... y así sucesivamente

### Ejemplo 3: Simular caída de BD

1. Inicia JobHost normalmente
2. Verifica que el cache se crea: `data/job-configuration-cache.json`
3. Detén SQL Server o cambia la connection string a una inválida
4. Reinicia JobHost
5. Verás logs de modo offline
6. Los jobs siguen ejecutándose normalmente

---

## 4. Monitoreo

### Ver uso actual de recursos

Los logs muestran:

```
[INFO] Resource 'SapConnection' acquired (Current: 3/5)
```

Esto significa: **3 de 5 slots en uso**

### Ver estado de base de datos

```
[INFO] ✓ Base de datos disponible (modo normal)
[WARN] 🟡 MODO OFFLINE (usando cache)
```

### Verificar cache

Revisa el archivo `data/job-configuration-cache.json`:

```json
{
  "Timestamp": "2025-10-26T14:30:00Z",
  "Jobs": [
    {
      "JobId": 1,
      "Name": "ProcessInvoices",
      "ResourceType": "SapConnection",
      ...
    }
  ]
}
```

---

## 5. Troubleshooting

### Jobs no respetan el límite de recursos

**Verificar:**
1. `ResourceType` está configurado en `JobParam` o manifest
2. `appsettings.json` tiene la sección `ResourceLimits`
3. El valor en `ResourceType` coincide exactamente (case-sensitive) con la clave en `ResourceLimits.Limits`

### Cache no se está usando

**Verificar:**
1. `JobConfigurationCache.Enabled = true` en `appsettings.json`
2. El directorio `data/` tiene permisos de escritura
3. Revisar logs para errores de I/O

### Jobs esperan demasiado tiempo

**Solución:**
- Incrementar el límite: `"SapConnection": 10` (en vez de 5)
- O escalonar los horarios de ejecución

---

## 6. Mejores Prácticas

1. **Identificar recursos críticos**: Marca todos los jobs que usan SAP con `ResourceType: SapConnection`

2. **Ajustar límites gradualmente**:
   - Empieza con límite bajo (ej: 3)
   - Monitorea tiempos de espera
   - Incrementa si es necesario

3. **Mantener cache actualizado**:
   - Asegúrate que la BD esté disponible la mayor parte del tiempo
   - El cache es para emergencias, no para uso continuo

4. **Revisar logs regularmente**:
   - Busca warns de espera prolongada
   - Busca warns de cache obsoleto

5. **Testear resiliencia**:
   - Simula caída de BD periódicamente
   - Verifica que jobs continúan ejecutándose

---

## 7. Código de Ejemplo

### Crear un nuevo tipo de recurso

```json
// appsettings.json
{
  "ResourceLimits": {
    "Limits": {
      "SapConnection": 5,
      "CustomApi": 3,           // NUEVO
      "HeavyFileProcessing": 2  // NUEVO
    }
  }
}
```

```sql
-- Asignar a un job
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (10, 'ResourceType', 'CustomApi');
```

### Verificar configuración de un job

```sql
SELECT
    j.Name,
    jp.[Key],
    jp.[Value]
FROM dbo.Job j
JOIN dbo.JobParam jp ON jp.JobId = j.JobId
WHERE j.JobId = 1;
```

---

## 8. Arquitectura Técnica

```
Job Execution Flow:

1. Quartz dispara job
   ↓
2. ExternalProcessJob.Execute()
   ↓
3. Lee ResourceType del JobDataMap
   ↓
4. ResourceThrottlingManager.AcquireAsync(resourceType)
   ├─ Verifica límite (ej: 5/5)
   ├─ Si disponible → continúa
   └─ Si NO → ESPERA en SemaphoreSlim
   ↓
5. Ejecuta proceso externo
   ↓
6. Libera recurso (ResourceLease.Dispose())
```

```
Configuration Loading Flow:

1. DbAutomationSchedulingHostedService inicia
   ↓
2. Cada 60 segundos:
   ↓
3. LoadJobsWithFallbackAsync()
   ├─ try: LoadEnabledAsync() (BD)
   │   ├─ Éxito → SaveSnapshotAsync()
   │   └─ Retorna jobs
   └─ catch:
       ├─ HasValidSnapshot()?
       ├─ LoadSnapshotAsync() (disco)
       └─ Retorna jobs del cache
```

---

## Soporte

Para más información o problemas, revisar:
- Logs en `logs/jobhost-*.log`
- Cache en `data/job-configuration-cache.json`
- Configuración en `appsettings.json`
