#!/usr/bin/env python3
"""
Aetheria VR - 3D model generator using Shap-E (text-to-3D).

Best for infinite dungeon: trees, rocks, pillars, torches.
Uses Hugging Face diffusers. First run downloads ~2GB model.

Pipeline: text prompt -> 3D mesh (OBJ) -> base64 for Unity.

Usage:
  from model_3d import generate_3d_model
  obj_b64 = generate_3d_model("fantasy dungeon pillar")
"""

import base64

_pipeline = None


def _get_pipeline():
    """Load Shap-E pipeline on first use."""
    global _pipeline
    if _pipeline is not None:
        return _pipeline
    try:
        from diffusers import DiffusionPipeline
        import torch

        device = "cuda" if torch.cuda.is_available() else "cpu"
        _pipeline = DiffusionPipeline.from_pretrained(
            "openai/shap-e",
            torch_dtype=torch.float16 if device == "cuda" else torch.float32,
        )
        _pipeline = _pipeline.to(device)
        return _pipeline
    except ImportError as e:
        raise ImportError(
            "Install: pip install diffusers transformers accelerate trimesh"
        ) from e


def generate_3d_model(
    prompt: str,
    guidance_scale: float = 15.0,
    num_inference_steps: int = 64,
) -> str:
    """
    Generate 3D mesh from text prompt. Returns base64-encoded OBJ.

    Args:
        prompt: Text description (e.g. "fantasy dungeon pillar", "rock boulder")
        guidance_scale: How closely to follow prompt
        num_inference_steps: More = better quality, slower

    Returns:
        Base64 string of OBJ file
    """
    pipe = _get_pipeline()
    result = pipe(
        prompt,
        guidance_scale=guidance_scale,
        num_inference_steps=num_inference_steps,
        output_type="mesh",
    )

    mesh = result.images[0]
    # Use diffusers export_to_ply (handles MeshDecoderOutput structure) then convert to OBJ
    import tempfile
    import os
    from diffusers.utils import export_to_ply

    with tempfile.NamedTemporaryFile(suffix=".ply", delete=False) as tmp:
        ply_path = tmp.name
    try:
        export_to_ply(mesh, ply_path)
        import trimesh
        tri_mesh = trimesh.load(ply_path)
        if isinstance(tri_mesh, trimesh.Scene):
            tri_mesh = tri_mesh.dump(concatenate=True)
        # Decimate to ~1500 faces for faster transfer (Shap-E outputs 10k+, ~4MB+ raw)
        try:
            tri_mesh = tri_mesh.simplify_quadric_decimation(face_count=1500)
        except Exception:
            pass
        obj_str = tri_mesh.export(file_type="obj")
        if isinstance(obj_str, bytes):
            obj_str = obj_str.decode("utf-8")
        return base64.b64encode(obj_str.encode("utf-8")).decode("utf-8")
    finally:
        if os.path.exists(ply_path):
            os.remove(ply_path)


def is_available() -> bool:
    """Check if diffusers/torch are installed (does not load full model)."""
    try:
        import diffusers  # noqa: F401
        import torch  # noqa: F401
        return True
    except ImportError:
        return False
