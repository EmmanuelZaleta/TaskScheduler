-- Script para agregar ResourceType a jobs existentes
-- Este script agrega el parámetro ResourceType a jobs que usan SAP

USE [NCDR_YCC_Moldeo];
GO

-- 1. Agregar ResourceType=SapConnection a jobs específicos
-- Reemplaza 'JobName1', 'JobName2' con los nombres reales de tus jobs SAP
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
SELECT
    j.JobId,
    'ResourceType',
    'SapConnection'
FROM dbo.Job j
WHERE j.Name IN ('JobName1', 'JobName2', 'JobName3') -- Reemplazar con nombres reales
  AND NOT EXISTS (
      SELECT 1 FROM dbo.JobParam jp
      WHERE jp.JobId = j.JobId
      AND jp.[Key] = 'ResourceType'
  );
GO

-- 2. Verificar jobs con ResourceType configurado
SELECT
    j.JobId,
    j.Name,
    j.OperationCode,
    jp.[Value] AS ResourceType,
    j.Enabled
FROM dbo.Job j
LEFT JOIN dbo.JobParam jp ON jp.JobId = j.JobId AND jp.[Key] = 'ResourceType'
WHERE j.Enabled = 1
ORDER BY j.Name;
GO

-- 3. Ejemplo: Actualizar un job específico por JobId
-- UPDATE: Primero eliminar si existe
DELETE FROM dbo.JobParam
WHERE JobId = 1 -- Reemplazar con JobId real
  AND [Key] = 'ResourceType';

-- Luego insertar
INSERT INTO dbo.JobParam (JobId, [Key], [Value])
VALUES (1, 'ResourceType', 'SapConnection'); -- Reemplazar con JobId real
GO

-- 4. Ver configuración completa de un job
SELECT
    j.JobId,
    j.Name,
    jp.[Key],
    jp.[Value]
FROM dbo.Job j
LEFT JOIN dbo.JobParam jp ON jp.JobId = j.JobId
WHERE j.JobId = 1 -- Reemplazar con JobId real
ORDER BY jp.[Key];
GO
