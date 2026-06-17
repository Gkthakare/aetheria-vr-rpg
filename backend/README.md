# Aetheria Unified Backend

One FastAPI process (port **8001**) that serves everything the Unity Quest client
needs, consolidated from the former `3d-monster-gen/web/backend` (game + studio API)
and `ml-training` (gesture/VFX/3D sidecar).

## Run

```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt          # omit the ML block for a light CPU server
python main.py                           # http://0.0.0.0:8001  (Swagger at /docs)
```

For Meta Quest builds, set the Unity `AIClient.apiBaseUrl` (and the other API
components) to your PC's LAN IP, e.g. `http://192.168.1.x:8001` — not `localhost`.

## Endpoints

Game runtime (Unity `AIClient`, `IslandHeightmapTerrainLoader`):
- `GET  /health`
- `POST /api/v1/intent/parse` - text -> spawn intent (heuristic)
- `GET  /api/v1/island/heightmap.png` and `/api/v1/island/heightmap`
- Stubs (return safe 200 so Unity degrades gracefully):
  `POST /api/v1/chunk/parameters`, `/api/v1/voice/transcribe`,
  `/api/v1/message/process`, `/api/v1/dungeon/grow`, `/api/v1/opponent/generate`

ML sidecar (Unity `APIGestureRecognizer`, `VFXTextureRequest`, `Model3DRequest`):
- `POST /gesture` - hand pose -> gesture (rule-based, instant)
- `POST /generate_vfx` - prompt -> base64 PNG (Stable Diffusion, lazy/heavy)
- `POST /generate_3d` - prompt -> base64 OBJ (Shap-E, lazy/heavy)

Content studio (React dashboard in `studio-web/`):
- `/api/assets*`, `/api/generations*`, `/api/monsters*`, `WS /ws/edit/{id}`

## Layout

```
backend/
  main.py            # the single FastAPI app (entry: python main.py)
  requirements.txt   # core + optional ML block
  services/          # island_heightmap, generator_provider, image_generator, model_3d
  training/          # offline gesture/VFX training scripts (not served)
  pipeline/          # offline GPU content pipeline (SDXL -> mesh) + config/
  studio-web/        # React/Vite content dashboard (source only; npm install to run)
  docs/              # intent schema + vertical-slice doc
  data/              # runtime DB + generated artifacts (git-ignored)
```

Heavy artifacts (`.venv/`, `data/exports`, model caches, frame dumps) are
git-ignored via the repo-root `.gitignore`.
