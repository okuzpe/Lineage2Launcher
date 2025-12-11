# Pasos para Probar el Launcher Localmente

## 📋 Requisitos Previos

1. Tener Python instalado
2. Tener los archivos del cliente de Lineage 2 en una carpeta (ej: `C:\Juegos\L2`)
3. El cliente debe tener `system\L2.exe`

---

## 🚀 Opción 1: Automática (Recomendada)

### Un solo comando:

```bash
python test_local.py
```

Este script hace TODO automáticamente:
- ✅ Crea/actualiza `config.json`
- ✅ Genera `manifest.json` si no existe
- ✅ Inicia el servidor local (puerto 8000)
- ✅ Ejecuta el launcher

**Nota:** Si quieres usar otro puerto:
```bash
python test_local.py --port 8080
```

---

## 🔧 Opción 2: Manual (Paso a Paso)

### Paso 1: Generar el Manifest

```bash
python generate_manifest.py "C:\Juegos\L2" "manifest.json"
```

Esto crea `manifest.json` con todos los archivos del juego y sus hashes MD5.

### Paso 2: Copiar el Manifest al Servidor

```bash
copy manifest.json "C:\Juegos\L2\manifest.json"
```

El manifest debe estar en la misma carpeta que los archivos del juego.

### Paso 3: Compilar el Launcher

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable estará en:
```
bin\Release\net8.0-windows\win-x64\publish\Lineage2Launcher.exe
```

### Paso 4: Iniciar el Servidor Local

En una terminal:

```bash
python start_local_server.py 8000 "C:\Juegos\L2"
```

Deberías ver:
```
Serving files from: C:\Juegos\L2
Server running on http://localhost:8000
```

### Paso 5: Ejecutar el Launcher

1. Abre `bin\Release\net8.0-windows\win-x64\publish\Lineage2Launcher.exe`
2. Haz clic en **"✓ Verify/Download"**
3. Espera a que termine la verificación/descarga
4. Haz clic en **"▶ Play"** para iniciar el juego

---

## ✅ Verificación

### ¿El servidor funciona?

Abre en tu navegador:
```
http://localhost:8000/manifest.json
```

Deberías ver el contenido del manifest en formato JSON.

### ¿El launcher encuentra los archivos?

- El launcher descargará/verificará archivos en la **misma carpeta donde está el .exe**
- Busca el ejecutable del juego en: `[carpeta_del_launcher]\system\L2.exe`

---

## 🐛 Solución de Problemas

### Error: "Game executable not found"

**Causa:** El launcher no encuentra `system\L2.exe`

**Solución:**
1. Asegúrate de que los archivos del juego estén descargados
2. Verifica que exista `system\L2.exe` en la carpeta del launcher
3. El launcher descarga archivos en su propia carpeta (portable)

### Error: "404 File not found"

**Causa:** El servidor no está sirviendo el manifest

**Solución:**
1. Verifica que el servidor esté corriendo
2. Verifica que `manifest.json` esté en `C:\Juegos\L2\`
3. Prueba acceder a `http://localhost:8000/manifest.json` en el navegador

### Error: "All files are up to date" pero no descarga

**Causa:** Los archivos ya existen en la carpeta del launcher

**Solución:**
- Si quieres forzar descarga, borra los archivos de la carpeta del launcher (excepto el .exe y la carpeta `img/`)

---

## 📝 Notas Importantes

1. **El launcher es portable**: Descarga archivos en su propia carpeta
2. **El servidor debe estar corriendo** mientras usas el launcher
3. **El manifest debe estar actualizado** cada vez que cambies archivos del juego
4. **El ejecutable del juego** debe estar en `system\L2.exe` relativo a la carpeta del launcher

