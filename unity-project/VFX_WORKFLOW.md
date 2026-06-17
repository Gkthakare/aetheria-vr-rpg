# Aetheria VFX Texture Workflow – Step by Step

Use this workflow to generate realistic textures with Stable Diffusion and apply them in your game.

---

## Step 1: Start the Server

```powershell
cd ml-training
python server.py
```

Ensure `diffusers`, `transformers`, and `accelerate` are installed. The model loads on first request (~1 min).

---

## Step 2: Generate Textures in Unity

1. Open **HelloVR** (or your scene with the VFX plane).
2. Select the **VFXTestPlane** (or the object with VFXTextureRequest).
3. **Add VFXPresetGenerator** (if not present): Add Component → search "VFXPresetGenerator".
4. Ensure it has:
   - **VFXTextureRequest** (Api Url: `http://localhost:8000/generate_vfx`)
   - **VFXPresetGenerator**
5. (Optional) Remove **VFXTestTrigger** – VFXPresetGenerator handles G and 1–8.
6. Press **Play**.
7. Use the keyboard:
   - **G** or **1** = Mountain
   - **2** = Water
   - **3** = Grass
   - **4** = Sky
   - **5** = Sand
   - **6** = Dungeon Wall
   - **7** = Forest Floor
   - **8** = Lava

8. Wait ~1 minute per texture (CPU) or ~10 seconds (GPU).
9. The texture appears on the plane.

---

## Step 3: Save Textures to Assets

1. While still in **Play** mode, after a texture you like has been generated:
2. Go to the menu: **Aetheria > Save Last Generated Texture**.
3. The texture is saved to `Assets/GeneratedTextures/`.
4. Unity imports it automatically.

---

## Step 4: Create Materials from Textures

1. In the **Project** window, go to `Assets/GeneratedTextures/`.
2. Select a texture (e.g. `photorealistic mountain rock texture, detailed, 4k.png`).
3. Right-click → **Aetheria > Create Material from Texture**.
4. A material is created next to the texture (e.g. `photorealistic mountain rock texture, detailed, 4k_Mat.mat`).

---

## Step 5: Use Textures in the Game

### Option A: Island World
1. Find **IslandWorldManager** (with IslandWorldBuilder).
2. Drag your materials into: **Road Material**, **Grass Material**, **Tree Foliage Material**, **Mountain Material**.
3. Enter Play mode – island builds with your textures.

### Option B: Dungeon
1. Find **WorldManager** (with DungeonRoomVisualizer).
2. Drag materials into **Mat Sand**, **Mat Grass**, **Mat Start**, etc.
3. Enter Play mode.

---

## Quick Reference

| Action | How |
|--------|-----|
| Generate texture | Play → Press G or 1–8 |
| Save to Assets | Aetheria > Save Last Generated Texture |
| Create material | Right-click texture → Aetheria > Create Material from Texture |
| Apply to dungeon | Drag material into DungeonRoomVisualizer slots |

---

## Custom Presets

1. **Project** → right-click → **Create > Aetheria > Environment Texture Presets**.
2. Name it (e.g. `MyPresets`).
3. Edit the prompts for each preset.
4. Assign it to **VFXPresetGenerator** in the Inspector.
