"""
Aetheria unified backend (single FastAPI app, port 8001).

Consolidates the former two services into one process for the Unity Quest client:
- Game runtime API: /health, /api/v1/intent/parse, /api/v1/island/heightmap*
  (+ stubs for chunk/voice/message/dungeon/opponent that Unity already calls).
- ML sidecar (ported from the old ml-training server): /gesture, /generate_vfx,
  /generate_3d.
- Content studio API + dashboard data: /api/assets, /api/generations, /api/monsters.

Run:  python main.py   (serves on 0.0.0.0:8001)
"""

from datetime import datetime
import json
import os
from pathlib import Path
import shutil
import sqlite3
from typing import Any, Optional

from fastapi import FastAPI, File, HTTPException, Request, UploadFile, WebSocket
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, Response
from pydantic import BaseModel, Field

from services.generator_provider import GenerationProviderError, get_generation_provider
from services.island_heightmap import generate_heightmap_png, generate_heightmap_raw_meta


BASE_DIR = Path(__file__).resolve().parent
DATA_DIR = (BASE_DIR / "data").resolve()
UPLOAD_DIR = (DATA_DIR / "exports").resolve()
GENERATED_CANDIDATE_DIR = (DATA_DIR / "generated_candidates").resolve()
DB_PATH = DATA_DIR / "assets.db"
SUPPORTED_FORMATS = (".glb", ".gltf", ".obj", ".fbx")


app = FastAPI(
    title="Aetheria Unified Backend",
    description="Game runtime API + ML sidecar + content studio for the Aetheria VR RPG",
    version="2.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:3000",
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:8000",
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class Asset(BaseModel):
    id: Optional[int] = None
    name: str
    description: Optional[str] = None
    file_path: str
    file_format: str
    created_at: Optional[str] = None
    updated_at: Optional[str] = None
    metadata: Optional[dict] = None


class AssetTransform(BaseModel):
    asset_id: int
    position: dict
    rotation: dict
    scale: dict


class AssetMaterial(BaseModel):
    asset_id: int
    color: str
    metalness: float
    roughness: float


class MonsterGeneration(BaseModel):
    name: str
    baseAssetId: int
    traits: dict
    rarity: str


class GenerationRequest(BaseModel):
    prompt: str = Field(..., min_length=3, max_length=500)
    candidateCount: int = Field(default=3, ge=1, le=6)
    options: dict[str, Any] = Field(default_factory=dict)


class CandidateSelectionRequest(BaseModel):
    candidateId: int
    name: Optional[str] = None
    description: Optional[str] = None


class IntentParseRequest(BaseModel):
    """Player text or transcript for vertical-slice intent parsing."""

    text: str = Field(..., min_length=1, max_length=4000)
    locale: Optional[str] = None


def _heuristic_parse_intent(text: str) -> dict[str, Any]:
    """
    Stub parser: returns JSON matching Unity IntentParseResponse / spawn payload shape.
    Replace with LLM call when ready.
    """
    t = text.lower().strip()
    is_dragon = "dragon" in t or "dragons" in t
    is_wolf = "wolf" in t or "wolves" in t
    is_black = "black" in t or "dark" in t
    is_big = any(k in t for k in ("big", "large", "huge", "giant", "massive"))
    mythic_weak = ("mythic" in t and "weapon" in t) or "mythic weapon" in t

    if is_dragon:
        scale = 1.25 if is_big else 1.0
        display = "Dragon"
        if is_big:
            display = f"Large {display}"
        return {
            "version": "1",
            "intentType": "spawn_enemy",
            "confidence": 0.84,
            "spawn": {
                "catalogId": "creature.dragon.v1",
                "displayName": display,
                "scale": scale,
                "tintR": 0.55,
                "tintG": 0.62,
                "tintB": 0.72,
                "difficultyTier": "elite" if is_big else "normal",
                "weaknessTags": [],
                "placementMode": "near_player",
                "minDistanceM": 18.0,
                "maxDistanceM": 42.0,
                "requireNavMesh": False,
            },
            "errors": [],
        }

    if not is_wolf:
        return {
            "version": "1",
            "intentType": "noop",
            "confidence": 0.35,
            "errors": [
                "No supported creature keyword in this slice (try mentioning a wolf or dragon).",
            ],
        }

    scale = 1.45 if is_big else 1.0
    tint_r, tint_g, tint_b = (0.08, 0.08, 0.1) if is_black else (0.45, 0.38, 0.32)
    display = "Black Wolf" if is_black else "Wolf"
    if is_big:
        display = f"Large {display}"

    weakness_tags = []
    if mythic_weak:
        weakness_tags.append("mythic_weapon")

    return {
        "version": "1",
        "intentType": "spawn_enemy",
        "confidence": 0.82 if is_wolf else 0.5,
        "spawn": {
            "catalogId": "creature.wolf.v1",
            "displayName": display,
            "scale": scale,
            "tintR": tint_r,
            "tintG": tint_g,
            "tintB": tint_b,
            "difficultyTier": "elite" if is_big else "normal",
            "weaknessTags": weakness_tags,
            "placementMode": "near_player",
            "minDistanceM": 12.0,
            "maxDistanceM": 28.0,
            "requireNavMesh": True,
        },
        "errors": [],
    }


def get_db_connection(*, row_factory: bool = False) -> sqlite3.Connection:
    conn = sqlite3.connect(DB_PATH)
    if row_factory:
        conn.row_factory = sqlite3.Row
    return conn


def safe_json_loads(value: Any, default: Any) -> Any:
    if value in (None, ""):
        return default
    if isinstance(value, (dict, list)):
        return value
    try:
        return json.loads(value)
    except (TypeError, json.JSONDecodeError):
        return default


def slugify(value: str, fallback: str = "asset") -> str:
    normalized = "".join(char.lower() if char.isalnum() else "-" for char in value.strip())
    compact = "-".join(part for part in normalized.split("-") if part)
    return compact or fallback


def to_storage_path(path: Path) -> str:
    return os.path.relpath(path, BASE_DIR)


def resolve_storage_path(file_path: str) -> Path:
    path = Path(file_path)
    if path.is_absolute():
        return path
    return (BASE_DIR / path).resolve()


def ensure_supported_format(filename: str) -> str:
    file_ext = Path(filename).suffix.lower()
    if file_ext not in SUPPORTED_FORMATS:
        supported = ", ".join(SUPPORTED_FORMATS)
        raise HTTPException(status_code=400, detail=f"Unsupported file format. Use {supported}")
    return file_ext


def serialize_asset_row(row: sqlite3.Row) -> dict[str, Any]:
    asset = dict(row)
    asset["metadata"] = safe_json_loads(asset.get("metadata"), {})
    return asset


def serialize_edit_row(row: sqlite3.Row) -> dict[str, Any]:
    edit = dict(row)
    edit["data"] = safe_json_loads(edit.get("data"), {})
    return edit


def serialize_generation_candidate_row(row: sqlite3.Row) -> dict[str, Any]:
    candidate = dict(row)
    candidate["metadata"] = safe_json_loads(candidate.get("metadata"), {})
    return candidate


def serialize_generation_job_row(row: sqlite3.Row) -> dict[str, Any]:
    job = dict(row)
    job["options"] = safe_json_loads(job.get("options"), {})
    return job


def fetch_generation_job_response(job_id: int) -> dict[str, Any]:
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM generation_jobs WHERE id = ?", (job_id,))
    job_row = cursor.fetchone()
    if not job_row:
        conn.close()
        raise HTTPException(status_code=404, detail="Generation job not found")

    cursor.execute(
        "SELECT * FROM generation_candidates WHERE job_id = ? ORDER BY id ASC",
        (job_id,),
    )
    candidate_rows = cursor.fetchall()
    conn.close()

    return {
        **serialize_generation_job_row(job_row),
        "candidates": [serialize_generation_candidate_row(row) for row in candidate_rows],
    }


def fetch_asset_row(cursor: sqlite3.Cursor, asset_id: int) -> sqlite3.Row | None:
    cursor.execute("SELECT * FROM assets WHERE id = ?", (asset_id,))
    return cursor.fetchone()


def fetch_generation_candidate(
    cursor: sqlite3.Cursor, job_id: int, candidate_id: int
) -> sqlite3.Row | None:
    cursor.execute(
        "SELECT * FROM generation_candidates WHERE id = ? AND job_id = ?",
        (candidate_id, job_id),
    )
    return cursor.fetchone()


def insert_asset_record(
    cursor: sqlite3.Cursor,
    *,
    name: str,
    description: str,
    file_path: Path,
    file_format: str,
    metadata: Optional[dict[str, Any]] = None,
) -> int:
    stored_path = to_storage_path(file_path)
    cursor.execute(
        """
        INSERT INTO assets (name, description, file_path, file_format, metadata)
        VALUES (?, ?, ?, ?, ?)
        """,
        (name, description, stored_path, file_format, json.dumps(metadata or {})),
    )
    return cursor.lastrowid


def init_db():
    """Initialize SQLite database."""
    conn = get_db_connection()
    cursor = conn.cursor()

    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS assets (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            description TEXT,
            file_path TEXT NOT NULL UNIQUE,
            file_format TEXT,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            metadata JSON DEFAULT '{}'
        )
        """
    )

    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS edits (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            asset_id INTEGER NOT NULL,
            edit_type TEXT,
            data JSON,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (asset_id) REFERENCES assets(id)
        )
        """
    )

    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS monsters (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            base_asset_id INTEGER NOT NULL,
            traits JSON,
            rarity TEXT,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (base_asset_id) REFERENCES assets(id)
        )
        """
    )

    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS generation_jobs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            prompt TEXT NOT NULL,
            status TEXT NOT NULL,
            provider TEXT NOT NULL,
            options JSON DEFAULT '{}',
            error TEXT,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
        """
    )

    cursor.execute(
        """
        CREATE TABLE IF NOT EXISTS generation_candidates (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            job_id INTEGER NOT NULL,
            provider_candidate_id TEXT NOT NULL,
            name TEXT NOT NULL,
            description TEXT,
            preview_label TEXT,
            status TEXT NOT NULL,
            source_file_path TEXT,
            local_file_path TEXT,
            file_format TEXT,
            metadata JSON DEFAULT '{}',
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (job_id) REFERENCES generation_jobs(id)
        )
        """
    )

    conn.commit()
    conn.close()


@app.on_event("startup")
async def startup_event():
    """Initialize database and storage directories on startup."""
    UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    GENERATED_CANDIDATE_DIR.mkdir(parents=True, exist_ok=True)
    init_db()


@app.get("/")
async def root():
    return {
        "status": "running",
        "service": "3D Asset Dashboard API",
        "version": "1.0.0",
        "generationProvider": os.getenv("GENERATION_PROVIDER", "mock"),
    }


@app.get("/health")
async def health():
    """Lightweight health check for Unity AIClient and load balancers."""
    return {"status": "ok"}


@app.get("/api/v1/island/heightmap.png")
async def island_heightmap_png(
    size: int = 513,
    seed: int = 0,
    beach_start: float = 0.62,
    beach_width: float = 0.22,
    noise_scale: float = 4.5,
    ridge_boost: float = 0.12,
    ocean_rim_start: float = 0.97,
):
    """
    Procedural island heightmap as PNG (grayscale) for Unity TerrainData.SetHeights.
    Resolution must be 33, 65, 129, 257, 513, or 1025 (power-of-two + 1).
    Response header X-Sea-Level-Normalized matches sea_level_normalized in the JSON endpoint.
    """
    try:
        png_bytes, meta = generate_heightmap_png(
            size,
            seed,
            beach_start=beach_start,
            beach_width=beach_width,
            noise_scale=noise_scale,
            ridge_boost=ridge_boost,
            ocean_rim_start=ocean_rim_start,
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    headers = {
        "X-Sea-Level-Normalized": str(meta["sea_level_normalized"]),
        "X-Heightmap-Seed": str(meta["seed"]),
        "X-Ocean-Rim-Start": str(meta.get("ocean_rim_start", ocean_rim_start)),
    }
    return Response(content=png_bytes, media_type="image/png", headers=headers)


@app.get("/api/v1/island/heightmap")
async def island_heightmap_bundle(
    size: int = 513,
    seed: int = 0,
    world_size_meters: float = 512.0,
    terrain_height_meters: float = 600.0,
    beach_start: float = 0.62,
    beach_width: float = 0.22,
    noise_scale: float = 4.5,
    ridge_boost: float = 0.12,
    ocean_rim_start: float = 0.97,
):
    """Same heightmap as JSON: base64 PNG plus sizes and sea_level_normalized for one round-trip."""
    try:
        return generate_heightmap_raw_meta(
            size,
            seed,
            world_size_meters,
            terrain_height_meters,
            beach_start=beach_start,
            beach_width=beach_width,
            noise_scale=noise_scale,
            ridge_boost=ridge_boost,
            ocean_rim_start=ocean_rim_start,
        )
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/api/v1/intent/parse")
async def parse_intent(request: IntentParseRequest):
    """
    Vertical slice: parse player text into spawn/catalog intent.
    Unity: AIClient.ParseIntent -> SpawnDirector.
    """
    return _heuristic_parse_intent(request.text)


@app.get("/api/assets")
async def get_assets():
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM assets ORDER BY created_at DESC")
    rows = cursor.fetchall()
    conn.close()
    assets = [serialize_asset_row(row) for row in rows]
    return {"assets": assets, "total": len(assets)}


@app.get("/api/assets/{asset_id}")
async def get_asset(asset_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    row = fetch_asset_row(cursor, asset_id)
    conn.close()

    if not row:
        raise HTTPException(status_code=404, detail="Asset not found")

    return serialize_asset_row(row)


@app.post("/api/assets/upload")
async def upload_asset(file: UploadFile = File(...), name: str = None, description: str = None):
    ensure_supported_format(file.filename or "")

    try:
        file_ext = Path(file.filename).suffix.lower()
        asset_name = name or Path(file.filename).stem
        file_name = f"{slugify(asset_name)}-{datetime.now().strftime('%Y%m%d%H%M%S%f')}{file_ext}"
        file_path = UPLOAD_DIR / file_name

        with open(file_path, "wb") as handle:
            handle.write(await file.read())

        conn = get_db_connection()
        cursor = conn.cursor()
        asset_id = insert_asset_record(
            cursor,
            name=asset_name,
            description=description or "",
            file_path=file_path,
            file_format=file_ext.lstrip("."),
        )
        conn.commit()
        conn.close()

        return {
            "id": asset_id,
            "name": asset_name,
            "file_path": to_storage_path(file_path),
            "status": "uploaded",
        }
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Upload failed: {exc}") from exc


@app.get("/api/assets/{asset_id}/download")
async def download_asset(asset_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    row = fetch_asset_row(cursor, asset_id)
    conn.close()

    if not row:
        raise HTTPException(status_code=404, detail="Asset not found")

    asset = serialize_asset_row(row)
    file_path = resolve_storage_path(asset["file_path"])
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="File not found")

    download_name = f"{slugify(asset['name'], 'asset')}.{asset['file_format']}"
    return FileResponse(file_path, filename=download_name)


@app.post("/api/assets/{asset_id}/transform")
async def transform_asset(asset_id: int, transform: AssetTransform):
    conn = get_db_connection()
    cursor = conn.cursor()

    edit_data = {
        "position": transform.position,
        "rotation": transform.rotation,
        "scale": transform.scale,
    }

    cursor.execute(
        "INSERT INTO edits (asset_id, edit_type, data) VALUES (?, ?, ?)",
        (asset_id, "transform", json.dumps(edit_data)),
    )
    cursor.execute(
        "UPDATE assets SET updated_at = CURRENT_TIMESTAMP WHERE id = ?",
        (asset_id,),
    )
    conn.commit()
    conn.close()

    return {"status": "saved", "asset_id": asset_id}


@app.post("/api/assets/{asset_id}/material")
async def update_material(asset_id: int, material: AssetMaterial):
    conn = get_db_connection()
    cursor = conn.cursor()

    material_data = {
        "color": material.color,
        "metalness": material.metalness,
        "roughness": material.roughness,
    }

    cursor.execute(
        "INSERT INTO edits (asset_id, edit_type, data) VALUES (?, ?, ?)",
        (asset_id, "material", json.dumps(material_data)),
    )
    cursor.execute(
        "UPDATE assets SET updated_at = CURRENT_TIMESTAMP WHERE id = ?",
        (asset_id,),
    )
    conn.commit()
    conn.close()

    return {"status": "saved", "asset_id": asset_id}


@app.get("/api/assets/{asset_id}/edits")
async def get_edits(asset_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute(
        "SELECT * FROM edits WHERE asset_id = ? ORDER BY created_at DESC",
        (asset_id,),
    )
    rows = cursor.fetchall()
    conn.close()
    return {"edits": [serialize_edit_row(row) for row in rows]}


@app.delete("/api/assets/{asset_id}")
async def delete_asset(asset_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    row = fetch_asset_row(cursor, asset_id)

    if not row:
        conn.close()
        raise HTTPException(status_code=404, detail="Asset not found")

    asset = serialize_asset_row(row)
    cursor.execute("DELETE FROM edits WHERE asset_id = ?", (asset_id,))
    cursor.execute("DELETE FROM assets WHERE id = ?", (asset_id,))
    conn.commit()
    conn.close()

    file_path = resolve_storage_path(asset["file_path"])
    if file_path.exists():
        file_path.unlink()

    return {"status": "deleted", "asset_id": asset_id}


@app.post("/api/assets/{asset_id}/export")
async def export_asset(asset_id: int, format: str = "glb"):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    row = fetch_asset_row(cursor, asset_id)
    conn.close()

    if not row:
        raise HTTPException(status_code=404, detail="Asset not found")

    asset = serialize_asset_row(row)
    file_path = resolve_storage_path(asset["file_path"])
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="File not found")

    return FileResponse(file_path, filename=f"{slugify(asset['name'], 'asset')}.{format}")


@app.post("/api/generations")
async def create_generation_job(request: GenerationRequest):
    provider = get_generation_provider(os.getenv("GENERATION_PROVIDER", "mock"))
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    provider_options = {
        **request.options,
        "output_dir": str(GENERATED_CANDIDATE_DIR),
    }

    cursor.execute(
        """
        INSERT INTO generation_jobs (prompt, status, provider, options)
        VALUES (?, ?, ?, ?)
        """,
        (request.prompt, "processing", provider.name, json.dumps(provider_options)),
    )
    job_id = cursor.lastrowid

    cursor.execute("SELECT * FROM assets ORDER BY created_at DESC")
    available_assets = [serialize_asset_row(row) for row in cursor.fetchall()]

    try:
        result = provider.start_generation(
            request.prompt,
            request.candidateCount,
            provider_options,
            available_assets,
        )

        for candidate in result["candidates"]:
            cursor.execute(
                """
                INSERT INTO generation_candidates (
                    job_id,
                    provider_candidate_id,
                    name,
                    description,
                    preview_label,
                    status,
                    source_file_path,
                    local_file_path,
                    file_format,
                    metadata
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    job_id,
                    candidate["provider_candidate_id"],
                    candidate["name"],
                    candidate.get("description", ""),
                    candidate.get("preview_label", ""),
                    candidate.get("status", "ready"),
                    candidate.get("source_file_path"),
                    candidate.get("local_file_path"),
                    candidate.get("file_format", "glb"),
                    json.dumps(candidate.get("metadata", {})),
                ),
            )

        cursor.execute(
            """
            UPDATE generation_jobs
            SET status = ?, updated_at = CURRENT_TIMESTAMP
            WHERE id = ?
            """,
            (result.get("status", "ready"), job_id),
        )
        conn.commit()
    except GenerationProviderError as exc:
        cursor.execute(
            """
            UPDATE generation_jobs
            SET status = 'failed', error = ?, updated_at = CURRENT_TIMESTAMP
            WHERE id = ?
            """,
            (str(exc), job_id),
        )
        conn.commit()
        conn.close()
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        cursor.execute(
            """
            UPDATE generation_jobs
            SET status = 'failed', error = ?, updated_at = CURRENT_TIMESTAMP
            WHERE id = ?
            """,
            (str(exc), job_id),
        )
        conn.commit()
        conn.close()
        raise HTTPException(status_code=500, detail="Failed to create generation job") from exc

    conn.close()
    return fetch_generation_job_response(job_id)


@app.get("/api/generations/{job_id}")
async def get_generation_job(job_id: int):
    return fetch_generation_job_response(job_id)


@app.get("/api/generations/{job_id}/candidates/{candidate_id}/download")
async def download_generated_candidate(job_id: int, candidate_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    candidate_row = fetch_generation_candidate(cursor, job_id, candidate_id)
    conn.close()

    if not candidate_row:
        raise HTTPException(status_code=404, detail="Generated candidate not found")

    candidate = serialize_generation_candidate_row(candidate_row)
    file_path = resolve_storage_path(candidate.get("local_file_path") or candidate.get("source_file_path") or "")
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="Generated candidate file not found")

    file_ext = f".{candidate.get('file_format', 'glb')}"
    return FileResponse(file_path, filename=f"{slugify(candidate['name'], 'monster')}{file_ext}")


@app.post("/api/generations/{job_id}/select")
async def select_generated_candidate(job_id: int, request: CandidateSelectionRequest):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM generation_jobs WHERE id = ?", (job_id,))
    job_row = cursor.fetchone()
    candidate_row = fetch_generation_candidate(cursor, job_id, request.candidateId)

    if not job_row or not candidate_row:
        conn.close()
        raise HTTPException(status_code=404, detail="Generation job or candidate not found")

    job = serialize_generation_job_row(job_row)
    candidate = serialize_generation_candidate_row(candidate_row)
    source_path = resolve_storage_path(candidate.get("local_file_path") or candidate.get("source_file_path") or "")
    if not source_path.exists():
        conn.close()
        raise HTTPException(status_code=404, detail="Generated candidate file not found")

    file_ext = source_path.suffix.lower()
    ensure_supported_format(source_path.name)

    asset_name = request.name or candidate["name"]
    file_name = f"{slugify(asset_name, 'generated-monster')}-{datetime.now().strftime('%Y%m%d%H%M%S%f')}{file_ext}"
    destination_path = UPLOAD_DIR / file_name
    shutil.copy2(source_path, destination_path)

    metadata = {
        **candidate.get("metadata", {}),
        "generation_job_id": job_id,
        "generation_candidate_id": candidate["id"],
        "provider_candidate_id": candidate["provider_candidate_id"],
        "provider": job["provider"],
        "selected_at": datetime.utcnow().isoformat(),
    }
    description = request.description or candidate.get("description") or f"Generated from prompt: {job['prompt']}"

    try:
        asset_id = insert_asset_record(
            cursor,
            name=asset_name,
            description=description,
            file_path=destination_path,
            file_format=file_ext.lstrip("."),
            metadata=metadata,
        )

        traits = metadata.get("traits", {})
        rarity = traits.get("rarity", "generated")
        cursor.execute(
            """
            INSERT INTO monsters (name, base_asset_id, traits, rarity)
            VALUES (?, ?, ?, ?)
            """,
            (asset_name, asset_id, json.dumps(traits), rarity),
        )
        monster_id = cursor.lastrowid

        cursor.execute(
            """
            UPDATE generation_candidates
            SET local_file_path = ?, status = 'selected'
            WHERE id = ?
            """,
            (to_storage_path(destination_path), candidate["id"]),
        )
        conn.commit()
    except Exception as exc:
        conn.rollback()
        if destination_path.exists():
            destination_path.unlink()
        conn.close()
        raise HTTPException(status_code=500, detail="Failed to save generated candidate") from exc

    saved_asset = fetch_asset_row(cursor, asset_id)
    conn.close()

    return {
        "status": "saved",
        "asset": serialize_asset_row(saved_asset),
        "monsterId": monster_id,
    }


@app.post("/api/monsters/generate")
async def generate_monster(monster: MonsterGeneration):
    conn = get_db_connection()
    cursor = conn.cursor()

    try:
        cursor.execute(
            """
            INSERT INTO monsters (name, base_asset_id, traits, rarity)
            VALUES (?, ?, ?, ?)
            """,
            (monster.name, monster.baseAssetId, json.dumps(monster.traits), monster.rarity),
        )
        conn.commit()
        monster_id = cursor.lastrowid
        return {
            "id": monster_id,
            "name": monster.name,
            "baseAssetId": monster.baseAssetId,
            "traits": monster.traits,
            "rarity": monster.rarity,
            "status": "generated",
        }
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
    finally:
        conn.close()


@app.get("/api/monsters")
async def get_monsters():
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute(
        """
        SELECT m.*, a.file_path, a.name AS asset_name
        FROM monsters m
        LEFT JOIN assets a ON m.base_asset_id = a.id
        ORDER BY m.created_at DESC
        """
    )
    rows = cursor.fetchall()
    conn.close()

    monsters = [dict(row) for row in rows]
    for monster in monsters:
        monster["traits"] = safe_json_loads(monster.get("traits"), {})
    return {"monsters": monsters, "total": len(monsters)}


@app.get("/api/monsters/{monster_id}")
async def get_monster(monster_id: int):
    conn = get_db_connection(row_factory=True)
    cursor = conn.cursor()
    cursor.execute(
        """
        SELECT m.*, a.file_path, a.name AS asset_name
        FROM monsters m
        LEFT JOIN assets a ON m.base_asset_id = a.id
        WHERE m.id = ?
        """,
        (monster_id,),
    )
    row = cursor.fetchone()
    conn.close()

    if not row:
        raise HTTPException(status_code=404, detail="Monster not found")

    monster = dict(row)
    monster["traits"] = safe_json_loads(monster.get("traits"), {})
    return monster


@app.websocket("/ws/edit/{asset_id}")
async def websocket_endpoint(websocket: WebSocket, asset_id: int):
    await websocket.accept()
    try:
        while True:
            data = await websocket.receive_json()
            await websocket.send_json(
                {
                    "status": "received",
                    "asset_id": asset_id,
                    "data": data,
                }
            )
    except Exception as exc:
        print(f"WebSocket error: {exc}")


# ---------------------------------------------------------------------------
# ML sidecar endpoints (ported from the former ml-training/server.py).
# Models load lazily on first call so the server starts instantly without GPU.
# ---------------------------------------------------------------------------


class GestureRequest(BaseModel):
    pinch: float = 0.0
    grip: float = 0.0
    lx: float = 0.0
    ly: float = 0.0
    lz: float = 0.0


class VfxRequest(BaseModel):
    prompt: str = "photorealistic mountain texture"
    width: int = 256
    height: int = 256


class Mesh3DRequest(BaseModel):
    prompt: str = "fantasy dungeon pillar"


@app.post("/gesture")
async def gesture(request: GestureRequest):
    """Rule-based hand pose -> gesture name (Unity APIGestureRecognizer)."""
    if request.pinch > 0.7:
        name = "pinch"
    elif request.grip > 0.7:
        name = "fist"
    elif request.grip < 0.2:
        name = "open"
    else:
        name = "none"
    return {"gesture": name}


@app.post("/generate_vfx")
async def generate_vfx(request: VfxRequest):
    """Text -> texture (Stable Diffusion). Heavy/lazy; returns base64 PNG."""
    try:
        from services.image_generator import generate_image

        b64 = generate_image(request.prompt, width=request.width, height=request.height)
        return {"image": b64, "width": request.width, "height": request.height}
    except ImportError:
        return {
            "error": "Stable Diffusion not installed",
            "hint": "pip install diffusers transformers accelerate",
            "status": "not_implemented",
        }
    except Exception as exc:  # noqa: BLE001
        return {"error": str(exc), "status": "error"}


@app.post("/generate_3d")
async def generate_3d(request: Mesh3DRequest):
    """Text -> 3D mesh (Shap-E). Heavy/lazy; returns base64 OBJ."""
    try:
        from services.model_3d import generate_3d_model

        obj_b64 = generate_3d_model(request.prompt)
        return {"obj": obj_b64, "prompt": request.prompt}
    except ImportError as exc:
        return {
            "error": "3D model generation not installed",
            "hint": "pip install diffusers transformers accelerate",
            "status": "not_implemented",
            "detail": str(exc),
        }
    except Exception as exc:  # noqa: BLE001
        return {"error": str(exc), "status": "error"}


# ---------------------------------------------------------------------------
# Game API stubs: routes the Unity AIClient already calls but that are not yet
# implemented. They return safe HTTP 200 payloads so the client degrades
# gracefully (Unity has local fallbacks) instead of failing on 404.
# ---------------------------------------------------------------------------


async def _safe_json(request: Request) -> dict[str, Any]:
    try:
        body = await request.json()
        return body if isinstance(body, dict) else {}
    except Exception:  # noqa: BLE001
        return {}


@app.post("/api/v1/chunk/parameters")
async def chunk_parameters(request: Request):
    """Stub: map a prompt/seed to world-generator parameters."""
    data = await _safe_json(request)
    return {
        "version": "1",
        "implemented": False,
        "seed": int(data.get("seed", 0) or 0),
        "parameters": {
            "theme": data.get("theme", "temperate"),
            "aggression": 0.5,
            "spawnDensity": 0.5,
            "vegetationDensity": 0.5,
            "elevation": 0.5,
        },
        "notes": "Stub response; replace with LLM/heuristic mapping.",
    }


@app.post("/api/v1/voice/transcribe")
async def voice_transcribe(request: Request):
    """Stub: speech-to-text. Wire Whisper here later."""
    return {
        "version": "1",
        "implemented": False,
        "transcript": "",
        "confidence": 0.0,
        "notes": "Stub; integrate Whisper STT.",
    }


@app.post("/api/v1/message/process")
async def message_process(request: Request):
    """Stub: generic NL message -> action; reuses the heuristic intent parser."""
    data = await _safe_json(request)
    text = str(data.get("text", "")).strip()
    if text:
        return {"version": "1", "implemented": True, "intent": _heuristic_parse_intent(text)}
    return {"version": "1", "implemented": False, "errors": ["No text provided."]}


@app.post("/api/v1/dungeon/grow")
async def dungeon_grow(request: Request):
    """Stub: expand/advance a dungeon graph for the next floor."""
    data = await _safe_json(request)
    return {
        "version": "1",
        "implemented": False,
        "floor": int(data.get("floor", 1) or 1),
        "addedRooms": [],
        "notes": "Stub; deterministic growth currently handled client-side.",
    }


@app.post("/api/v1/opponent/generate")
async def opponent_generate(request: Request):
    """Stub: produce a rank-scaled opponent spec."""
    data = await _safe_json(request)
    return {
        "version": "1",
        "implemented": False,
        "opponent": {
            "catalogId": "creature.wolf.v1",
            "displayName": "Wolf",
            "scale": 1.0,
            "difficultyTier": str(data.get("tier", "normal")),
        },
        "notes": "Stub; returns a safe default opponent.",
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8001)
