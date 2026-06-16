---
title: PRD — L2Titan Launcher
version: 1.0
date: 2026-06-03
status: as-built + roadmap
author: Omar (okuzpe) + BMAD/Claude
---

# PRD — L2Titan Launcher

## 1. Visión

Launcher oficial de Windows para el servidor privado **L2Titan** (Lineage 2 Interlude x25). Un solo ejecutable que instala, verifica, repara y actualiza el cliente del juego, y lo lanza con un clic. Estética "epic fantasy" alineada con la web `l2-titan.com`, al nivel de launchers comerciales (Riot/Battle.net).

## 2. Usuarios

- **Jugador nuevo**: descarga el launcher, espera que instale el juego completo (~7 GB) sin pasos manuales.
- **Jugador recurrente**: abre el launcher, verifica rápido, juega. Tolerancia cero a re-descargas innecesarias.
- **Operador del servidor (Omar)**: despliega actualizaciones del cliente con un solo script; necesita que el manifest y los archivos publicados nunca queden inconsistentes.

## 3. Requisitos funcionales (estado: ✅ implementado / 🔜 pendiente)

### Núcleo
- ✅ FR1 — Verificación automática al arrancar: hash SHA-256 por archivo contra manifest remoto.
- ✅ FR2 — Descarga incremental: solo archivos faltantes o con hash distinto; staging en `.part` + move atómico (nunca deja archivos truncados).
- ✅ FR3 — Pausa/reanudación mid-archivo; cancelación limpia al cerrar la ventana.
- ✅ FR4 — Progreso por bytes (no por archivos) con velocidad y ETA.
- ✅ FR5 — Lanzar el juego (`system\L2.exe` configurable) con elevación UAC; detecta juego ya corriendo.
- ✅ FR6 — Botón de estado único con semántica de color: PLAY verde / PAUSE azul / RESUME ámbar / CHECKING gris / RETRY rojo.

### Contenido y comunidad
- ✅ FR7 — Hero embebido con WebView2 mostrando `l2-titan.com` (fallback si falta el runtime o falla la navegación inicial).
- ❌ FR8 — Drawer de rates del servidor — ELIMINADO 2026-06-16 (código del VM retirado en la simplificación de UI; sin binding en el XAML actual).
- ⚠️ FR9 — Accesos directos Website/Discord/TikTok: ✅ presentes. El estado del servidor (Login/Game) con polling fue ELIMINADO 2026-06-16 (no había UI que lo mostrara).

### Errores y resiliencia
- ✅ FR10 — Mensajes de error accionables y específicos en la barra de estado (`LauncherError`): sin conexión, manifest corrupto, archivo de servidor corrupto (hash), sin espacio en disco, sin permisos de escritura, juego abierto bloqueando archivos, conexión estancada (timeout 60 s).
- ✅ FR11 — Manifest con 3 reintentos + backoff; hash-mismatch NO se reintenta (problema de servidor, se informa).
- ✅ FR12 — Log interno de diagnóstico (en DEBUG se replica a consola).

### Operación (lado servidor)
- ✅ FR13 — `deploy-simple.sh`: genera manifest (SHA-256), empaqueta, sube por SSH, extrae y publica con permisos correctos (644/755); falla ruidosamente ante errores parciales.
- ✅ FR14 — CI (GitHub Actions, gratis en repo público): build + tests en push/PR; firma del exe con SignPath Foundation en tags `v*` (requiere alta en SignPath + secrets `SIGNPATH_*`).

### Pendiente (roadmap)
- ✅ FR15 — Redesplegar manifest del servidor en SHA-256 — HECHO 2026-06-16 (servidor en producción con manifest SHA-256 + HTTPS).
- ✅ FR16 — Firma del manifest (clave embebida + firma detached RSA) — HECHO 2026-06-16 (`Services/ManifestSecurity.cs` + `sign-manifest.sh`).
- ✅ FR17 — Auto-update del propio launcher — HECHO 2026-06-16 (`Services/LauncherUpdater.cs` + `launcher.json` firmado + `publish-launcher.sh`).
- 🔜 FR18 — Noticias nativas en el launcher (hoy las cubre el WebView).

## 4. Requisitos no funcionales

- **NFR1 Seguridad**: canal de updates solo HTTPS (URLs http:// se rechazan y se fuerza el default seguro); rutas del manifest validadas contra path-traversal; SHA-256 extremo a extremo.
- **NFR2 Confiabilidad**: ningún estado intermedio corrupto tras crash/cierre/pausa; verificación de 1500+ archivos en segundos.
- **NFR3 UX**: el usuario nunca ve un estado sin salida — todo error termina en RETRY o instrucción concreta.
- **NFR4 Distribución**: single-file self-contained win-x64, sin compresión (evita falsos positivos AV), firmado.
- **NFR5 Compatibilidad**: Windows 10/11, .NET 10, DPI-aware (app.manifest).

## 5. Fuera de alcance

- Multi-servidor / selector de realms.
- Plataformas no-Windows.
- Gestión de cuentas dentro del launcher (la web lo cubre).
