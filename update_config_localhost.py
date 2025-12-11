#!/usr/bin/env python3
"""
Script para actualizar config.json en AppData para pruebas locales
Uso: python update_config_localhost.py [puerto]
"""

import json
import os
import sys
from pathlib import Path

def update_appdata_config(port=8000, game_path=None):
    """Actualiza config.json en AppData para localhost"""
    try:
        import platform
        if platform.system() == "Windows":
            appdata_path = Path(os.environ.get("APPDATA", "")) / "Lineage2Launcher"
            appdata_path.mkdir(parents=True, exist_ok=True)
            config_file = appdata_path / "config.json"
        else:
            # Linux/Mac
            home = Path.home()
            config_dir = home / ".config" / "Lineage2Launcher"
            config_dir.mkdir(parents=True, exist_ok=True)
            config_file = config_dir / "config.json"
        
        # Leer config existente si existe
        if config_file.exists():
            with open(config_file, 'r', encoding='utf-8') as f:
                config = json.load(f)
        else:
            config = {}
        
        # Actualizar valores
        server_url = f"http://localhost:{port}"
        manifest_url = f"{server_url}/manifest.json"
        
        config["ServerUrl"] = server_url
        config["ManifestUrl"] = manifest_url
        
        if game_path:
            config["GamePath"] = game_path
        
        # Si no se especifica GameExecutable, intentar detectarlo
        if "GameExecutable" not in config or config.get("GameExecutable") == "l2.exe":
            # Buscar ejecutable común en subcarpeta system
            if game_path:
                system_exe = Path(game_path) / "system" / "L2.exe"
                if system_exe.exists():
                    config["GameExecutable"] = "system\\L2.exe"
                else:
                    config["GameExecutable"] = "l2.exe"
            else:
                config["GameExecutable"] = "l2.exe"
        
        if "GameParameters" not in config:
            config["GameParameters"] = ""
        
        # Guardar
        with open(config_file, 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
        
        print(f"[OK] config.json actualizado en: {config_file}")
        print(f"  ServerUrl: {server_url}")
        print(f"  ManifestUrl: {manifest_url}")
        if game_path:
            print(f"  GamePath: {game_path}")
        
        return True
    except Exception as e:
        print(f"[ERROR] Error: {e}")
        return False

if __name__ == "__main__":
    port = 8000
    game_path = None
    
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            print(f"Error: '{sys.argv[1]}' no es un puerto válido")
            sys.exit(1)
    
    if len(sys.argv) > 2:
        game_path = sys.argv[2]
    
    update_appdata_config(port, game_path)

