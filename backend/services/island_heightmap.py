"""
Procedural island heightmap (PNG) for Unity Terrain.
Radial falloff + FBM noise — no ML required; stable and fast.
"""

from __future__ import annotations

import io
import math
from typing import Any

try:
    from PIL import Image
except ImportError as e:  # pragma: no cover
    raise ImportError("Install Pillow: pip install Pillow") from e


def _smoothstep(edge0: float, edge1: float, x: float) -> float:
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0 + 1e-9)))
    return t * t * (3.0 - 2.0 * t)


def _hash2(ix: int, iy: int, seed: int) -> float:
    n = math.sin((ix * 127.1 + iy * 311.7 + seed * 0.001) * 12.9898) * 43758.5453
    return n - math.floor(n)


def _value_noise(nx: float, ny: float, seed: int) -> float:
    ix, iy = int(math.floor(nx)), int(math.floor(ny))
    fx, fy = nx - ix, ny - iy
    ux = fx * fx * (3.0 - 2.0 * fx)
    uy = fy * fy * (3.0 - 2.0 * fy)
    a = _hash2(ix, iy, seed)
    b = _hash2(ix + 1, iy, seed)
    c = _hash2(ix, iy + 1, seed)
    d = _hash2(ix + 1, iy + 1, seed)
    return a * (1 - ux) * (1 - uy) + b * ux * (1 - uy) + c * (1 - ux) * uy + d * ux * uy


def _fbm(x: float, y: float, seed: int, octaves: int = 5) -> float:
    amp = 0.5
    freq = 1.0
    total = 0.0
    norm = 0.0
    for _ in range(octaves):
        total += amp * _value_noise(x * freq + seed * 0.1, y * freq - seed * 0.07, seed)
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return total / max(norm, 1e-6)


def _valid_terrain_size(n: int) -> bool:
    """Unity heightmap: power-of-two + 1 (33 … 1025)."""
    if n < 33 or n > 1025:
        return False
    return (n - 1) & (n - 2) == 0 and (n - 1) > 0


def generate_heightmap_png(
    size: int,
    seed: int = 0,
    *,
    beach_start: float = 0.62,
    beach_width: float = 0.22,
    noise_scale: float = 4.5,
    ridge_boost: float = 0.12,
    ocean_rim_start: float = 0.97,
) -> tuple[bytes, dict[str, Any]]:
    """
    Returns (PNG bytes, metadata dict).

    Height is 0–255 in PNG (beach ~ low, center elevated). Unity maps to TerrainData heights 0–1.
    sea_level_normalized: height at the flattened outer rim (for aligning Suimono / Terrain base Y).
    ocean_rim_start: radial distance beyond which height is forced to 0 so the terrain edge meets water cleanly.
    """
    if not _valid_terrain_size(size):
        raise ValueError(
            f"size must be Unity terrain resolution (33, 65, 129, 257, 513, 1025), got {size}"
        )

    img = Image.new("L", (size, size))
    px = img.load()
    samples_edge: list[float] = []
    rim = max(0.0, min(1.0, ocean_rim_start))

    for j in range(size):
        for i in range(size):
            u = (i + 0.5) / size
            v = (j + 0.5) / size
            dx, dy = u - 0.5, v - 0.5
            r = math.sqrt(dx * dx + dy * dy) / 0.70710678
            r = min(r, 1.0)

            # Land mass: 1 at center → 0 toward edge
            land = 1.0 - _smoothstep(beach_start, beach_start + beach_width, r)
            land = land * land

            n = _fbm(u * noise_scale, v * noise_scale, seed, 5)
            ridge = _fbm(u * noise_scale * 2.2 + 10.0, v * noise_scale * 2.2, seed + 31, 3)
            h = land * (0.72 + 0.28 * n + ridge_boost * (ridge - 0.5))
            h = max(0.0, min(1.0, h))
            if r >= rim:
                h = 0.0
            px[i, j] = int(round(h * 255.0))

            if r > 0.94:
                samples_edge.append(h)

    if rim < 1.0:
        sea_level_normalized = 0.0
    else:
        sea_level_normalized = sum(samples_edge) / max(len(samples_edge), 1)

    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    meta: dict[str, Any] = {
        "width": size,
        "height": size,
        "sea_level_normalized": round(sea_level_normalized, 4),
        "ocean_rim_start": round(rim, 4),
        "seed": seed,
    }
    return buf.getvalue(), meta


def generate_heightmap_raw_meta(
    size: int,
    seed: int,
    world_size_meters: float,
    terrain_height_meters: float,
    **kwargs: Any,
) -> dict[str, Any]:
    """JSON-friendly meta + base64 PNG for clients that prefer one round-trip."""
    import base64

    png, inner = generate_heightmap_png(size, seed, **kwargs)
    meta = {
        "width": inner["width"],
        "height": inner["height"],
        "world_size_meters": world_size_meters,
        "terrain_height_meters": terrain_height_meters,
        "sea_level_normalized": inner["sea_level_normalized"],
        "ocean_rim_start": inner.get("ocean_rim_start"),
        "seed": seed,
        "png_base64": base64.b64encode(png).decode("ascii"),
    }
    return meta
