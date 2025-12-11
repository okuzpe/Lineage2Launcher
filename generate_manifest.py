#!/usr/bin/env python3
"""
Script para generar el manifest.json del launcher de Lineage 2
Uso: python generate_manifest.py <ruta_del_cliente> <output.json>
"""

import os
import sys
import json
import hashlib
from pathlib import Path

def calculate_md5(file_path):
    """Calcula el hash MD5 de un archivo"""
    hash_md5 = hashlib.md5()
    with open(file_path, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            hash_md5.update(chunk)
    return hash_md5.hexdigest().lower()

def generate_manifest(game_path, output_path):
    """Genera el manifest.json desde la carpeta del juego"""
    game_path = Path(game_path)
    manifest = []
    
    print(f"Escaneando: {game_path}")
    
    for root, dirs, files in os.walk(game_path):
        # Ignorar ciertas carpetas
        dirs[:] = [d for d in dirs if d not in {'logs', 'screenshots', 'temp', '__pycache__'}]
        
        for file in files:
            # Excluir manifest.json del manifest (no debe incluirse a sí mismo)
            if file.lower() == "manifest.json":
                continue
                
            file_path = Path(root) / file
            rel_path = file_path.relative_to(game_path)
            
            # Incluir TODOS los archivos (sin filtrar por extensión)
                try:
                    file_size = file_path.stat().st_size
                    file_hash = calculate_md5(file_path)
                    
                    # Convertir a formato Windows (backslash)
                    path_str = str(rel_path).replace('/', '\\')
                    
                    manifest.append({
                        "Path": path_str,
                        "Hash": file_hash,
                        "Size": file_size
                    })
                    
                    print(f"✓ {path_str} ({file_size} bytes)")
                except Exception as e:
                    print(f"✗ Error procesando {rel_path}: {e}")
    
    # Guardar manifest
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    
    print(f"\n✓ Manifest generado: {output_path}")
    print(f"  Total de archivos: {len(manifest)}")
    return manifest

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Uso: python generate_manifest.py <ruta_del_cliente> <output.json>")
        print("Ejemplo: python generate_manifest.py C:\\Lineage2 manifest.json")
        sys.exit(1)
    
    game_path = sys.argv[1]
    output_path = sys.argv[2]
    
    if not os.path.exists(game_path):
        print(f"Error: La ruta no existe: {game_path}")
        sys.exit(1)
    
    generate_manifest(game_path, output_path)

