from __future__ import annotations

from abc import ABC, abstractmethod
import base64
import json
from pathlib import Path
import random
import re
import struct
import uuid


class GenerationProviderError(Exception):
    """Raised when a generation provider cannot produce candidates."""


class BaseGenerationProvider(ABC):
    name = "base"

    @abstractmethod
    def start_generation(self, prompt: str, candidate_count: int, options: dict, available_assets: list[dict]) -> dict:
        """Start a generation job and return normalized candidate metadata."""

    def get_generation_status(self, job_id: str) -> dict:
        """Providers with async jobs can override this later."""
        return {"job_id": job_id, "status": "ready"}

    def fetch_candidate_file(self, candidate: dict) -> Path:
        """Return the source file path for a normalized candidate."""
        file_path = candidate.get("local_file_path") or candidate.get("source_file_path")
        if not file_path:
            raise GenerationProviderError("Candidate does not include a downloadable file path.")
        return Path(file_path)


class MockMonsterGenerationProvider(BaseGenerationProvider):
    name = "mock"

    descriptor_pool = [
        "Abyssal",
        "Ashen",
        "Crystal",
        "Ember",
        "Feral",
        "Frost",
        "Gloom",
        "Iron",
        "Lunar",
        "Molten",
        "Storm",
        "Venom",
        "Void",
    ]
    species_pool = [
        "Behemoth",
        "Chimera",
        "Drake",
        "Golem",
        "Hydra",
        "Leviathan",
        "Maw",
        "Sentinel",
        "Stalker",
        "Titan",
        "Warden",
        "Wraith",
    ]
    size_pool = ["small", "medium", "large", "colossal"]
    element_pool = ["acid", "arcane", "ash", "crystal", "flame", "frost", "shadow", "storm", "void"]
    temperament_pool = ["aggressive", "cunning", "defensive", "predatory", "territorial", "unstable"]
    rarity_pool = ["common", "uncommon", "rare", "epic", "legendary"]

    def start_generation(self, prompt: str, candidate_count: int, options: dict, available_assets: list[dict]) -> dict:
        seed = options.get("seed") if isinstance(options, dict) else None
        output_dir = Path(options.get("output_dir", ".")).resolve()
        output_dir.mkdir(parents=True, exist_ok=True)
        rng = random.Random(str(seed or prompt).lower())
        shuffled_assets = [asset for asset in available_assets if asset.get("file_path")]
        rng.shuffle(shuffled_assets)

        candidates = []
        for index in range(candidate_count):
            source_asset = shuffled_assets[index % len(shuffled_assets)] if shuffled_assets else None
            traits = self._build_traits(rng, index)
            candidate_name = self._build_name(prompt, rng, index)
            file_path = output_dir / f"{slugify(candidate_name, 'monster')}-{uuid.uuid4().hex[:8]}.gltf"
            self._write_candidate_gltf(file_path, rng, traits, index)
            candidates.append(
                {
                    "provider_candidate_id": f"mock-{uuid.uuid4().hex[:10]}",
                    "name": candidate_name,
                    "description": self._build_description(prompt, source_asset, traits),
                    "preview_label": f"{traits['rarity'].title()} {traits['element'].title()}",
                    "status": "ready",
                    "source_file_path": str(file_path),
                    "local_file_path": str(file_path),
                    "file_format": "gltf",
                    "metadata": {
                        "prompt": prompt,
                        "traits": traits,
                        "source_asset_id": source_asset.get("id") if source_asset else None,
                        "source_asset_name": source_asset.get("name") if source_asset else None,
                        "mock_provider": True,
                    },
                }
            )

        return {
            "status": "ready",
            "provider": self.name,
            "candidates": candidates,
        }

    def _build_name(self, prompt: str, rng: random.Random, index: int) -> str:
        prompt_words = [
            word.capitalize()
            for word in re.findall(r"[A-Za-z0-9]+", prompt)
            if len(word) > 3
        ]
        prefix = prompt_words[index % len(prompt_words)] if prompt_words else rng.choice(self.descriptor_pool)
        suffix = rng.choice(self.species_pool)
        return f"{prefix} {suffix}"

    def _build_traits(self, rng: random.Random, index: int) -> dict:
        return {
            "size": self.size_pool[index % len(self.size_pool)],
            "element": rng.choice(self.element_pool),
            "temperament": rng.choice(self.temperament_pool),
            "rarity": self.rarity_pool[min(index, len(self.rarity_pool) - 1)],
            "headCount": rng.randint(1, 4),
            "limbStyle": rng.choice(["clawed", "armored", "spiked", "tentacled", "winged"]),
        }

    def _build_description(self, prompt: str, source_asset: dict, traits: dict) -> str:
        if source_asset:
            return (
                f"Prompt-driven concept generated from '{prompt}' using "
                f"{source_asset.get('name', 'gallery asset')} as optional inspiration. "
                f"This variant is {traits['temperament']} with a {traits['element']} affinity."
            )
        return (
            f"Procedural prompt-driven monster generated from '{prompt}'. "
            f"This variant is {traits['temperament']} with a {traits['element']} affinity."
        )

    def _write_candidate_gltf(self, file_path: Path, rng: random.Random, traits: dict, index: int) -> None:
        scale = {"small": 0.8, "medium": 1.0, "large": 1.25, "colossal": 1.55}[traits["size"]]
        width = (0.55 + rng.uniform(0.0, 0.35)) * scale
        height = (0.95 + rng.uniform(0.0, 0.45)) * scale
        depth = (0.45 + rng.uniform(0.0, 0.3)) * scale
        horn_height = 0.18 * traits["headCount"] * scale
        limb_push = 0.18 if traits["limbStyle"] in {"winged", "tentacled"} else 0.08

        vertices = [
            (0.0, height + horn_height, 0.0),
            (-width, 0.25 * scale, depth),
            (width * (1.0 + limb_push), 0.12 * scale, depth * 0.8),
            (width * 0.72, -height, -depth),
            (-width * (1.0 + limb_push), -height * 0.92, -depth * 0.65),
            (0.0, height * 0.35, depth * 1.35),
            (0.0, -height * 0.35, -depth * 1.25),
        ]
        indices = [
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 1,
            1, 5, 2,
            3, 6, 4,
            1, 4, 6,
            1, 6, 5,
            2, 5, 6,
            2, 6, 3,
        ]

        position_values = [component for vertex in vertices for component in vertex]
        position_bytes = struct.pack(f"<{len(position_values)}f", *position_values)
        index_bytes = struct.pack(f"<{len(indices)}H", *indices)
        buffer_bytes = position_bytes + index_bytes
        buffer_b64 = base64.b64encode(buffer_bytes).decode("ascii")

        min_values = [min(vertex[axis] for vertex in vertices) for axis in range(3)]
        max_values = [max(vertex[axis] for vertex in vertices) for axis in range(3)]
        color = self._color_for_element(traits["element"], index)

        gltf = {
            "asset": {"version": "2.0", "generator": "MockMonsterGenerationProvider"},
            "scene": 0,
            "scenes": [{"nodes": [0]}],
            "nodes": [{"mesh": 0, "name": f"monster-node-{index}"}],
            "meshes": [
                {
                    "name": f"monster-mesh-{index}",
                    "primitives": [
                        {
                            "attributes": {"POSITION": 0},
                            "indices": 1,
                            "material": 0,
                        }
                    ],
                }
            ],
            "materials": [
                {
                    "name": f"monster-material-{index}",
                    "pbrMetallicRoughness": {
                        "baseColorFactor": [color[0], color[1], color[2], 1.0],
                        "metallicFactor": 0.1,
                        "roughnessFactor": 0.75,
                    },
                    "doubleSided": True,
                }
            ],
            "buffers": [
                {
                    "byteLength": len(buffer_bytes),
                    "uri": f"data:application/octet-stream;base64,{buffer_b64}",
                }
            ],
            "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": len(position_bytes), "target": 34962},
                {"buffer": 0, "byteOffset": len(position_bytes), "byteLength": len(index_bytes), "target": 34963},
            ],
            "accessors": [
                {
                    "bufferView": 0,
                    "byteOffset": 0,
                    "componentType": 5126,
                    "count": len(vertices),
                    "type": "VEC3",
                    "min": min_values,
                    "max": max_values,
                },
                {
                    "bufferView": 1,
                    "byteOffset": 0,
                    "componentType": 5123,
                    "count": len(indices),
                    "type": "SCALAR",
                },
            ],
        }

        file_path.write_text(json.dumps(gltf), encoding="utf-8")

    def _color_for_element(self, element: str, index: int) -> tuple[float, float, float]:
        colors = {
            "acid": (0.56, 0.94, 0.32),
            "arcane": (0.56, 0.45, 0.98),
            "ash": (0.62, 0.62, 0.67),
            "crystal": (0.45, 0.88, 0.98),
            "flame": (0.95, 0.39, 0.26),
            "frost": (0.66, 0.88, 0.98),
            "shadow": (0.28, 0.28, 0.42),
            "storm": (0.37, 0.67, 0.98),
            "void": (0.18, 0.12, 0.32),
        }
        base_color = colors.get(element, (0.7, 0.7, 0.7))
        shift = min(index * 0.04, 0.16)
        return tuple(min(channel + shift, 1.0) for channel in base_color)


def slugify(value: str, fallback: str = "monster") -> str:
    normalized = "".join(char.lower() if char.isalnum() else "-" for char in value.strip())
    compact = "-".join(part for part in normalized.split("-") if part)
    return compact or fallback


def get_generation_provider(provider_name: str | None = None) -> BaseGenerationProvider:
    normalized_name = (provider_name or "mock").strip().lower()
    if normalized_name in {"mock", "dev", "offline"}:
        return MockMonsterGenerationProvider()
    raise GenerationProviderError(f"Unsupported generation provider '{normalized_name}'.")
