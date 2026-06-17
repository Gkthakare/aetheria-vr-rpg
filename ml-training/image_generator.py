#!/usr/bin/env python3
"""
Aetheria VR - Image generator using Stable Diffusion.

Generates textures for mountains, water, sky, trees, etc.
Uses Hugging Face diffusers. First run downloads ~4GB model.

Usage:
  from image_generator import generate_image
  b64 = generate_image("photorealistic mountain texture")
"""

import base64
import io

# Lazy load - only import when first used
_pipeline = None


def _get_pipeline():
    """Load Stable Diffusion pipeline on first use."""
    global _pipeline
    if _pipeline is not None:
        return _pipeline
    try:
        from diffusers import StableDiffusionPipeline
        import torch

        device = "cuda" if torch.cuda.is_available() else "cpu"
        if device == "cpu":
            # Use float32 on CPU to avoid MPS issues on Mac
            _pipeline = StableDiffusionPipeline.from_pretrained(
                "runwayml/stable-diffusion-v1-5",
                torch_dtype=torch.float32,
                safety_checker=None,
            )
        else:
            _pipeline = StableDiffusionPipeline.from_pretrained(
                "runwayml/stable-diffusion-v1-5",
                torch_dtype=torch.float16,
                safety_checker=None,
            )
        _pipeline = _pipeline.to(device)
        return _pipeline
    except ImportError as e:
        raise ImportError(
            "Install diffusers: pip install diffusers transformers accelerate"
        ) from e


def generate_image(
    prompt: str,
    width: int = 256,
    height: int = 256,
    num_inference_steps: int = 25,
    guidance_scale: float = 7.5,
) -> str:
    """
    Generate image from text prompt. Returns base64-encoded PNG.

    Args:
        prompt: Text description (e.g. "photorealistic mountain texture")
        width: Output width (default 256 for textures)
        height: Output height
        num_inference_steps: SD steps (fewer = faster, lower quality)
        guidance_scale: How closely to follow prompt

    Returns:
        Base64 string of PNG image
    """
    pipe = _get_pipeline()
    image = pipe(
        prompt=prompt,
        width=width,
        height=height,
        num_inference_steps=num_inference_steps,
        guidance_scale=guidance_scale,
    ).images[0]

    buf = io.BytesIO()
    image.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")


def is_available() -> bool:
    """Check if diffusers/torch are installed (does not load full model)."""
    try:
        import diffusers  # noqa: F401
        import torch  # noqa: F401
        return True
    except ImportError:
        return False
