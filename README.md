# Lineage 2 l2Titan Launcher

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Un launcher profesional y confiable para servidores privados de Lineage 2 (l2Titan) que automatiza la descarga, verificación e instalación del cliente del juego. Diseñado para proporcionar una experiencia fluida y segura para los jugadores.

## 📋 Tabla de Contenidos

- [Características](#-características)
- [Requisitos del Sistema](#-requisitos-del-sistema)
- [Instalación](#-instalación)
- [Configuración](#-configuración)
- [Pruebas Locales](#-pruebas-locales)
- [Despliegue en Producción](#-despliegue-en-producción)
- [Uso](#-uso)
- [Generación del Manifest](#-generación-del-manifest)
- [Arquitectura Técnica](#-arquitectura-técnica)
- [Estructura del Servidor](#-estructura-del-servidor)
- [Solución de Problemas](#-solución-de-problemas)
- [Preguntas Frecuentes (FAQ)](#-preguntas-frecuentes-faq)
- [Compilación](#-compilación)
- [Contribuir](#-contribuir)
- [Licencia](#-licencia)

## ✨ Características

### Funcionalidades Principales

- **📥 Descarga Automática**: Descarga automática de todos los archivos necesarios del cliente desde tu servidor
- **🔒 Verificación de Integridad**: Sistema robusto de verificación mediante checksums MD5 para garantizar la integridad de los archivos
- **🔄 Actualización Inteligente**: Detecta y actualiza automáticamente archivos corruptos, desactualizados o faltantes
- **🎨 Interfaz Gráfica Intuitiva**: Interfaz simple y funcional con Windows Forms, fácil de usar para cualquier jugador
- **📊 Log en Tiempo Real**: Visualización en tiempo real del progreso de descarga y verificación con log detallado
- **⚡ Inicio Rápido**: Lanzamiento directo del juego una vez completada la verificación
- **🛡️ Seguridad**: Validación de hash antes y después de cada descarga para prevenir archivos corruptos

### Beneficios

- **Ahorro de Tiempo**: Los jugadores no necesitan descargar manualmente actualizaciones
- **Confiabilidad**: Garantiza que todos los jugadores tengan la versión correcta del cliente
- **Mantenimiento Simplificado**: Los administradores solo necesitan actualizar el manifest en el servidor
- **Experiencia de Usuario Mejorada**: Proceso automatizado sin intervención manual

## 💻 Requisitos del Sistema

### Para Usuarios Finales

- **Sistema Operativo**: Windows 7 SP1 o superior (Windows 10/11 recomendado)
- **.NET Runtime**: .NET 8.0 Desktop Runtime o superior
- **Espacio en Disco**: Depende del tamaño del cliente (típicamente 2-5 GB)
- **Conexión a Internet**: Requerida para descargar archivos del servidor
- **Permisos**: Permisos de escritura en la carpeta de instalación del juego

### Para Desarrolladores

- **.NET SDK**: .NET 6.0 SDK o superior
- **IDE**: Visual Studio 2022, Visual Studio Code, o JetBrains Rider
- **Herramientas**: Git (opcional, para control de versiones)

## 📦 Instalación

### Opción 1: Usar el Ejecutable Pre-compilado

1. Descarga la última versión del launcher desde los releases
2. Extrae el archivo ZIP en una carpeta de tu elección
3. Asegúrate de tener instalado el .NET 6.0 Desktop Runtime
4. Ejecuta `Lineage2Launcher.exe`

### Opción 2: Compilar desde el Código Fuente

Ver la sección [Compilación](#-compilación) para instrucciones detalladas.

## ⚙️ Configuración

### Configuración Inicial

La primera vez que ejecutes el launcher, se creará automáticamente un archivo `config.json` con valores por defecto. Debes editarlo para configurar tu servidor.

### Archivo config.json

Abre `config.json` y configura los siguientes parámetros:

```json
{
  "GamePath": "C:\\Program Files\\Lineage2",
  "ServerUrl": "https://tu-servidor.com/lineage2",
  "ManifestUrl": "https://tu-servidor.com/lineage2/manifest.json",
  "GameExecutable": "l2.exe",
  "GameParameters": ""
}
```

#### Parámetros de Configuración

| Parámetro | Descripción | Ejemplo | Requerido |
|-----------|-------------|---------|-----------|
| `GamePath` | Ruta completa donde se instalará el juego | `"C:\\Program Files\\Lineage2"` | ✅ Sí |
| `ServerUrl` | URL base de tu servidor (sin barra final) | `"https://servidor.com/lineage2"` | ✅ Sí |
| `ManifestUrl` | URL completa del archivo manifest.json | `"https://servidor.com/lineage2/manifest.json"` | ✅ Sí |
| `GameExecutable` | Nombre del ejecutable del juego | `"l2.exe"` | ✅ Sí |
| `GameParameters` | Parámetros adicionales para el juego (opcional) | `"-window"` o `""` | ❌ No |

### Ejemplos de Configuración

#### Configuración Básica
```json
{
  "GamePath": "C:\\Games\\Lineage2",
  "ServerUrl": "https://mi-servidor.com/l2",
  "ManifestUrl": "https://mi-servidor.com/l2/manifest.json",
  "GameExecutable": "l2.exe",
  "GameParameters": ""
}
```

#### Configuración con Parámetros del Juego
```json
{
  "GamePath": "D:\\Lineage2",
  "ServerUrl": "https://servidor-seguro.com/lineage2",
  "ManifestUrl": "https://servidor-seguro.com/lineage2/manifest.json",
  "GameExecutable": "l2.exe",
  "GameParameters": "-window -nopatch"
}
```

### Validación de Configuración

El launcher validará automáticamente:
- ✅ Existencia del archivo `config.json`
- ✅ Accesibilidad del servidor (al iniciar verificación)
- ✅ Formato JSON válido
- ✅ Parámetros requeridos presentes

Si falta algún parámetro requerido, se usarán valores por defecto y se mostrará un mensaje en el log.

## 🧪 Pruebas Locales

Para probar el launcher localmente sin necesidad de un servidor web público, puedes usar el script único `test_local.py` que automatiza todo el proceso.

### Script Único: test_local.py (Recomendado)

El script `test_local.py` automatiza todo el proceso de prueba local:
- ✅ Verifica y crea `config.json` si es necesario
- ✅ Verifica que la carpeta del cliente existe
- ✅ Genera `manifest.json` automáticamente si no existe
- ✅ Configura `config.json` para usar localhost
- ✅ Inicia el servidor HTTP local
- ✅ Verifica que el servidor funciona correctamente
- ✅ Opcionalmente ejecuta el launcher automáticamente

#### Uso Básico

```bash
python test_local.py
```

El script te guiará paso a paso:
1. Si no existe `config.json`, te pedirá la ruta del cliente
2. Verificará que la carpeta existe
3. Generará el manifest si no existe
4. Configurará todo para localhost
5. Iniciará el servidor en el puerto 8000

#### Opciones Avanzadas

```bash
# Usar un puerto diferente
python test_local.py --port 8080

# No ejecutar el launcher automáticamente
python test_local.py --no-launcher

# No generar manifest si no existe (fallará si falta)
python test_local.py --skip-manifest

# Combinar opciones
python test_local.py --port 9000 --no-launcher
```

#### Ejemplo de Salida

```
============================================================
  Lineage2Launcher - Prueba Local
============================================================

============================================================
  1. Verificando configuración
============================================================
✓ Config.json cargado

============================================================
  2. Verificando carpeta del cliente
============================================================
✓ Carpeta del cliente encontrada: C:\Juegos\L2

============================================================
  3. Verificando manifest.json
============================================================
✓ manifest.json encontrado en: C:\Juegos\L2\manifest.json

============================================================
  4. Configurando para localhost
============================================================
✓ config.json ya está configurado para localhost

============================================================
  5. Listo para iniciar
============================================================
  → El servidor se iniciará en unos segundos...
  → Una vez iniciado, puedes ejecutar el launcher desde otra terminal
  → Launcher encontrado: bin\Release\net8.0-windows\win-x64\publish\Lineage2Launcher.exe

============================================================
  6. Iniciando servidor HTTP
============================================================
============================================================
  Servidor HTTP Local - Puerto 8000
============================================================
  → Directorio: C:\Juegos\L2
  → URL: http://localhost:8000/
  → Manifest: http://localhost:8000/manifest.json
  → Presiona Ctrl+C para detener el servidor
============================================================
✓ Servidor responde correctamente en puerto 8000
  → URL: http://localhost:8000/manifest.json
```

### Método Manual (Alternativa)

Si prefieres hacerlo manualmente:

1. **Genera el manifest:**
   ```bash
   python generate_manifest.py "C:\Lineage2" manifest.json
   copy manifest.json "C:\Lineage2\manifest.json"
   ```

2. **Configura `config.json`** para localhost:
   ```json
   {
     "GamePath": "C:\\Lineage2",
     "ServerUrl": "http://localhost:8000",
     "ManifestUrl": "http://localhost:8000/manifest.json",
     "GameExecutable": "l2.exe",
     "GameParameters": ""
   }
   ```

3. **Inicia el servidor:**
   ```bash
   python start_local_server.py 8000 "C:\Lineage2"
   ```

4. **Ejecuta el launcher:**
   ```bash
   cd bin\Release\net8.0-windows\win-x64\publish
   Lineage2Launcher.exe
   ```

### Notas para Pruebas Locales

- ✅ El servidor local sirve archivos desde el directorio del cliente
- ✅ El manifest debe estar en la raíz del directorio servido
- ✅ Todos los archivos del cliente deben estar accesibles vía HTTP
- ✅ Puedes usar cualquier puerto disponible (8000, 8080, 9000, etc.)
- ✅ Si el puerto está en uso, el script te sugerirá otro puerto
- ✅ El servidor se detiene con `Ctrl+C`
- ✅ El script `test_local.py` puede ejecutar el launcher automáticamente si está compilado

### Solución de Problemas en Pruebas Locales

**Error: "Puerto ya en uso"**
- Usa otro puerto: `python test_local.py --port 8080`
- O cierra la aplicación que está usando el puerto

**Error: "No se puede conectar al servidor"**
- Verifica que el servidor local esté corriendo
- Verifica que `config.json` use `http://localhost:PUERTO`
- Verifica que el firewall no esté bloqueando

**Error: "Manifest no encontrado"**
- El script generará el manifest automáticamente si no existe
- O genera manualmente: `python generate_manifest.py "C:\Lineage2" manifest.json`

**Error: "La carpeta del cliente no existe"**
- Verifica la ruta en `config.json` (campo `GamePath`)
- Asegúrate de usar el formato correcto: `C:\\Juegos\\L2` (doble backslash)

## 🚀 Despliegue en Producción

Esta guía te ayudará a configurar el launcher para uso en producción, incluyendo la configuración del servidor HTTP, generación del manifest, compilación y distribución del launcher a tus usuarios.

### 1. Preparación del Servidor HTTP

Tu servidor web debe estar configurado para servir archivos estáticos vía HTTP/HTTPS. Puedes usar cualquier servidor web (Nginx, Apache, IIS, etc.).

#### Estructura de Directorios en el Servidor

```
/var/www/lineage2/          (o la ruta que uses)
├── manifest.json          ← Archivo manifest (requerido)
├── system/
│   ├── L2.exe            ← Ejecutable del juego
│   └── ...                ← Otros archivos del sistema
├── textures/
│   └── ...                ← Texturas del juego
├── sounds/
│   └── ...                ← Sonidos
└── ...                    ← Todas las carpetas del cliente
```

**Importante:**
- ✅ El `manifest.json` debe estar en la raíz del directorio servido
- ✅ Mantén la estructura de carpetas original del cliente
- ✅ Todos los archivos deben ser accesibles públicamente vía HTTP

#### Configuración de MIME Types

Asegúrate de que tu servidor sirva archivos `.json` con el tipo MIME correcto:

**Nginx:**
```nginx
location ~ \.json$ {
    add_header Content-Type application/json;
}
```

**Apache:**
```apache
<FilesMatch "\.json$">
    Header set Content-Type "application/json"
</FilesMatch>
```

**IIS:**
- Agrega `.json` → `application/json` en la configuración de MIME types

### 2. Generación del Manifest para Producción

#### Paso 1: Preparar el Cliente Completo

Asegúrate de tener una copia completa y actualizada del cliente de Lineage 2 en tu máquina local.

#### Paso 2: Generar el Manifest

Usa el script de generación de manifest:

**Python:**
```bash
python generate_manifest.py "C:\Ruta\Al\Cliente\Completo" manifest.json
```

**PowerShell (alternative):**
You can use the Python script which works on all platforms:
```bash
python generate_manifest.py "C:\Ruta\Al\Cliente\Completo" manifest.json
```

#### Paso 3: Verificar el Manifest

Antes de subirlo, verifica que:
- ✅ El archivo es JSON válido (puedes usar [JSONLint](https://jsonlint.com/))
- ✅ Todos los hashes están en minúsculas
- ✅ Las rutas usan backslashes (`\\`) para Windows
- ✅ Los tamaños de archivo son correctos
- ✅ No hay archivos duplicados

#### Paso 4: Subir el Manifest al Servidor

Sube el `manifest.json` a la raíz del directorio web de tu servidor:

```bash
# Ejemplo con SCP
scp manifest.json usuario@servidor.com:/var/www/lineage2/manifest.json

# O usando FTP/SFTP según tu configuración
```

Verifica que sea accesible:
```bash
curl https://tu-servidor.com/lineage2/manifest.json
```

### 3. Subida de Archivos del Cliente

#### Paso 1: Subir Todos los Archivos

Sube todos los archivos del cliente al servidor, manteniendo la estructura de carpetas:

```bash
# Ejemplo con rsync
rsync -avz "C:\Ruta\Al\Cliente\Completo/" usuario@servidor.com:/var/www/lineage2/

# O usando FTP/SFTP según tu configuración
```

#### Paso 2: Verificar Accesibilidad

Verifica que todos los archivos sean accesibles vía HTTP. Puedes probar algunos archivos aleatorios:

```bash
# Probar algunos archivos del manifest
curl -I https://tu-servidor.com/lineage2/system/L2.exe
curl -I https://tu-servidor.com/lineage2/textures/algunatextura.bmp
```

**Importante:**
- ✅ Todos los archivos deben retornar código HTTP 200
- ✅ Los archivos deben tener el tamaño correcto
- ✅ Verifica que no haya archivos bloqueados por el servidor

### 4. Configuración del Launcher para Producción

El launcher detecta automáticamente la carpeta donde está el ejecutable y descarga los archivos ahí. No necesitas configurar `GamePath` manualmente.

#### Opción A: Config Embebido (Recomendado)

Compila el launcher con la configuración ya incluida. Modifica el código antes de compilar:

1. **Edita `MainForm.cs`** y cambia los valores por defecto en `LoadConfiguration()`:
   ```csharp
   serverUrl = "https://tu-servidor.com/lineage2";
   manifestUrl = "https://tu-servidor.com/lineage2/manifest.json";
   ```

2. O crea un `config.json` junto al ejecutable con la configuración correcta.

#### Opción B: Config Externo

Distribuye el launcher con un `config.json` que los usuarios pueden modificar si es necesario.

### 5. Compilación del Launcher

#### Compilación Self-Contained (Recomendado)

Esta opción crea un único `.exe` que incluye el runtime de .NET, ideal para distribución:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable estará en:
```
bin/Release/net8.0-windows/win-x64/publish/Lineage2Launcher.exe
```

**Ventajas:**
- ✅ Un solo archivo `.exe`
- ✅ No requiere que los usuarios instalen .NET Runtime
- ✅ Más fácil de distribuir

**Desventajas:**
- ⚠️ El archivo es más grande (~150 MB)
- ⚠️ Tarda más en iniciar (extrae archivos temporalmente)

#### Compilación con Runtime Requerido

Si prefieres un ejecutable más pequeño:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

**Ventajas:**
- ✅ Archivo más pequeño (~1-2 MB)
- ✅ Inicia más rápido

**Desventajas:**
- ⚠️ Los usuarios deben instalar .NET 8.0 Desktop Runtime
- ⚠️ Más pasos para los usuarios

### 6. Distribución a Usuarios

#### Qué Distribuir

**Opción 1: Solo el Ejecutable (Recomendado)**
- Distribuye solo `Lineage2Launcher.exe`
- El launcher detecta automáticamente su carpeta y descarga ahí
- Los usuarios pueden ponerlo en cualquier carpeta

**Opción 2: Ejecutable + Config**
- Distribuye `Lineage2Launcher.exe` + `config.json`
- Útil si quieres que los usuarios puedan modificar la configuración

#### Empaquetado

Puedes crear un ZIP con:
```
Lineage2Launcher-v1.0.zip
└── Lineage2Launcher.exe
```

O si incluyes config:
```
Lineage2Launcher-v1.0.zip
├── Lineage2Launcher.exe
└── config.json
```

#### Instrucciones para Usuarios Finales

1. **Descargar el launcher** desde tu sitio web
2. **Extraer** el ZIP en cualquier carpeta (ej: `C:\Juegos\Lineage2\`)
3. **Ejecutar** `Lineage2Launcher.exe`
4. **Hacer clic** en "Verificar/Descargar"
5. **Esperar** a que descargue todos los archivos
6. **Hacer clic** en "Jugar" cuando esté listo

**Nota:** Si usas la compilación sin runtime, los usuarios deben instalar [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) primero.

### 7. Verificación Post-Despliegue

Antes de distribuir, verifica que todo funcione:

#### Checklist de Verificación

- [ ] Servidor accesible públicamente
  ```bash
  curl https://tu-servidor.com/lineage2/manifest.json
  ```

- [ ] Manifest descargable y válido
  ```bash
  curl https://tu-servidor.com/lineage2/manifest.json | python -m json.tool
  ```

- [ ] Archivos accesibles (prueba algunos aleatorios)
  ```bash
  curl -I https://tu-servidor.com/lineage2/system/L2.exe
  ```

- [ ] Launcher se conecta correctamente
  - Ejecuta el launcher
  - Verifica que descarga el manifest sin errores

- [ ] Descarga de archivos funciona
  - Crea una carpeta vacía
  - Ejecuta el launcher desde ahí
  - Verifica que descarga todos los archivos correctamente

- [ ] Ejecución del juego funciona
  - Verifica que el botón "Jugar" ejecuta el juego correctamente

### 8. Actualización del Cliente

Cuando necesites actualizar el cliente del juego:

#### Proceso de Actualización

1. **Modifica los archivos** del cliente en tu máquina local

2. **Genera un nuevo manifest:**
   ```bash
   python generate_manifest.py "C:\Ruta\Al\Cliente\Actualizado" manifest.json
   ```

3. **Sube los archivos modificados/nuevos** al servidor:
   ```bash
   # Solo sube los archivos que cambiaron
   rsync -avz --update "C:\Ruta\Al\Cliente\Actualizado/" usuario@servidor.com:/var/www/lineage2/
   ```

4. **Sube el nuevo manifest:**
   ```bash
   scp manifest.json usuario@servidor.com:/var/www/lineage2/manifest.json
   ```

5. **Verifica** que el nuevo manifest sea accesible

#### Comportamiento para Usuarios

- Los usuarios **no necesitan actualizar el launcher**
- Al ejecutar el launcher y hacer clic en "Verificar/Descargar":
  - El launcher descargará el nuevo manifest
  - Comparará los hashes de los archivos locales con el nuevo manifest
  - Descargará automáticamente solo los archivos que cambiaron
  - Los archivos no modificados no se volverán a descargar

**Ventajas:**
- ✅ Actualizaciones automáticas para usuarios
- ✅ Solo descarga lo necesario (ahorro de ancho de banda)
- ✅ Proceso transparente para el usuario

### Notas Importantes

- **Portabilidad:** El launcher es completamente portable. Los usuarios pueden ponerlo en cualquier carpeta y descargará el juego ahí.

- **Seguridad:** Asegúrate de que tu servidor tenga HTTPS si manejas información sensible. El launcher soporta HTTPS.

- **Rendimiento:** Para servidores con muchos usuarios, considera usar un CDN o servidor de archivos estáticos optimizado.

- **Monitoreo:** Monitorea el uso de ancho de banda de tu servidor, especialmente después de lanzar actualizaciones.

## 🎮 Uso

### Flujo de Trabajo del Launcher

El launcher sigue un flujo simple y directo:

```
Inicio → Verificar/Descargar → Verificación Completa → Jugar
```

### Guía Paso a Paso

#### 1. Iniciar el Launcher

1. Ejecuta `Lineage2Launcher.exe`
2. Verifica que la configuración en `config.json` sea correcta
3. La interfaz mostrará el estado "Listo para verificar..."

#### 2. Verificar y Descargar Archivos

1. Haz clic en el botón **"Verificar/Descargar"**
2. El launcher realizará las siguientes acciones:
   - 🔌 Conecta al servidor especificado
   - 📥 Descarga el `manifest.json` desde la URL configurada
   - 🔍 Analiza todos los archivos listados en el manifest
   - ✅ Verifica cada archivo local comparando su hash MD5
   - 📥 Descarga archivos faltantes o corruptos
   - 🔒 Verifica el hash de cada archivo descargado
   - 📊 Muestra el progreso en tiempo real

3. Durante el proceso verás:
   - **Barra de Progreso**: Indica el porcentaje de archivos procesados
   - **Estado Actual**: Muestra qué archivo se está verificando/descargando
   - **Log Detallado**: Registro completo de todas las operaciones

#### 3. Iniciar el Juego

1. Una vez completada la verificación, el botón **"Jugar"** se habilitará
2. Haz clic en **"Jugar"** para iniciar el juego
3. El launcher ejecutará el juego con los parámetros configurados

### Elementos de la Interfaz

| Elemento | Descripción |
|----------|-------------|
| **Título** | Muestra "Lineage 2 l2Titan Launcher" |
| **Estado** | Indica la operación actual (ej: "Verificando: system\l2.exe") |
| **Barra de Progreso** | Muestra el progreso de verificación/descarga (0-100%) |
| **Botón Verificar/Descargar** | Inicia el proceso de verificación y descarga |
| **Botón Jugar** | Inicia el juego (habilitado solo después de verificación exitosa) |
| **Log** | Muestra todas las operaciones con timestamps |

### Interpretación del Log

El log muestra información detallada con el siguiente formato:

```
[HH:mm:ss] Mensaje de estado
```

**Tipos de Mensajes:**
- `✓` - Operación exitosa
- `✗` - Error o advertencia
- Información general sin prefijo

**Ejemplos:**
```
[14:23:15] Conectando al servidor...
[14:23:16] Descargando manifest desde: https://servidor.com/manifest.json
[14:23:17] Se encontraron 1523 archivos en el manifest.
[14:23:18] ✓ system\l2.exe - OK
[14:23:19] Archivo corrupto o desactualizado: system\L2.ini
[14:23:20] Descargando: L2.ini
[14:23:22] ✓ Descargado y verificado: L2.ini
```

## 📄 Generación del Manifest

El manifest es un archivo JSON que contiene la lista completa de archivos del cliente con sus hashes MD5 y tamaños. Es esencial para que el launcher sepa qué archivos verificar y descargar.

### Formato del Manifest

El manifest debe ser un array JSON con objetos que contengan:

```json
[
  {
    "Path": "system\\l2.exe",
    "Hash": "abc123def456789012345678901234567890",
    "Size": 12345678
  },
  {
    "Path": "system\\L2.ini",
    "Hash": "def456ghi789012345678901234567890123",
    "Size": 2048
  }
]
```

#### Campos del Manifest

| Campo | Tipo | Descripción | Ejemplo |
|-------|------|-------------|---------|
| `Path` | string | Ruta relativa del archivo (usar `\\` para Windows) | `"system\\l2.exe"` |
| `Hash` | string | Hash MD5 en minúsculas sin guiones | `"abc123def456..."` |
| `Size` | number | Tamaño del archivo en bytes | `12345678` |

### Generación Automática

El proyecto incluye dos scripts para generar el manifest automáticamente:

#### Opción 1: Script Python

**Requisitos:**
- Python 3.6 o superior

**Uso:**
```bash
python generate_manifest.py "C:\ruta\a\tu\cliente" manifest.json
```

**Ejemplo:**
```bash
python generate_manifest.py "C:\Lineage2" manifest.json
```

**Características:**
- ✅ Escanea recursivamente todos los archivos
- ✅ Calcula hashes MD5 automáticamente
- ✅ Filtra extensiones relevantes (ejecutables, datos, texturas, sonidos, etc.)
- ✅ Ignora carpetas temporales (logs, screenshots, temp)
- ✅ Genera formato Windows (backslashes)

#### Opción 2: Script Python (Recomendado)

**Requisitos:**
- Python 3.6 o superior

**Uso:**
```bash
python generate_manifest.py "C:\ruta\a\tu\cliente" manifest.json
```

**Ejemplo:**
```bash
python generate_manifest.py "C:\Lineage2" manifest.json
```

**Características:**
- ✅ Funciona en Windows, Linux y macOS
- ✅ Fácil de usar y mantener
- ✅ Incluye todas las extensiones necesarias para Lineage 2

### Extensiones Incluidas

El script incluye automáticamente archivos con las siguientes extensiones:

**Ejecutables y Bibliotecas:**
- `.exe` - Ejecutables
- `.dll` - Bibliotecas dinámicas
- `.so` - Bibliotecas compartidas (Linux)

**Configuración:**
- `.ini` - Archivos de configuración
- `.cfg` - Archivos de configuración
- `.xml` - Archivos XML
- `.json` - Archivos JSON

**Datos del Juego:**
- `.dat` - Archivos de datos
- `.pak` - Archivos empaquetados
- `.bin` - Archivos binarios
- `.l2j` - Archivos específicos de L2J

**Texturas e Imágenes:**
- `.bmp`, `.jpg`, `.jpeg`, `.png` - Imágenes
- `.tga`, `.dds`, `.tiff` - Texturas

**Sonidos y Música:**
- `.wav`, `.mp3`, `.ogg`, `.flac` - Audio
- `.m3u` - Listas de reproducción

**Fuentes:**
- `.ttf`, `.fon`, `.otf` - Fuentes

**Texto y Documentación:**
- `.txt`, `.log`, `.readme` - Texto

**Scripts:**
- `.lua`, `.py`, `.sh`, `.bat`, `.cmd` - Scripts

También se incluyen archivos sin extensión.

### Carpetas Excluidas

Los scripts automáticamente excluyen:
- `logs/` - Logs del juego
- `screenshots/` - Capturas de pantalla
- `temp/` - Archivos temporales
- `__pycache__/` - Cache de Python (solo script Python)

### Verificación del Manifest

Antes de subir el manifest a tu servidor, verifica:

1. ✅ El archivo es JSON válido
2. ✅ Todos los hashes están en minúsculas
3. ✅ Las rutas usan backslashes (`\\`) para Windows
4. ✅ Los tamaños de archivo son correctos
5. ✅ No hay archivos duplicados en el manifest

Puedes validar el JSON usando herramientas online como [JSONLint](https://jsonlint.com/).

## 🏗️ Arquitectura Técnica

### Flujo del Sistema

```
┌─────────────┐
│   Usuario   │
│  (Cliente)  │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  Lineage2Launcher│
│   (MainForm)    │
└──────┬──────────┘
       │
       ├──► Carga config.json
       │
       ├──► Descarga manifest.json del servidor
       │
       ├──► Para cada archivo en manifest:
       │    │
       │    ├──► ¿Existe localmente?
       │    │    ├──► NO → Descargar
       │    │    └──► SÍ → Verificar hash MD5
       │    │         ├──► Coincide → OK
       │    │         └──► No coincide → Descargar
       │    │
       │    └──► Verificar hash del archivo descargado
       │
       └──► Habilitar botón "Jugar"
            │
            └──► Ejecutar juego
```

### Componentes Principales

#### MainForm.cs
- **Responsabilidad**: Interfaz de usuario y orquestación del proceso
- **Funciones Clave**:
  - `LoadConfiguration()`: Carga y valida la configuración
  - `CheckAndDownloadFiles()`: Proceso principal de verificación
  - `DownloadFile()`: Descarga individual de archivos
  - `CalculateFileHash()`: Cálculo de hash MD5
  - `LogMessage()`: Registro de eventos

#### Program.cs
- **Responsabilidad**: Punto de entrada de la aplicación
- **Funciones**: Inicialización de Windows Forms

#### Clases de Datos

**LauncherConfig**
```csharp
{
    GamePath: string,        // Ruta de instalación
    ServerUrl: string,       // URL del servidor
    ManifestUrl: string,     // URL del manifest
    GameExecutable: string,  // Ejecutable del juego
    GameParameters: string   // Parámetros adicionales
}
```

**FileManifest**
```csharp
{
    Path: string,  // Ruta relativa del archivo
    Hash: string,  // Hash MD5
    Size: long     // Tamaño en bytes
}
```

### Verificación de Integridad

El sistema utiliza **MD5** para verificar la integridad de los archivos:

1. **Hash Local**: Se calcula el MD5 del archivo local
2. **Comparación**: Se compara con el hash del manifest
3. **Validación Post-Descarga**: Después de descargar, se verifica nuevamente
4. **Eliminación en Error**: Si el hash no coincide, se elimina el archivo corrupto

### Seguridad

- ✅ Validación de hash antes y después de descarga
- ✅ Timeout de 30 minutos para conexiones HTTP
- ✅ Manejo de excepciones en todas las operaciones de red
- ✅ Eliminación automática de archivos corruptos

### Rendimiento

- **Descargas Secuenciales**: Actualmente las descargas son secuenciales
- **Buffer de 8KB**: Tamaño de buffer optimizado para descargas
- **Progreso en Tiempo Real**: Actualización continua de la barra de progreso

## 🌐 Estructura del Servidor

Tu servidor web debe tener la siguiente estructura para que el launcher funcione correctamente:

```
https://tu-servidor.com/lineage2/
├── manifest.json          ← Archivo manifest (requerido)
├── system/
│   ├── l2.exe            ← Ejecutable principal
│   ├── L2.ini            ← Configuración del juego
│   ├── L2.dll            ← Bibliotecas
│   └── ...               ← Otros archivos del sistema
├── data/
│   ├── *.pak             ← Archivos de datos
│   └── ...
└── ...                   ← Otras carpetas del cliente
```

### Requisitos del Servidor Web

- ✅ **HTTP/HTTPS**: El servidor debe servir archivos vía HTTP o HTTPS
- ✅ **CORS**: No se requiere configuración CORS especial (aplicación de escritorio)
- ✅ **MIME Types**: Configurar correctamente los tipos MIME para `.json`
- ✅ **Acceso Público**: El manifest y los archivos deben ser accesibles públicamente

### Ejemplo de Configuración Nginx

```nginx
server {
    listen 80;
    server_name tu-servidor.com;
    root /var/www/lineage2;
    
    location / {
        autoindex off;
        try_files $uri $uri/ =404;
    }
    
    location ~ \.json$ {
        add_header Content-Type application/json;
    }
}
```

### Ejemplo de Configuración Apache

```apache
<VirtualHost *:80>
    ServerName tu-servidor.com
    DocumentRoot /var/www/lineage2
    
    <Directory /var/www/lineage2>
        Options -Indexes
        AllowOverride None
        Require all granted
    </Directory>
    
    <FilesMatch "\.json$">
        Header set Content-Type "application/json"
    </FilesMatch>
</VirtualHost>
```

## 🔧 Solución de Problemas

### Problemas Comunes y Soluciones

#### Error: "No se puede conectar al servidor"

**Síntomas:**
- El launcher no puede descargar el manifest
- Mensaje de error de conexión en el log

**Soluciones:**
1. ✅ Verifica que la URL en `config.json` sea correcta
2. ✅ Asegúrate de que el servidor esté accesible (prueba en el navegador)
3. ✅ Revisa el firewall de Windows
4. ✅ Verifica que no haya un proxy bloqueando la conexión
5. ✅ Si usas HTTPS, verifica que el certificado sea válido

**Comandos de Diagnóstico:**
```powershell
# Probar conectividad
Test-NetConnection -ComputerName tu-servidor.com -Port 80

# Verificar DNS
nslookup tu-servidor.com
```

#### Error: "El manifest está vacío o es inválido"

**Síntomas:**
- El manifest se descarga pero está vacío
- Error de deserialización JSON

**Soluciones:**
1. ✅ Verifica que el manifest.json sea JSON válido
2. ✅ Asegúrate de que el manifest no esté vacío
3. ✅ Verifica que el servidor esté sirviendo el archivo correctamente
4. ✅ Usa una herramienta de validación JSON para verificar el formato

#### Archivos Corruptos Detectados

**Síntomas:**
- El launcher detecta archivos con hash incorrecto
- Descarga repetida del mismo archivo

**Soluciones:**
1. ✅ El launcher descargará automáticamente archivos corruptos
2. ✅ Si persiste, elimina manualmente el archivo corrupto
3. ✅ Verifica que no haya antivirus interfiriendo
4. ✅ Asegúrate de tener permisos de escritura en la carpeta

#### Error: "No se encontró el ejecutable del juego"

**Síntomas:**
- El botón "Jugar" no inicia el juego
- Mensaje de error sobre ejecutable faltante

**Soluciones:**
1. ✅ Verifica que `GameExecutable` en `config.json` sea correcto
2. ✅ Asegúrate de que la ruta del juego sea correcta
3. ✅ Verifica que el archivo exista en la ruta especificada
4. ✅ Ejecuta "Verificar/Descargar" nuevamente

#### Problemas de Permisos

**Síntomas:**
- No se pueden crear carpetas
- No se pueden escribir archivos
- Errores de acceso denegado

**Soluciones:**
1. ✅ Ejecuta el launcher como Administrador (clic derecho → Ejecutar como administrador)
2. ✅ Verifica permisos de la carpeta de instalación
3. ✅ Asegúrate de que la carpeta no esté en uso por otro proceso
4. ✅ Considera instalar en una carpeta con permisos de usuario (ej: `C:\Users\Usuario\Games\Lineage2`)

#### Descargas Muy Lentas

**Síntomas:**
- Las descargas toman mucho tiempo
- Timeout en descargas grandes

**Soluciones:**
1. ✅ Verifica la velocidad de tu conexión a Internet
2. ✅ Asegúrate de que el servidor tenga buen ancho de banda
3. ✅ Considera usar un CDN para archivos grandes
4. ✅ El timeout está configurado en 30 minutos por defecto

#### El Juego No Inicia

**Síntomas:**
- El launcher ejecuta el juego pero no se abre
- El juego se cierra inmediatamente

**Soluciones:**
1. ✅ Verifica que todos los archivos estén descargados correctamente
2. ✅ Ejecuta el juego manualmente para ver errores específicos
3. ✅ Verifica que `GameParameters` no contenga parámetros inválidos
4. ✅ Asegúrate de tener los requisitos del juego instalados (Visual C++ Redistributables, DirectX, etc.)

### Códigos de Error Comunes

| Código/Error | Causa Probable | Solución |
|--------------|----------------|----------|
| `HttpRequestException` | Problema de conexión | Verificar servidor y firewall |
| `JsonReaderException` | Manifest inválido | Validar formato JSON |
| `UnauthorizedAccessException` | Sin permisos | Ejecutar como administrador |
| `FileNotFoundException` | Archivo no encontrado | Verificar rutas en config |
| `DirectoryNotFoundException` | Carpeta no existe | Crear carpeta manualmente |

### Logs y Diagnóstico

El launcher genera logs detallados en la interfaz. Para diagnóstico avanzado:

1. **Copiar Log**: Selecciona todo el texto del log (Ctrl+A) y cópialo (Ctrl+C)
2. **Revisar Timestamps**: Cada mensaje tiene timestamp para rastrear problemas
3. **Buscar Errores**: Busca líneas con "Error" o "✗" en el log

## ❓ Preguntas Frecuentes (FAQ)

### ¿Necesito GameGuard?

**Respuesta:** No, generalmente los servidores privados de Lineage 2 no requieren GameGuard. GameGuard es un sistema de protección comercial de NCSoft que no es necesario para servidores privados. Si tu cliente específico requiere GameGuard, deberás incluirlo en el manifest y descargarlo como cualquier otro archivo.

### ¿Puedo usar el launcher con otros servidores?

**Respuesta:** Sí, el launcher es genérico y puede usarse con cualquier servidor que proporcione un manifest en el formato correcto. Solo necesitas configurar las URLs en `config.json`.

### ¿El launcher actualiza automáticamente?

**Respuesta:** El launcher verifica y actualiza los archivos del juego cada vez que haces clic en "Verificar/Descargar". No hay actualización automática en segundo plano, pero puedes ejecutar la verificación manualmente cuando quieras.

### ¿Puedo pausar una descarga?

**Respuesta:** Actualmente no hay funcionalidad de pausa/reanudación. Si cierras el launcher durante una descarga, deberás reiniciar el proceso completo.

### ¿Qué pasa si tengo una conexión lenta?

**Respuesta:** El launcher tiene un timeout de 30 minutos por descarga. Si tu conexión es muy lenta, considera:
- Descargar durante horas de menor tráfico
- Usar una conexión más estable
- Verificar que el servidor tenga buen ancho de banda

### ¿Puedo instalar el juego en otra unidad?

**Respuesta:** Sí, solo necesitas cambiar `GamePath` en `config.json` a la ruta deseada, por ejemplo: `"D:\\Games\\Lineage2"`.

### ¿El launcher funciona con HTTPS?

**Respuesta:** Sí, el launcher soporta tanto HTTP como HTTPS. Solo asegúrate de que el certificado SSL sea válido.

### ¿Puedo modificar el código?

**Respuesta:** Sí, el código está disponible y puedes modificarlo según tus necesidades. Ver la sección [Contribuir](#-contribuir) para más información.

### ¿Hay una versión para Linux/Mac?

**Respuesta:** Actualmente el launcher está diseñado solo para Windows usando Windows Forms. Para otras plataformas, se requeriría una reescritura usando una tecnología multiplataforma como .NET MAUI o Avalonia.

## 🎨 Launcher UI Integration

El launcher ha sido migrado a WPF con un diseño épico tipo Lineage II. Esta sección explica cómo compilar y usar el nuevo UI.

### Estructura del Nuevo UI

El nuevo launcher utiliza:
- **WPF (Windows Presentation Foundation)** en lugar de WinForms
- **MVVM Pattern** para separación de lógica y presentación
- **ResourceDictionaries** para estilos y temas
- **Layout de 3 columnas**: Servidores (izquierda), Noticias (centro), Botón PLAY (derecha)

### Archivos del Nuevo UI

```
Lineage2Launcher/
├── App.xaml / App.xaml.cs          # Aplicación WPF
├── MainWindow.xaml / MainWindow.xaml.cs  # Ventana principal
├── Themes/
│   ├── Colors.xaml                 # Paleta de colores épica
│   └── Controls.xaml               # Estilos de controles
├── ViewModels/
│   ├── MainViewModel.cs            # ViewModel principal
│   ├── ServerViewModel.cs         # ViewModel de servidores
│   ├── NewsItem.cs                # Modelo de noticias
│   └── RelayCommand.cs            # Implementación ICommand
└── Assets/
    ├── images/                    # Imágenes (placeholders)
    ├── icons/                     # Iconos SVG (placeholders)
    └── ui/                        # Elementos UI (placeholders)
```

### Activación del Nuevo UI

El nuevo UI está activo por defecto. El launcher usa WPF automáticamente cuando se compila.

### Reemplazar Assets Placeholder

Los assets son placeholders pequeños. Para usar assets reales:

1. **Background**: Reemplaza `Assets/images/background_launcher.png` con tu imagen (1920x1080 o 3840x1080)
2. **Logo**: Reemplaza `Assets/images/logo_placeholder.png` con tu logo (512x512 o 1600x400)
3. **Botones**: Reemplaza `Assets/ui/play_button_*.png` con tus botones (300x60 @1x, 600x120 @2x)
4. **Iconos**: Reemplaza `Assets/icons/*.svg` con tus iconos SVG

### Volver a la UI Anterior (WinForms)

Si necesitas volver a WinForms:

1. Restaura `MainForm.cs` desde el backup en `/backups/`
2. Cambia `Lineage2Launcher.csproj`:
   ```xml
   <UseWPF>true</UseWPF>  →  <UseWindowsForms>true</UseWindowsForms>
   ```
3. Restaura `Program.cs` para usar `MainForm` en lugar de `MainWindow`

### Compilar el Nuevo UI

```bash
# Compilar en Release
dotnet build -c Release

# Publicar self-contained
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable estará en: `bin/Release/net10.0-windows/win-x64/publish/Lineage2Launcher.exe`

### Características del Nuevo UI

- ✅ **Diseño épico**: Marco dorado, fondo oscuro, estilo gótico
- ✅ **Layout 3 columnas**: Servidores, Noticias, Botón PLAY
- ✅ **Animaciones**: Hover effects, fade-in, shimmer en progress bar
- ✅ **MVVM**: Separación clara de lógica y presentación
- ✅ **Temas**: ResourceDictionaries para fácil personalización
- ✅ **Responsive**: Adaptable a diferentes tamaños de ventana

### Personalización

#### Colores

Edita `Themes/Colors.xaml` para cambiar la paleta:
```xml
<Color x:Key="OldGold">#B8860B</Color>  <!-- Cambia este valor -->
```

#### Estilos

Edita `Themes/Controls.xaml` para modificar estilos de controles:
```xml
<Style x:Key="PlayButtonStyle" TargetType="Button">
    <!-- Modifica aquí -->
</Style>
```

#### Layout

Edita `MainWindow.xaml` para cambiar el layout de 3 columnas.

## 🔨 Compilación

### Requisitos de Compilación

- **.NET SDK**: .NET 10.0 SDK o superior
- **IDE** (opcional): Visual Studio 2022, Visual Studio Code, o JetBrains Rider
- **Sistema Operativo**: Windows (para compilar aplicaciones WPF)

### Compilar desde la Línea de Comandos

1. **Abrir Terminal/PowerShell** en la carpeta del proyecto

2. **Restaurar Dependencias** (si es necesario):
   ```bash
   dotnet restore
   ```

3. **Compilar en Modo Debug**:
   ```bash
   dotnet build
   ```

4. **Compilar en Modo Release**:
   ```bash
   dotnet build -c Release
   ```

5. **El ejecutable estará en**:
   ```
   bin\Release\net10.0-windows\Lineage2Launcher.exe
   ```

### Compilar desde Visual Studio

1. Abre `Lineage2Launcher.csproj` en Visual Studio
2. Selecciona la configuración "Release"
3. Presiona `Ctrl+Shift+B` o ve a `Build → Build Solution`
4. El ejecutable estará en la carpeta `bin\Release\net10.0-windows\`

### Publicar como Ejecutable Único

Para crear un ejecutable autocontenido (sin necesidad de .NET Runtime):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable estará en: `bin\Release\net10.0-windows\win-x64\publish\`

### Dependencias

El proyecto utiliza las siguientes dependencias NuGet:

- **Newtonsoft.Json** (v13.0.3): Para serialización/deserialización JSON

Estas se restauran automáticamente al compilar.

## 🔏 Firma de Código (Code Signing)

La firma del ejecutable con un certificado Authenticode es la forma más efectiva de evitar falsos positivos de antivirus y advertencias de Windows SmartScreen.

### ¿Por qué firmar?

- **SmartScreen**: Sin firma, Windows mostrará una advertencia "Windows protegió su equipo" cada vez que un usuario ejecute el launcher.
- **Antivirus**: Los ejecutables firmados con certificados de confianza rara vez son marcados como malware.
- **Reputación**: La reputación se acumula sobre el certificado. Cuantas más descargas, menos advertencias.

### Opciones de Certificado

| Opción | Coste | Efectividad | Notas |
|--------|-------|-------------|-------|
| **EV Code Signing** (DigiCert, Sectigo) | ~$300-500/año | Máxima | Elimina SmartScreen inmediatamente. Requiere token USB. |
| **Standard Code Signing** (Sectigo, Comodo) | ~$70-200/año | Alta | SmartScreen desaparece después de algunas descargas. |
| **SignPath.io** | Gratis (open source) | Alta | Requiere que el proyecto sea open source. |
| **Certificado auto-firmado** | Gratis | Baja | Útil solo para pruebas internas. No elimina SmartScreen. |

### Cómo firmar el ejecutable

#### Paso 1: Obtener un certificado

Compra un certificado de firma de código de un proveedor reconocido (DigiCert, Sectigo, GlobalSign).

#### Paso 2: Firmar con signtool

Después de compilar el ejecutable:

```bash
# Firma con certificado desde archivo .pfx
signtool sign /f "MiCertificado.pfx" /p "password" /fd sha256 /tr http://timestamp.digicert.com /td sha256 "bin\Release\net10.0-windows\win-x64\publish\L2TitanLauncher.exe"

# Firma con certificado en token USB (EV)
signtool sign /n "L2Titan" /fd sha256 /tr http://timestamp.digicert.com /td sha256 "bin\Release\net10.0-windows\win-x64\publish\L2TitanLauncher.exe"
```

#### Paso 3: Verificar la firma

```bash
signtool verify /pa "L2TitanLauncher.exe"
```

### Integración con el proceso de build

Puedes automatizar la firma en el script `publish-release.sh`:

```bash
# Después de dotnet publish, firmar el exe
signtool sign /n "L2Titan" /fd sha256 /tr http://timestamp.digicert.com /td sha256 "$PUBLISH_DIR/L2TitanLauncher.exe"
```

## 🤝 Contribuir

Las contribuciones son bienvenidas. Si deseas contribuir al proyecto:

1. **Fork** el repositorio
2. Crea una **rama** para tu feature (`git checkout -b feature/AmazingFeature`)
3. **Commit** tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. **Push** a la rama (`git push origin feature/AmazingFeature`)
5. Abre un **Pull Request**

### Áreas de Mejora Conocidas

- [ ] Implementar descargas paralelas para mejor rendimiento
- [ ] Agregar funcionalidad de pausa/reanudación de descargas
- [ ] Implementar sistema de reintentos automáticos
- [ ] Agregar validación de tamaño antes de descargar
- [ ] Implementar caché del manifest
- [ ] Agregar sistema de autenticación/login
- [ ] Implementar actualización automática del launcher
- [ ] Mejorar manejo de errores y mensajes de usuario

### Estándares de Código

- Usar convenciones de C# estándar
- Comentar código complejo
- Mantener métodos pequeños y enfocados
- Agregar manejo de errores apropiado

## 📄 Licencia

Este proyecto está bajo la Licencia MIT. Ver el archivo `LICENSE` para más detalles.

---

**Desarrollado con ❤️ para la comunidad de Lineage 2**

Para soporte, reportar bugs o sugerencias, abre un issue en el repositorio del proyecto.
