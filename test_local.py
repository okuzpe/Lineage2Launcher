#!/usr/bin/env python3
"""
Script único para pruebas locales del launcher
Automatiza: configuración, generación de manifest, servidor local y ejecución del launcher

Uso: python test_local.py [--port PORT] [--no-launcher] [--skip-manifest]
"""

import os
import sys
import json
import shutil
import subprocess
import urllib.request
import http.server
import socketserver
import argparse
import time
from pathlib import Path

# Importar funciones de otros scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

def print_step(message):
    """Imprime un paso del proceso"""
    print(f"\n{'='*60}")
    print(f"  {message}")
    print(f"{'='*60}")

def print_success(message):
    """Imprime mensaje de éxito"""
    print(f"✓ {message}")

def print_error(message):
    """Imprime mensaje de error"""
    print(f"✗ Error: {message}")

def print_info(message):
    """Imprime mensaje informativo"""
    print(f"  → {message}")

def load_config():
    """Carga o crea config.json"""
    config_path = Path("config.json")
    
    if config_path.exists():
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
            print_success(f"Config.json cargado")
            return config
        except Exception as e:
            print_error(f"No se pudo leer config.json: {e}")
            sys.exit(1)
    else:
        print_info("config.json no existe, creando uno por defecto...")
        # Pedir ruta del cliente
        default_path = Path.home() / "Games" / "Lineage2"
        game_path = input(f"Ingresa la ruta del cliente de Lineage 2 [{default_path}]: ").strip()
        if not game_path:
            game_path = str(default_path)
        
        config = {
            "GamePath": game_path.replace('/', '\\'),
            "ServerUrl": "http://localhost:8000",
            "ManifestUrl": "http://localhost:8000/manifest.json",
            "GameExecutable": "l2.exe",
            "GameParameters": ""
        }
        
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
        
        print_success(f"config.json creado con GamePath: {config['GamePath']}")
        return config

def verify_client_folder(game_path):
    """Verifica que la carpeta del cliente existe"""
    if not os.path.exists(game_path):
        print_error(f"La carpeta del cliente no existe: {game_path}")
        print_info("Por favor, verifica la ruta en config.json")
        sys.exit(1)
    print_success(f"Carpeta del cliente encontrada: {game_path}")

def check_manifest(game_path, skip_generate=False):
    """Verifica si existe manifest.json, si no, lo genera"""
    manifest_path = Path(game_path) / "manifest.json"
    
    if manifest_path.exists():
        print_success(f"manifest.json encontrado en: {manifest_path}")
        return True
    
    if skip_generate:
        print_error(f"manifest.json no existe en: {manifest_path}")
        print_info("Usa --skip-manifest para omitir la generación")
        sys.exit(1)
    
    print_info("manifest.json no existe, generando...")
    
    # Importar función de generate_manifest
    try:
        from generate_manifest import generate_manifest
        
        # Generar manifest en carpeta temporal primero
        temp_manifest = Path("manifest_temp.json")
        generate_manifest(game_path, str(temp_manifest))
        
        # Copiar a la carpeta del cliente
        shutil.copy(temp_manifest, manifest_path)
        temp_manifest.unlink()  # Eliminar temporal
        
        print_success(f"manifest.json generado en: {manifest_path}")
        return True
    except Exception as e:
        print_error(f"No se pudo generar manifest: {e}")
        sys.exit(1)

def update_config_for_localhost(config, port):
    """Actualiza config.json para usar localhost en múltiples ubicaciones"""
    updated = False
    
    server_url = f"http://localhost:{port}"
    manifest_url = f"{server_url}/manifest.json"
    
    if config.get("ServerUrl") != server_url:
        config["ServerUrl"] = server_url
        updated = True
    
    if config.get("ManifestUrl") != manifest_url:
        config["ManifestUrl"] = manifest_url
        updated = True
    
    if updated:
        # Actualizar config.json en el directorio del proyecto
        with open("config.json", 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
        print_success("config.json actualizado para localhost (directorio del proyecto)")
        
        # También actualizar en AppData (donde el launcher lo busca)
        try:
            import platform
            if platform.system() == "Windows":
                appdata_path = Path(os.environ.get("APPDATA", "")) / "Lineage2Launcher"
                appdata_path.mkdir(parents=True, exist_ok=True)
                appdata_config = appdata_path / "config.json"
                
                with open(appdata_config, 'w', encoding='utf-8') as f:
                    json.dump(config, f, indent=2, ensure_ascii=False)
                print_success(f"config.json actualizado para localhost (AppData: {appdata_config})")
            else:
                # Linux/Mac: usar ~/.config/Lineage2Launcher
                home = Path.home()
                config_dir = home / ".config" / "Lineage2Launcher"
                config_dir.mkdir(parents=True, exist_ok=True)
                config_file = config_dir / "config.json"
                
                with open(config_file, 'w', encoding='utf-8') as f:
                    json.dump(config, f, indent=2, ensure_ascii=False)
                print_success(f"config.json actualizado para localhost ({config_file})")
        except Exception as e:
            print_info(f"No se pudo actualizar config.json en AppData: {e}")
            print_info("El launcher usará el config.json del directorio del proyecto")
    else:
        print_success("config.json ya está configurado para localhost")

def test_server(port, timeout=5):
    """Prueba que el servidor responde y el manifest es accesible"""
    url = f"http://localhost:{port}/manifest.json"
    try:
        response = urllib.request.urlopen(url, timeout=timeout)
        if response.status == 200:
            content = response.read().decode('utf-8')  # Leer todo el contenido
            # Verificar que es JSON válido
            try:
                parsed = json.loads(content)
                # El manifest puede ser un array directo o un objeto con "files"
                files_list = None
                if isinstance(parsed, list):
                    # Formato: array directo de archivos
                    files_list = parsed
                elif isinstance(parsed, dict) and "files" in parsed:
                    # Formato: objeto con propiedad "files"
                    files_list = parsed.get("files", [])
                else:
                    print_error("El manifest no tiene el formato esperado (debe ser array o objeto con 'files')")
                    print_info(f"Tipo recibido: {type(parsed).__name__}")
                    print_info(f"Contenido recibido (primeros 200 chars): {content[:200]}")
                    return False
                
                if files_list and len(files_list) > 0:
                    print_success(f"Servidor responde correctamente en puerto {port}")
                    print_info(f"Manifest accesible: {url}")
                    print_info(f"Archivos en manifest: {len(files_list)}")
                    return True
                else:
                    print_error("El manifest está vacío")
                    return False
            except json.JSONDecodeError as e:
                print_error("El servidor responde pero el manifest no es JSON válido")
                print_info(f"Error JSON: {e}")
                print_info(f"Contenido recibido (primeros 200 chars): {content[:200]}")
                return False
        else:
            print_error(f"Servidor responde con código: {response.status}")
            return False
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print_error(f"Manifest no encontrado (404) en: {url}")
            print_info("Asegúrate de que manifest.json existe en el directorio del servidor")
        else:
            print_error(f"Error HTTP {e.code}: {e.reason}")
        return False
    except urllib.error.URLError as e:
        print_error(f"No se pudo conectar al servidor: {e.reason}")
        print_info("Asegúrate de que el servidor esté corriendo")
        return False
    except Exception as e:
        print_error(f"Error al probar servidor: {e}")
        return False

def start_server(port, directory):
    """Inicia el servidor HTTP local"""
    original_dir = os.getcwd()
    os.chdir(directory)
    
    class CustomHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
        def end_headers(self):
            self.send_header('Access-Control-Allow-Origin', '*')
            self.send_header('Access-Control-Allow-Methods', 'GET, OPTIONS')
            self.send_header('Access-Control-Allow-Headers', '*')
            if self.path.endswith('.json'):
                self.send_header('Content-Type', 'application/json')
            super().end_headers()
        
        def log_message(self, format, *args):
            # Log silencioso o mínimo
            pass
    
    try:
        with socketserver.TCPServer(("", port), CustomHTTPRequestHandler) as httpd:
            print_step(f"Servidor HTTP Local - Puerto {port}")
            print_info(f"Directorio: {directory}")
            print_info(f"URL: http://localhost:{port}/")
            print_info(f"Manifest: http://localhost:{port}/manifest.json")
            print_info("Presiona Ctrl+C para detener el servidor")
            print("="*60)
            
            # Verificar que manifest.json existe antes de probar
            manifest_path = Path(directory) / "manifest.json"
            if not manifest_path.exists():
                print_error(f"manifest.json no encontrado en: {directory}")
                print_info("El servidor está corriendo pero el manifest no está disponible")
                print_info("Ejecuta: python generate_manifest.py")
            else:
                print_success(f"manifest.json encontrado en: {manifest_path}")
            
            # Probar servidor después de un delay más largo (en thread separado para no bloquear)
            def test_in_background():
                time.sleep(3)  # Dar más tiempo al servidor
                os.chdir(original_dir)
                try:
                    if test_server(port):
                        print()
                except Exception as e:
                    # No es crítico si falla la prueba, el servidor puede estar funcionando
                    pass
                finally:
                    os.chdir(directory)
            
            import threading
            test_thread = threading.Thread(target=test_in_background, daemon=True)
            test_thread.start()
            
            httpd.serve_forever()
    except OSError as e:
        if hasattr(e, 'winerror') and e.winerror == 10048:
            print_error(f"El puerto {port} ya está en uso")
            print_info(f"Prueba con otro puerto: python test_local.py --port {port + 1}")
        else:
            print_error(f"Error al iniciar el servidor: {e}")
        sys.exit(1)
    except KeyboardInterrupt:
        print("\n\nServidor detenido.")

def find_launcher_exe():
    """Busca el ejecutable del launcher"""
    project_root = Path.cwd()
    possible_paths = [
        project_root / "bin/Release/net8.0-windows/win-x64/publish/Lineage2Launcher.exe",
        project_root / "bin/Release/net8.0-windows/Lineage2Launcher.exe",
        project_root / "bin/Debug/net8.0-windows/Lineage2Launcher.exe",
    ]
    
    for path in possible_paths:
        if path.exists():
            return path.resolve()  # Retornar ruta absoluta
    
    return None

def main():
    parser = argparse.ArgumentParser(
        description="Script único para pruebas locales del launcher",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Ejemplos:
  python test_local.py
  python test_local.py --port 8080
  python test_local.py --no-launcher
  python test_local.py --skip-manifest
        """
    )
    parser.add_argument('--port', type=int, default=8000,
                       help='Puerto para el servidor local (default: 8000)')
    parser.add_argument('--no-launcher', action='store_true',
                       help='No ejecutar el launcher automáticamente')
    parser.add_argument('--skip-manifest', action='store_true',
                       help='No generar manifest si no existe')
    
    args = parser.parse_args()
    
    print_step("Lineage2Launcher - Prueba Local")
    
    # 1. Cargar configuración
    print_step("1. Verificando configuración")
    config = load_config()
    game_path = config.get("GamePath", "")
    
    if not game_path:
        print_error("GamePath no está configurado en config.json")
        sys.exit(1)
    
    # 2. Verificar carpeta del cliente
    print_step("2. Verificando carpeta del cliente")
    verify_client_folder(game_path)
    
    # 3. Verificar/generar manifest
    print_step("3. Verificando manifest.json")
    check_manifest(game_path, args.skip_manifest)
    
    # 4. Actualizar config para localhost
    print_step("4. Configurando para localhost")
    update_config_for_localhost(config, args.port)
    
    # 5. Verificar dependencias
    print_step("5. Verificando dependencias")
    try:
        import http.server
        import socketserver
        print_success("Dependencias Python OK")
    except ImportError as e:
        print_error(f"Falta dependencia: {e}")
        sys.exit(1)
    
    # 6. Mostrar instrucciones
    print_step("6. Listo para iniciar")
    print_info("El servidor se iniciará en unos segundos...")
    print_info("Una vez iniciado, puedes ejecutar el launcher desde otra terminal")
    
    launcher_path = find_launcher_exe()
    if launcher_path:
        print_success(f"Launcher encontrado: {launcher_path}")
        if not args.no_launcher:
            print_info("El launcher se ejecutará automáticamente después de iniciar el servidor")
    else:
        print_info("Launcher no encontrado. Compila primero con:")
        print_info("  dotnet build -c Release")
        print_info("O para ejecutable único:")
        print_info("  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true")
    
    time.sleep(2)
    
    # 7. Iniciar servidor (esto bloquea)
    print_step("7. Iniciando servidor HTTP")
    
    # Si se quiere ejecutar el launcher, hacerlo en un thread separado
    if launcher_path and not args.no_launcher:
        import threading
        
        def run_launcher():
            time.sleep(3)  # Esperar a que el servidor inicie
            print_info("Ejecutando launcher...")
            try:
                # Usar ruta absoluta y ejecutar desde el directorio del proyecto (donde está config.json)
                launcher_abs_path = Path(launcher_path).resolve()
                project_dir = Path.cwd()
                
                # En Windows, usar la ruta absoluta directamente
                if sys.platform == 'win32':
                    subprocess.Popen([str(launcher_abs_path)], cwd=str(project_dir), shell=True)
                else:
                    subprocess.Popen([str(launcher_abs_path)], cwd=str(project_dir))
                
                print_success("Launcher ejecutado")
            except Exception as e:
                print_error(f"No se pudo ejecutar el launcher: {e}")
                print_info(f"Ejecuta manualmente: {launcher_path}")
        
        launcher_thread = threading.Thread(target=run_launcher, daemon=True)
        launcher_thread.start()
    
    # Iniciar servidor (bloquea)
    start_server(args.port, game_path)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nProceso cancelado por el usuario.")
        sys.exit(0)

