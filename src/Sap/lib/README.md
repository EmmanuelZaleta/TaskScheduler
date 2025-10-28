# Directorio de librerías externas

## DLLs de Interoperabilidad COM de SAP

Este directorio contiene las DLLs necesarias para la interoperabilidad con SAP GUI Scripting Engine.

### DLLs Requeridas

1. **Interop.SAPFEWSELib.dll** - Interoperabilidad con SAP GUI Scripting Engine
2. **Interop.SapROTWr.dll** - Interoperabilidad con SAP Running Object Table Wrapper

### Ubicación de las DLLs

Las DLLs deben copiarse desde el directorio de compilación:
```
C:\Users\90022817\source\repos\YCC.SapAutomation.Host\src\ExternalApps\YCC.AnalisisCogisInventario\obj\Debug\net8.0-windows10.0.17763.0\
```

### Instalación

#### Desde PowerShell (recomendado):

```powershell
$sourceDir = "C:\Users\90022817\source\repos\YCC.SapAutomation.Host\src\ExternalApps\YCC.AnalisisCogisInventario\obj\Debug\net8.0-windows10.0.17763.0"
$destDir = "src\Sap\lib"

Copy-Item "$sourceDir\Interop.SAPFEWSELib.dll" -Destination $destDir
Copy-Item "$sourceDir\Interop.SapROTWr.dll" -Destination $destDir

Write-Host "DLLs copiadas exitosamente" -ForegroundColor Green
```

#### Desde Bash/Git Bash:

```bash
SOURCE_DIR="C:/Users/90022817/source/repos/YCC.SapAutomation.Host/src/ExternalApps/YCC.AnalisisCogisInventario/obj/Debug/net8.0-windows10.0.17763.0"
DEST_DIR="src/Sap/lib"

cp "$SOURCE_DIR/Interop.SAPFEWSELib.dll" "$DEST_DIR/"
cp "$SOURCE_DIR/Interop.SapROTWr.dll" "$DEST_DIR/"

echo "DLLs copiadas exitosamente"
```

### Verificación

Después de copiar las DLLs, verifique que ambos archivos existen:

```powershell
Get-ChildItem src\Sap\lib\*.dll
```

Debería ver:
- Interop.SAPFEWSELib.dll
- Interop.SapROTWr.dll

### Notas

- Estas DLLs son generadas automáticamente por Visual Studio cuando se agregan referencias COM a SAP GUI
- No se deben versionar en el repositorio (están en .gitignore)
- Son específicas de Windows y solo funcionan en sistemas con SAP GUI instalado
- Asegúrese de tener SAP GUI Scripting habilitado en su instalación de SAP
