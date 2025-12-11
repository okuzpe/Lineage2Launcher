# Guía de Integración de Assets en WPF

## Ejemplo de Uso en MainWindow.xaml

### Fragmento Completo de MainWindow

```xml
<Window x:Class="Lineage2Launcher.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Window.Resources>
        <!-- Los recursos se cargan automáticamente desde App.xaml -->
    </Window.Resources>
    
    <Grid>
        <!-- Ejemplo: Usar status dot -->
        <Ellipse Style="{StaticResource StatusDotOnlineStyle}"/>
        
        <!-- Ejemplo: Usar icono de play -->
        <Path Style="{StaticResource PlayIconStyle}"/>
        
        <!-- Ejemplo: Usar colores -->
        <TextBlock Foreground="{StaticResource OldGoldBrush}" Text="LINEAGE"/>
        
        <!-- Ejemplo: Botón PLAY con estilo -->
        <Button Style="{StaticResource PlayButtonStyle}" 
                Content="PLAY"
                Background="{StaticResource PlayRedBrush}"/>
        
        <!-- Ejemplo: Progress bar con binding -->
        <ProgressBar Style="{StaticResource ProgressBarStyle}"
                     Value="{Binding Progress}"/>
    </Grid>
</Window>
```

## Binding de Progress Bar

### En ViewModel

```csharp
private int _progress;
public int Progress
{
    get => _progress;
    set
    {
        _progress = value;
        OnPropertyChanged();
    }
}
```

### En XAML

```xml
<ProgressBar Value="{Binding Progress}" 
             Maximum="100"
             Style="{StaticResource ProgressBarStyle}"/>
```

## Personalización de Colores

### Cambiar Color de Título

Editar `Themes/Colors.xaml`:

```xml
<Color x:Key="OldGold">#B8860B</Color>  <!-- Cambiar este valor -->
```

### Cambiar Color de Botón PLAY

```xml
<Color x:Key="PlayRed">#6C1B1B</Color>  <!-- Cambiar este valor -->
```

## Agregar Nuevos Iconos

### 1. Crear SVG

Crear `Assets/svg/icon_nuevo.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" 
        fill="#B8860B"/>
</svg>
```

### 2. Agregar a LauncherAssets.xaml

```xml
<PathGeometry x:Key="NuevoIconPath">
    <PathFigure StartPoint="12,2" IsClosed="True">
        <!-- Path data aquí -->
    </PathFigure>
</PathGeometry>

<Style x:Key="NuevoIconStyle" TargetType="Path">
    <Setter Property="Data" Value="{StaticResource NuevoIconPath}"/>
    <Setter Property="Fill" Value="{StaticResource OldGoldBrush}"/>
    <Setter Property="Stretch" Value="Uniform"/>
    <Setter Property="Width" Value="24"/>
    <Setter Property="Height" Value="24"/>
</Style>
```

### 3. Usar en XAML

```xml
<Path Style="{StaticResource NuevoIconStyle}"/>
```

## Reemplazar Imagen de Fondo

### Opción 1: Usar ImageBrush

```xml
<Window.Background>
    <ImageBrush ImageSource="Assets/images/background_launcher.png"
                Opacity="0.12"
                Stretch="UniformToFill"/>
</Window.Background>
```

### Opción 2: Usar Border con Background

```xml
<Border Background="{StaticResource PrimaryBlackBrush}">
    <Border.Background>
        <ImageBrush ImageSource="Assets/images/background_launcher.png"
                    Opacity="0.12"/>
    </Border.Background>
    <!-- Contenido aquí -->
</Border>
```

## Animaciones Personalizadas

### Hover Effect en Botón

Ya implementado en `PlayButtonStyle`. Para personalizar:

```xml
<Style x:Key="CustomButtonStyle" TargetType="Button">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}">
                    <ContentPresenter/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Trigger.EnterActions>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="border"
                                                     Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                                     To="1.05" Duration="0:0:0.2"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </Trigger.EnterActions>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

## Troubleshooting

### Los iconos no se muestran

1. Verificar que `LauncherAssets.xaml` esté importado en `App.xaml`
2. Verificar que los recursos usen `{StaticResource ...}` correctamente
3. Verificar que los PathGeometry estén bien formados

### Los colores no coinciden

1. Verificar que `Themes/Colors.xaml` esté importado
2. Verificar que los valores hex sean correctos
3. Limpiar y recompilar el proyecto

### La imagen de fondo no aparece

1. Verificar la ruta del archivo
2. Verificar que el archivo esté marcado como "Content" en el .csproj
3. Verificar la opacidad (debe ser baja, ~0.12)

## Recursos Adicionales

- [WPF ResourceDictionary Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/advanced/resourcedictionary-and-xaml-resource-references)
- [SVG to XAML Path Converter](https://github.com/BerndK/SvgToXaml)
- [WPF Animation Overview](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview)



