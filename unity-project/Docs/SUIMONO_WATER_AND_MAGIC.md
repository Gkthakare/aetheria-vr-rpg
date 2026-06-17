# SUIMONO water + Aetheria magic (HelloVR)

## What your scene uses

- **Object:** `Aetheria_SuimonoOcean` → `SUIMONO_Surface` → `Suimono_LOD0`
- **Material:** `Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/mat_water_surface.mat`
- **Shader:** `Suimono2/surface` → file `Assets/SUIMONO - WATER SYSTEM 2/SHADERS/Suimono2_surface.shader` (**code shader**, not Shader Graph)

## Tweak look (Inspector)

Select `mat_water_surface` → adjust tint, depth fade, foam, normals, refraction. Use **Edit…** on the shader only if you intend to maintain a **copy** of the shader (vendor updates).

## Shader Graph option (for `_MagicWaterPoint` / hub)

`WaterWorldInfluenceHub` sets globals for **custom** URP Shader Graphs. SUIMONO does **not** read them unless you modify its HLSL.

**Option A — Keep SUIMONO:** use **particle ripples** (`WaterRippleSpawner`, `RemoteWaterInfluence`) for interaction.

**Option B — Add PolyOne:** `Assets/PolyOne/Water URP/Shader/Shader Water.shadergraph` — add Blackboard properties `_MagicWaterPoint` (Vector4), `_MagicWaterStrength` (Float) and blend in the graph; assign that material to a test plane or second ocean layer.

## Layer / rays

Hand probe and long-range rays must hit a **collider** on the **Water** layer. If rays miss, add/adjust the SUIMONO collision mesh or a thin invisible **Mesh Collider** at sea level.

## `SuimonoInfinityWaterBootstrap` (WorldManager)

On **WorldManager**, the bootstrap now:

- **Scene Directional Light** — drag your sun; otherwise **Auto Find Directional Light** picks the first directional in the scene and sets `SuimonoModule.setLight`.
- **Default Flow Speed** — starts at **0.05** (calm); tune **Default Wave Scale** for slightly bigger swells without opening the surface prefab.
- **Enable Suimono Interaction** — keeps module + surface interaction on for Suimono’s own ripple path.
- **Suimono Ripple Fx Prefab** — drag `Assets/SUIMONO - WATER SYSTEM 2/EFFECTS/fx_rippleNormals.prefab` so ripples exist without the demo scene.

**Aetheria water magic** (orb, `RemoteWaterInfluence`) still uses your **particle ripple** prefab + **Water** layer; Suimono FX above is optional and separate.
