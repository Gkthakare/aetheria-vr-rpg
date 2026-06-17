#!/usr/bin/env python3
"""
Aetheria VR - ML API Server (Minimal)

Run: python server.py
Then Unity can call:
  POST http://localhost:8000/gesture  - body: {pinch, grip, lx, ly, lz} -> returns gesture name
  POST http://localhost:8000/generate_vfx - body: {prompt} -> returns base64 image (if model loaded)

For VFX generation, add Stable Diffusion or similar. For now gesture uses simple rules.
"""

from http.server import HTTPServer, BaseHTTPRequestHandler
from datetime import datetime
from collections import deque
import json

PORT = 8000
MAX_LOGS = 100
logs = deque(maxlen=MAX_LOGS)


def add_log(msg, typ="info"):
    entry = {"time": datetime.now().strftime("%H:%M:%S"), "msg": msg, "type": typ}
    logs.append(entry)
    print(f"[{entry['time']}] [{typ}] {msg}")


def get_html():
    return """<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Aetheria ML API</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Segoe UI', system-ui, sans-serif;
      background: #0d1117;
      color: #c9d1d9;
      min-height: 100vh;
      padding: 1.5rem;
    }
    .container { max-width: 900px; margin: 0 auto; }
    header {
      border-bottom: 1px solid #30363d;
      padding-bottom: 1rem;
      margin-bottom: 1rem;
    }
    h1 {
      font-size: 1.5rem;
      font-weight: 600;
      color: #58a6ff;
      margin-bottom: 0.25rem;
    }
    .subtitle { color: #8b949e; font-size: 0.9rem; }
    .endpoints {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
      margin: 1rem 0;
    }
    .endpoint {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 6px;
      padding: 0.5rem 0.75rem;
      font-family: 'Consolas', monospace;
      font-size: 0.85rem;
      color: #7ee787;
    }
    .log-panel {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      padding: 1rem;
      min-height: 300px;
      max-height: 50vh;
      overflow-y: auto;
      font-family: 'Consolas', 'Monaco', monospace;
      font-size: 0.8rem;
      line-height: 1.6;
    }
    .log-entry {
      padding: 0.2rem 0;
      border-bottom: 1px solid #21262d;
      display: flex;
      gap: 0.5rem;
      align-items: baseline;
    }
    .log-entry:last-child { border-bottom: none; }
    .log-time { color: #8b949e; flex-shrink: 0; }
    .log-msg { color: #c9d1d9; word-break: break-all; }
    .log-msg.gesture { color: #7ee787; }
    .log-msg.request { color: #58a6ff; }
    .log-msg.error { color: #f85149; }
    .live-badge {
      display: inline-block;
      width: 8px; height: 8px;
      background: #3fb950;
      border-radius: 50%;
      animation: pulse 1.5s infinite;
      margin-right: 0.5rem;
    }
    @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:0.5} }
  </style>
</head>
<body>
  <div class="container">
    <header>
      <h1><span class="live-badge"></span>Aetheria ML API</h1>
      <p class="subtitle">Live gesture recognition • Dark theme</p>
      <div class="endpoints">
        <span class="endpoint">POST /gesture</span>
        <span class="endpoint">POST /generate_vfx</span>
        <span class="endpoint">POST /generate_3d</span>
        <span class="endpoint">GET /logs</span>
      </div>
    </header>
    <div class="log-panel" id="logs"></div>
  </div>
  <script>
    const el = document.getElementById('logs');
    let lastCount = 0;
    function fetchLogs() {
      fetch('/logs').then(r => r.json()).then(data => {
        if (data.length !== lastCount) {
          lastCount = data.length;
          el.innerHTML = data.map(e => {
            let cls = 'log-msg';
            if (e.type === 'gesture') cls += ' gesture';
            else if (e.type === 'request') cls += ' request';
            else if (e.type === 'error') cls += ' error';
            return `<div class="log-entry"><span class="log-time">${e.time}</span><span class="${cls}">${e.msg}</span></div>`;
          }).join('');
          el.scrollTop = el.scrollHeight;
        }
      }).catch(() => {});
    }
    fetchLogs();
    setInterval(fetchLogs, 500);
  </script>
</body>
</html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/" or self.path == "":
            self.send_response(200)
            self.send_header("Content-Type", "text/html")
            self.end_headers()
            self.wfile.write(get_html().encode())
        elif self.path == "/logs":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(json.dumps(list(logs)).encode())
        elif self.path == "/favicon.ico":
            self.send_response(204)
            self.end_headers()
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path == "/gesture":
            self.handle_gesture()
        elif self.path == "/generate_vfx":
            self.handle_vfx()
        elif self.path == "/generate_3d":
            self.handle_generate_3d()
        else:
            self.send_error(404)

    def handle_gesture(self):
        try:
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length)
            data = json.loads(body)
            pinch = data.get("pinch", 0)
            grip = data.get("grip", 0)

            add_log(f"Request: pinch={pinch:.2f} grip={grip:.2f}", "request")

            # Simple rule-based (replace with ML model)
            if pinch > 0.7:
                gesture = "pinch"
            elif grip > 0.7:
                gesture = "fist"
            elif grip < 0.2:
                gesture = "open"
            else:
                gesture = "none"

            add_log(f"→ Gesture: {gesture}", "gesture")
            self.send_json({"gesture": gesture})
        except Exception as e:
            add_log(f"Error: {e}", "error")
            self.send_json({"gesture": "none", "error": str(e)})

    def handle_vfx(self):
        try:
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length)
            data = json.loads(body)
            prompt = data.get("prompt", "photorealistic mountain texture")
            width = int(data.get("width", 256))
            height = int(data.get("height", 256))

            short = prompt[:50] + "..." if len(prompt) > 50 else prompt
            add_log(f"VFX request: {short} ({width}x{height})", "request")

            try:
                from image_generator import generate_image
                b64 = generate_image(prompt, width=width, height=height)
                add_log(f"Generated image ({len(b64)} chars base64)", "gesture")
                self.send_json({"image": b64, "width": width, "height": height})
            except ImportError as e:
                add_log(f"Stable Diffusion not installed: pip install diffusers transformers accelerate", "error")
                self.send_json({
                    "error": "Stable Diffusion not installed",
                    "hint": "Run: pip install diffusers transformers accelerate",
                    "status": "not_implemented"
                })
            except Exception as e:
                add_log(f"VFX generation failed: {e}", "error")
                self.send_json({"error": str(e), "status": "error"})
        except Exception as e:
            add_log(f"VFX handler error: {e}", "error")
            self.send_json({"error": str(e)})

    def handle_generate_3d(self):
        """Generate 3D mesh from text prompt (Shap-E). For infinite dungeon decor."""
        try:
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length)
            data = json.loads(body)
            prompt = data.get("prompt", "fantasy dungeon pillar")

            short = prompt[:50] + "..." if len(prompt) > 50 else prompt
            add_log(f"3D request: {short}", "request")

            try:
                from model_3d import generate_3d_model
                obj_b64 = generate_3d_model(prompt)
                add_log(f"Generated 3D mesh ({len(obj_b64)} chars base64)", "gesture")
                self.send_json({"obj": obj_b64, "prompt": prompt})
            except ImportError as e:
                add_log(f"Shap-E not installed: {e}", "error")
                self.send_json({
                    "error": "3D model generation not installed",
                    "hint": "Shap-E is in diffusers. Ensure: pip install diffusers transformers accelerate",
                    "status": "not_implemented"
                })
            except Exception as e:
                add_log(f"3D generation failed: {e}", "error")
                self.send_json({"error": str(e), "status": "error"})
        except Exception as e:
            add_log(f"3D handler error: {e}", "error")
            self.send_json({"error": str(e)})

    def send_json(self, obj):
        try:
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(json.dumps(obj).encode())
        except (ConnectionAbortedError, BrokenPipeError, ConnectionResetError):
            add_log("Client disconnected before response sent", "info")

    def log_message(self, format, *args):
        print(f"[{self.log_date_time_string()}] {args[0]}")


def main():
    add_log("Server started", "info")
    server = HTTPServer(("", PORT), Handler)
    print(f"Aetheria ML API running at http://localhost:{PORT}")
    print("  Open in browser for live logs")
    print("  POST /gesture - hand pose -> gesture")
    print("  POST /generate_vfx - prompt -> image (Stable Diffusion)")
    print("  POST /generate_3d - prompt -> 3D mesh (Shap-E)")
    server.serve_forever()


if __name__ == "__main__":
    main()
