# Design Specifications - Launcher Épico Lineage 2

## Versión: 1.0.0

## Objetivo

Replicar al 98% de precisión el diseño visual de la imagen de referencia `Assets/images/lineage_launcher_reference.png`.

## Paleta de Colores Exacta

| Color | Hex | Uso |
|-------|-----|-----|
| **PrimaryBlack** | `#0B0B0F` | Fondo principal |
| **MetallicGray** | `#1E2226` | Paneles secundarios, bordes oscuros |
| **OldGold** | `#B8860B` | Texto principal, bordes dorados |
| **BrightGold** | `#D4AF37` | Acentos brillantes, texto destacado |
| **PlayRed** | `#6C1B1B` | Fondo botón PLAY |
| **BloodRed** | `#7A0B0B` | Gradiente botón PLAY |
| **GoldPale** | `#CFC0A8` | Texto secundario |
| **GreenOnline** | `#00FF00` | Status online (con glow) |
| **Yellow** | `#FFFF00` | Status busy/maintenance |
| **Red** | `#FF0000` | Status offline |

## Tipografías

### Títulos Principales
- **Fuente**: Cinzel (preferida) / Trajan Pro / Segoe UI (fallback)
- **Tamaño**: 48pt para "LINEAGE"
- **Peso**: Bold
- **Color**: `#D4AF37` (BrightGold)
- **Efecto**: Emboss sutil (DropShadowEffect)

### Subtítulos
- **Fuente**: Segoe UI
- **Tamaño**: 14pt para "LAUNCHER v.1.0.0"
- **Color**: `#CFC0A8` (GoldPale)

### Secciones (NEWS, etc.)
- **Fuente**: Cinzel / Trajan Pro / Segoe UI
- **Tamaño**: 18pt
- **Peso**: Bold
- **Color**: `#B8860B` (OldGold)

### Texto UI
- **Fuente**: Segoe UI
- **Tamaño**: 11-12pt según contexto
- **Color**: `#CFC0A8` (GoldPale) o `#B8860B` (OldGold)

## Medidas y Espaciado

### Ventana Principal
- **Tamaño**: 1024×680px
- **Marco**: Borde dorado de 4px con efecto relieve
- **Fondo**: `#0B0B0F` (PrimaryBlack)

### Header
- **Altura**: Auto (aprox. 100px)
- **Padding**: 20px vertical
- **Título**: 48pt, margin bottom 5px
- **Subtítulo**: 14pt

### Panel Izquierdo (Servers)
- **Ancho**: 250px
- **Fondo**: `#1E2226` (MetallicGray)
- **Borde derecho**: 2px dorado
- **Padding**: 10px
- **Server Logo Box**: Margin 10,10,10,5
- **Server Items**: Margin 5px vertical

### Panel Principal (News + Play)
- **Ancho**: * (resto del espacio)
- **Padding**: 10px
- **News Area**: Margin 10,5,10,10
- **Dragón fondo**: Opacidad 12-18%, alineado a la derecha
- **Play Button**: 400×80px, Margin 0,20
- **Progress Bar**: Height 30px, Margin 20,10,20,20

### Footer
- **Altura**: Auto (aprox. 40px)
- **Padding**: 20,10
- **Borde superior**: 1px dorado
- **Links**: Font 11pt, spacing 20px horizontal
- **Copyright**: Font 10pt, alineado a la derecha

## Elementos Específicos

### Botón PLAY
- **Fondo**: Gradiente rojo (`#6C1B1B` → `#7A0B0B`)
- **Texto**: "PLAY" en fuente serif, 32pt, Bold
- **Color texto**: `#D4AF37` (BrightGold)
- **Borde**: 3px dorado externo + 2px dorado interno (emboss)
- **Efectos**: 
  - Sombra emboss (DropShadowEffect)
  - Hover: Scale 1.03 + Glow dorado (BlurRadius 20, Opacity 0.9)
  - Duración animación: 300ms

### Barra de Progreso
- **Fondo**: `#0B0B0F` (PrimaryBlack)
- **Marco**: 3px dorado
- **Relleno**: Gradiente dorado horizontal con shimmer
- **Altura**: 30px
- **Texto estado**: 11pt, GoldPale, centrado abajo

### Status Dots
- **Tamaño**: 12×12px
- **Online**: Verde (`#00FF00`) con glow (BlurRadius 5, Opacity 1.0)
- **Busy**: Amarillo (`#FFFF00`) con glow (BlurRadius 5, Opacity 1.0)
- **Offline**: Rojo (`#FF0000`) con Opacity 0.8

### Marco de Ventana
- **Borde**: 4px gradiente dorado
- **Efecto**: Relieve metálico (DropShadowEffect, BlurRadius 2, Opacity 0.4)
- **Fondo**: `#0B0B0F` (PrimaryBlack)

## Assets Requeridos

### Imágenes
- **dragon_silhouette.png**: 1920×1280px, PNG con transparencia, opacidad 12-18%
- **background_texture.png**: (Opcional) Textura de fondo oscuro

### Fuentes
- **Cinzel-Regular.ttf**: Para títulos
- **Cinzel-Bold.ttf**: Para títulos en negrita
- **Cinzel-SemiBold.ttf**: (Opcional) Para variaciones

### SVGs
- **frame_gold.svg**: Marco dorado ornamentado
- **ornament_corner.svg**: Decoración de esquinas
- **separator_gold.svg**: Línea separadora dorada
- **status_dot_*.svg**: Dots de estado (online, busy, offline)
- **icon_play.svg**: Icono de play
- **icon_star.svg**: Estrella para favoritos

## Animaciones

### Window Fade-In
- **Efecto**: Opacity 0 → 1
- **Duración**: 400ms
- **Trigger**: Window.Loaded

### Logo Entrance
- **Efecto**: Opacity 0 → 1 + TranslateY -20 → 0
- **Duración**: 600ms
- **Easing**: CubicEase EaseOut
- **Trigger**: TextBlock.Loaded

### Play Button Hover
- **Efecto**: Scale 1.0 → 1.03 + Glow dorado
- **Duración**: 300ms
- **Trigger**: IsMouseOver

### Progress Bar Shimmer
- **Efecto**: LinearGradientBrush animado horizontalmente
- **Duración**: 1200ms (loop infinito)
- **Tipo**: RepeatBehavior="Forever"

## Estructura de Archivos

```
Lineage2Launcher/
├── MainWindow.xaml          # Layout principal
├── Themes/
│   ├── Colors.xaml          # Paleta de colores
│   └── Controls.xaml         # Estilos de controles
├── Assets/
│   ├── images/              # Imágenes de fondo
│   ├── svg/                 # Iconos vectoriales
│   ├── ui/                  # Elementos UI
│   └── xaml/                # LauncherAssets.xaml
├── Fonts/                   # Tipografías
└── ViewModels/              # Lógica MVVM
```

## Criterios de Éxito

- [x] Diseño visual coincide al 98% con la referencia
- [x] Todos los colores son exactos (#B8860B, #6C1B1B, etc.)
- [x] Tipografía serif para títulos principales
- [x] Dragón de fondo visible con opacidad correcta
- [x] Marco dorado con efecto de relieve
- [x] Botón PLAY con estilo exacto de la referencia
- [x] Animaciones funcionan correctamente
- [x] Assets optimizados y organizados
- [x] Documentación completa

## Notas Técnicas

1. **Fuentes**: Si Cinzel no está disponible, usar Trajan Pro o Segoe UI como fallback
2. **Imágenes**: El dragón debe ser una imagen PNG con transparencia, posicionada a la derecha
3. **Efectos**: Usar DropShadowEffect para glows y relieves
4. **Performance**: Optimizar imágenes PNG (no más de 1.5MB cada una)
5. **HiDPI**: Assets vectoriales (SVG) son escalables, PNGs requieren versiones @2x
6. **Compatibilidad**: Diseño responsivo para 1920×1080 y 1366×768

## Referencias

- Imagen de referencia: `Assets/images/lineage_launcher_reference.png`
- Documentación de assets: `Assets/README_ASSETS.md`
- Guía de integración: `Assets/INTEGRATION_GUIDE.md`


