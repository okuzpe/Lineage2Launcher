#!/usr/bin/env python3
"""
Servidor HTTP simple para pruebas locales del launcher
Uso: python start_local_server.py [puerto] [directorio]
Ejemplo: python start_local_server.py 8000 C:\Lineage2
"""

import http.server
import socketserver
import sys
import os
from pathlib import Path

def main():
    # Puerto por defecto
    port = 8000
    # Directorio por defecto (directorio actual)
    directory = os.getcwd()
    
    # Leer argumentos
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            print(f"Error: '{sys.argv[1]}' no es un puerto válido")
            sys.exit(1)
    
    if len(sys.argv) > 2:
        directory = sys.argv[2]
        if not os.path.exists(directory):
            print(f"Error: El directorio '{directory}' no existe")
            sys.exit(1)
    
    # Cambiar al directorio especificado
    os.chdir(directory)
    
    # Crear handler con soporte para CORS y MIME types
    class CustomHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
        def end_headers(self):
            # Agregar headers CORS (aunque no es necesario para app de escritorio)
            self.send_header('Access-Control-Allow-Origin', '*')
            self.send_header('Access-Control-Allow-Methods', 'GET, OPTIONS')
            self.send_header('Access-Control-Allow-Headers', '*')
            # Asegurar que JSON se sirva con el tipo MIME correcto
            if self.path.endswith('.json'):
                self.send_header('Content-Type', 'application/json')
            super().end_headers()
        
        def log_message(self, format, *args):
            # Log personalizado más limpio
            print(f"[{self.log_date_time_string()}] {args[0]} - {args[1]}")
    
    # Crear servidor
    try:
        with socketserver.TCPServer(("", port), CustomHTTPRequestHandler) as httpd:
            print("=" * 60)
            print("Servidor HTTP Local para Lineage2Launcher")
            print("=" * 60)
            print(f"Directorio: {os.getcwd()}")
            print(f"URL: http://localhost:{port}/")
            print(f"Manifest: http://localhost:{port}/manifest.json")
            print("=" * 60)
            print("Presiona Ctrl+C para detener el servidor")
            print("=" * 60)
            httpd.serve_forever()
    except OSError as e:
        if e.errno == 98 or e.winerror == 10048:  # Puerto en uso
            print(f"Error: El puerto {port} ya está en uso")
            print(f"Prueba con otro puerto: python start_local_server.py {port + 1}")
        else:
            print(f"Error al iniciar el servidor: {e}")
        sys.exit(1)
    except KeyboardInterrupt:
        print("\n\nServidor detenido.")

if __name__ == "__main__":
    main()

