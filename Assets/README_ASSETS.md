# Assets del Launcher Épico - Especificaciones Completas

## Estructura de Carpetas

```
Assets/
├── svg/                    # Iconos vectoriales SVG
│   ├── status_dot_online.svg
│   ├── status_dot_busy.svg
│   ├── status_dot_offline.svg
│   ├── icon_play.svg
│   ├── icon_star.svg
│   ├── frame_decor.svg
│   ├── frame_gold.svg (marco dorado completo)
│   ├── ornament_corner.svg (decoración de esquinas)
│   └── separator_gold.svg (línea separadora)
├── xaml/                   # Recursos XAML
│   └── LauncherAssets.xaml
├── png/                    # Imágenes raster (placeholders)
│   ├── @1x/
│   └── @2x/
├── images/                 # Imágenes de fondo
│   ├── background_launcher.png (placeholder)
│   ├── lineage_launcher_reference.png (referencia)
│   ├── dragon_silhouette_placeholder.txt (instrucciones)
│   └── background_texture_placeholder.txt (instrucciones)
├── ui/                     # Elementos UI
│   ├── frame_gold.png
│   ├── play_button_normal.png
│   ├── play_button_hover.png
│   └── play_button_placeholder.txt (instrucciones)
└── README_ASSETS.md        # Este archivo
```

## Especificaciones de Color

### Paleta Exacta (Hex)

| Color | Hex | Uso |
|-------|-----|-----|
| **PrimaryBlack** | `#0B0B0F` | Fondo principal |
| **MetallicGray** | `#1E2226` | Paneles secundarios |
| **OldGold** | `#B8860B` | Texto principal, bordes |
| **BrightGold** | `#D4AF37` | Acentos brillantes |
| **PlayRed** | `#6C1B1B` | Fondo botón PLAY |
| **GoldPale** | `#CFC0A8` | Texto secundario |
| **GreenOnline** | `#00FF00` | Status online (con glow) |
| **Yellow** | `#FFFF00` | Status busy/maintenance |
| **Red** | `#FF0000` | Status offline |

### Brushes en XAML

Todos los colores están definidos en `Themes/Colors.xaml` y `Assets/xaml/LauncherAssets.xaml`:

- `{StaticResource PrimaryBlackBrush}`
- `{StaticResource OldGoldBrush}`
- `{StaticResource GoldPaleBrush}`
- `{StaticResource PlayRedBrush}`
- `{StaticResource GreenOnlineBrush}`

## Tipografías

### Títulos
- **Fuente Principal**: Segoe UI (fallback: Cinzel, Trajan)
- **Tamaño**: 48pt para "LINEAGE"
- **Peso**: Bold
- **Color**: `#B8860B` (OldGold)

### Subtítulos
- **Fuente**: Segoe UI
- **Tamaño**: 14pt para "LAUNCHER v.1.0.0"
- **Color**: `#CFC0A8` (GoldPale)

### UI Text
- **Fuente**: Segoe UI
- **Tamaño**: 12-16pt según contexto
- **Color**: `#B8860B` (OldGold) o `#CFC0A8` (GoldPale)

### Log/Consola
- **Fuente**: Consolas
- **Tamaño**: 9-10pt
- **Color**: `#B8860B` (OldGold)

## Medidas y Espaciado

### Ventana Principal
- **Tamaño Base**: 1024×680px
- **Tamaño Canvas Diseño**: 1920×1280px
- **Versiones**: @1x, @2x, resize 1280×853

### Layout

#### Header
- **Altura**: Auto (aprox. 100px)
- **Padding**: 20px vertical
- **Título**: 48pt, margin bottom 5px
- **Subtítulo**: 14pt

#### Panel Izquierdo (Servers)
- **Ancho**: 250px
- **Padding**: 10px
- **Server Logo Box**: Margin 10,10,10,5
- **Server Items**: Margin 5px vertical

#### Panel Derecho (News + Play)
- **Ancho**: * (resto del espacio)
- **Padding**: 10px
- **News Area**: Margin 10,5,10,10
- **Play Button**: Width 400px, Height 80px, Margin 0,20
- **Progress Bar**: Height 30px, Margin 20,10

#### Footer
- **Altura**: Auto (aprox. 40px)
- **Padding**: 20,10
- **Links**: Font 11pt, spacing 20px horizontal
- **Copyright**: Font 10pt

## Iconos SVG

### Status Dots

#### Online (Verde)
- **Tamaño**: 12×12px (viewBox: 0 0 12 12)
- **Color**: `#00FF00`
- **Efecto**: Glow con blur 1.5px
- **Archivo**: `Assets/svg/status_dot_online.svg`

#### Busy (Amarillo)
- **Tamaño**: 12×12px
- **Color**: `#FFFF00`
- **Efecto**: Glow con blur 1.5px
- **Archivo**: `Assets/svg/status_dot_busy.svg`

#### Offline (Rojo)
- **Tamaño**: 12×12px
- **Color**: `#FF0000`
- **Opacidad**: 0.8
- **Archivo**: `Assets/svg/status_dot_offline.svg`

### Otros Iconos

#### Play Icon
- **Tamaño**: 24×24px (viewBox: 0 0 24 24)
- **Color**: `#B8860B`
- **Archivo**: `Assets/svg/icon_play.svg`

#### Star Icon (Favoritos)
- **Tamaño**: 24×24px (viewBox: 0 0 24 24)
- **Color**: `#B8860B`
- **Archivo**: `Assets/svg/icon_star.svg`

#### Frame Decoration
- **Tamaño**: 100×100px (viewBox: 0 0 100 100)
- **Color**: `#B8860B`, opacity 0.6
- **Archivo**: `Assets/svg/frame_decor.svg`

## Uso en XAML

### Importar Assets

En `App.xaml`:
```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Themes/Colors.xaml"/>
    <ResourceDictionary Source="Themes/Controls.xaml"/>
    <ResourceDictionary Source="Assets/xaml/LauncherAssets.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

### Usar Status Dots

```xml
<Ellipse Style="{StaticResource StatusDotOnlineStyle}"/>
<Ellipse Style="{StaticResource StatusDotBusyStyle}"/>
<Ellipse Style="{StaticResource StatusDotOfflineStyle}"/>
```

### Usar Iconos

```xml
<Path Style="{StaticResource PlayIconStyle}"/>
<Path Style="{StaticResource StarIconStyle}"/>
```

### Usar Colores

```xml
<TextBlock Foreground="{StaticResource OldGoldBrush}" Text="LINEAGE"/>
<Border Background="{StaticResource PlayRedBrush}"/>
```

## Imágenes de Fondo

### Background Launcher
- **Tamaño Recomendado**: 1920×1280px o 3840×2560px (@2x)
- **Formato**: PNG con transparencia (si aplica)
- **Opacidad**: 12-18% sobre fondo negro
- **Contenido**: Dragón y castillos estilo fantasy
- **Ubicación**: `Assets/images/background_launcher.png`

**Nota**: Actualmente es un placeholder. Reemplazar con imagen real.

## Animaciones

### Hover Play Button
- **Efecto**: Scale 1.03 + Gold Glow
- **Duración**: 300ms
- **Implementado en**: `Themes/Controls.xaml` → `PlayButtonStyle`

### Progress Bar Shimmer
- **Tipo**: LinearGradientBrush animado
- **Duración**: 1200ms (loop)
- **Implementado en**: `Themes/Controls.xaml` → `ProgressBarStyle`

### Window Fade-In
- **Efecto**: Opacity 0 → 1
- **Duración**: 400ms
- **Implementado en**: `MainWindow.xaml` → Window.Triggers

## Recursos Externos Recomendados

### Fuentes
- **Cinzel**: [Google Fonts](https://fonts.google.com/specimen/Cinzel)
- **Trajan Pro**: Incluida en Adobe Creative Suite
- **Inter**: [Google Fonts](https://fonts.google.com/specimen/Inter)

### Imágenes de Fondo
- Buscar en: Unsplash, Pexels, o recursos de fantasy gaming
- Términos: "dark fantasy dragon", "medieval castle silhouette", "epic game background"

### Iconos Adicionales
- [HONETi Fantasy RPG Icon Pack](https://forums.unrealengine.com/t/honeti-fantasy-rpg-icon-pack)
- [Fantasy RPG Icons](https://finalbossblues.itch.io/icons)

## Checklist de Integración

- [x] SVGs creados y optimizados
- [x] XAML ResourceDictionary con assets
- [x] Colores definidos en Themes/Colors.xaml
- [x] MainWindow.xaml actualizado con diseño exacto
- [ ] Imagen de fondo real (placeholder actual)
- [ ] Fuentes personalizadas instaladas (opcional)
- [ ] PNGs @2x generados (requiere herramientas externas)
- [ ] Archivos Figma/PSD (requiere herramientas externas)

## Notas Técnicas

1. **SVGs**: Todos los SVGs están optimizados y son escalables. Usar viewBox para mantener proporciones.

2. **XAML Paths**: Los iconos están convertidos a PathGeometry en `LauncherAssets.xaml` para mejor rendimiento.

3. **Performance**: Los efectos de glow usan DropShadowEffect de WPF. Para mejor rendimiento, considerar usar imágenes pre-renderizadas.

4. **HiDPI**: Todos los assets son vectoriales o escalables. Para PNGs, proporcionar versiones @1x y @2x.

5. **Accesibilidad**: Los colores cumplen con WCAG 4.5:1 para texto sobre fondos oscuros.

## Soporte

Para preguntas o problemas con los assets, consultar:
- `Themes/Colors.xaml` - Definiciones de color
- `Themes/Controls.xaml` - Estilos de controles
- `Assets/xaml/LauncherAssets.xaml` - Assets vectoriales
- `MainWindow.xaml` - Implementación del diseño


