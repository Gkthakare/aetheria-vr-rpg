"""
SDXL Concept Art Generation
Generates ultra-detailed creature concept art for the Forest Guardian Beast
"""

import torch
from diffusers import StableDiffusionXLPipeline
from PIL import Image
import os
import random
import yaml

# Load settings
CONFIG_PATH = "config/settings.yaml"
with open(CONFIG_PATH, 'r') as f:
    config = yaml.safe_load(f)

OUTPUT_DIR = "data/concept_art"
os.makedirs(OUTPUT_DIR, exist_ok=True)

print("🎨 SDXL Concept Art Generator")
print("=" * 50)
print(f"🖥️  Device: {config['gpu']['device']}")
print(f"📊 Data type: {config['sdxl']['dtype']}")
print(f"📁 Output: {OUTPUT_DIR}")
print()

# Initialize pipeline
print("📥 Loading SDXL model...")
print("   (First run downloads ~6-7GB)")

pipe = StableDiffusionXLPipeline.from_pretrained(
    config['sdxl']['model_id'],
    torch_dtype=torch.float16 if config['sdxl']['dtype'] == "float16" else torch.float32,
    use_safetensors=config['sdxl']['use_safetensors']
).to(config['gpu']['device'])

print("✅ Model loaded")
print()

# Concept prompt for Forest Guardian Beast
prompt = """
Ultra-detailed concept art of a Forest Guardian Beast,
ancient tree monster with bark skin, glowing green veins,
branch antlers, moss-covered body, spider-like lower limbs,
dark fantasy forest, cinematic lighting, high detail,
professional character design, dramatic shadows, mystical aura.
"""

negative_prompt = "blurry, low quality, distorted, ugly, bad anatomy"

print("🎬 Generating concept art...")
print(f"   Prompt: {prompt[:60]}...")
print(f"   Steps: {config['sdxl']['inference_steps']}, Scale: {config['sdxl']['guidance_scale']}")
print()

# Generate image
with torch.no_grad():
    result = pipe(
        prompt=prompt,
        negative_prompt=negative_prompt,
        num_inference_steps=config['sdxl']['inference_steps'],
        guidance_scale=config['sdxl']['guidance_scale'],
        height=config['sdxl']['height'],
        width=config['sdxl']['width']
    )

image = result.images[0]

# Save image
file_path = f"{OUTPUT_DIR}/forest_guardian_{random.randint(1000, 9999)}.png"
image.save(file_path)

print("✅ Concept art generated!")
print(f"📸 Saved to: {file_path}")
print(f"   Resolution: {image.width}x{image.height}")
print()
print("🚀 Next step: Run scripts/generate_multiview.py")
