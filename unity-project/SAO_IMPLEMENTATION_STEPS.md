# Aetheria VR – SAO Anime-Style Implementation Guide

**Goal:** Transform Aetheria into a Sword Art Online–inspired anime VR experience.  
**Order:** Follow phases in sequence. Each step builds on the previous.

---

# Phase 1: Anime Visual Style (Day 1)

## Step 1.1: Create Toon/Cel-Shading Material Preset

**Why:** SAO uses cel-shading — flat color bands with sharp shadows. URP Lit supports this via **Receive Shadows** and **Specular Highlights** settings.

1. In **Project** window, right-click → **Create → Material**.
2. Name it `Mat_SAO_Toon`.
3. In Inspector:
   - **Shader:** `Universal Render Pipeline/Lit`
   - **Surface Type:** Opaque
   - **Workflow Mode:** Metallic
   - **Smoothness:** 0.3 (reduces glossy look)
   - **Metallic:** 0
   - **Receive Shadows:** On
   - **Specular Highlights:** Off (or very low)
4. **Save** (Ctrl+S).

**Use this material** on characters, weapons, and key props for anime look.

---

## Step 1.2: Add Rim Lighting (Anime Edge Glow)

**Why:** SAO characters have a subtle bright outline. URP Lit has a built-in **Rim Light** in the material.

1. Select `Mat_SAO_Toon`.
2. In Inspector, expand **Advanced Options** (or check if your URP Lit version has **Rim**).
3. If not available: We'll add via a custom shader or post-process. For now, use **Emission** for glowing edges:
   - Create a second material `Mat_SAO_Rim` for objects that need rim.
   - Or use **URP Lit** with **Specular** color set to a light blue/white for edge highlight.

**Alternative (Shader Graph):** Create a Shader Graph with:
- Albedo + stepped diffuse (2–3 steps) + Fresnel for rim.
- Save as `Assets/Shaders/SAOToon.shadergraph`.

---

## Step 1.3: Post-Processing for Anime Look

**Why:** Saturated colors, subtle bloom, and contrast give the anime feel.

1. **GameObject → Volume → Global Volume**
2. Name it `PostProcessing_SAO`.
3. In Inspector:
   - **Profile:** Click **New** to create a profile.
   - Save as `Assets/Settings/SAO_PostProfile.asset`.
4. **Add Override** (click **Add Override**):
   - **Color Adjustments**
     - **Post Exposure:** 0.1
     - **Contrast:** 10
     - **Saturation:** 15
     - **Color Filter:** Slight warm tint (R:1, G:0.98, B:0.95) — optional
   - **Bloom**
     - **Threshold:** 0.9
     - **Intensity:** 0.4
     - **Scatter:** 0.7
     - **Clamp:** 65472
   - **Vignette** (optional, light)
     - **Intensity:** 0.2
     - **Smoothness:** 0.4
5. Ensure **Main Camera** (or XR Origin camera) has **Universal Additional Camera Data** with **Render Post Processing** checked.

---

## Step 1.4: Environment Lighting for Anime

1. Select **Directional Light** in scene.
2. In Inspector:
   - **Color:** Slight warm white (R:1, G:0.98, B:0.92)
   - **Intensity:** 1.2
   - **Indirect Multiplier:** 1.0
3. **Window → Rendering → Lighting**
4. **Environment:**
   - **Skybox Material:** Mat_Skybox_Air (or your skybox)
   - **Ambient Mode:** Skybox
   - **Ambient Intensity:** 1.0
5. **Environment → Fog**
   - **Color:** Light blue-gray (R:0.7, G:0.8, B:0.9)
   - **Mode:** Linear
   - **Start:** 25
   - **End:** 80

---

# Phase 2: Combat VFX (Day 2)

## Step 2.1: Create Sword Hit Spark Particle

1. **GameObject → Effects → Particle System**
2. Name it `VFX_SwordHit`.
3. In Inspector, configure:
   - **Duration:** 0.3
   - **Looping:** Off
   - **Start Lifetime:** 0.15
   - **Start Speed:** 2
   - **Start Size:** 0.05
   - **Start Color:** Yellow-orange (R:1, G:0.8, B:0.2)
   - **Simulation Space:** World
   - **Emission:** Rate over Time = 0; **Bursts:** Count 8, Time 0
   - **Shape:** Sphere, Radius 0.1
   - **Renderer:** Material = Default-Particle (or URP Particles/Unlit)
   - **Color over Lifetime:** Fade out (alpha 1→0)
4. **Save as Prefab:** Drag into `Assets/Prefabs/FX/VFX_SwordHit.prefab` (create FX folder if needed).

---

## Step 2.2: Create Skill Effect Particle (Light Cut)

1. **GameObject → Effects → Particle System**
2. Name it `VFX_LightCut`.
3. Configure:
   - **Duration:** 0.5
   - **Start Lifetime:** 0.3
   - **Start Speed:** 3
   - **Start Size:** 0.08
   - **Start Color:** Cyan/blue (R:0.5, G:0.9, B:1)
   - **Emission:** Burst 15 at 0
   - **Shape:** Box (0.5 x 0.1 x 1) — slash shape
   - **Renderer:** Additive blend
4. Save as `Assets/Prefabs/FX/VFX_LightCut.prefab`.

---

## Step 2.3: Create AbilityVFX Component

Create a script to hook combat VFX into AbilitySystem:

**File:** `Assets/Scripts/Combat/AbilityVFX.cs`

```csharp
using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// SAO-style combat VFX. Assign to player or weapon. Hooks into AbilitySystem.
    /// </summary>
    public class AbilityVFX : MonoBehaviour
    {
        [Header("Hit Effects")]
        [SerializeField] private ParticleSystem hitSparks;
        [SerializeField] private Transform vfxSpawnPoint; // e.g. weapon tip or hand

        [Header("Skill Effects")]
        [SerializeField] private ParticleSystem lightCutVFX;
        [SerializeField] private ParticleSystem heavyCleaveVFX;
        [SerializeField] private ParticleSystem finisherVFX;

        private void Awake()
        {
            if (vfxSpawnPoint == null) vfxSpawnPoint = transform;
        }

        public void PlayHitSparks(Vector3 position)
        {
            if (hitSparks != null)
            {
                hitSparks.transform.position = position;
                hitSparks.Play();
            }
        }

        public void PlaySkillVFX(WeaponSkill skill)
        {
            Vector3 pos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            ParticleSystem ps = skill.Id switch
            {
                "light_cut" => lightCutVFX,
                "heavy_cleave" => heavyCleaveVFX,
                "finisher_thrust" => finisherVFX,
                _ => null
            };
            if (ps != null)
            {
                ps.transform.position = pos;
                ps.transform.rotation = vfxSpawnPoint != null ? vfxSpawnPoint.rotation : transform.rotation;
                ps.Play();
            }
        }
    }
}
```

---

## Step 2.4: Connect AbilitySystem to AbilityVFX

1. Open `AbilitySystem.cs`.
2. Add at top: `[SerializeField] private AbilityVFX abilityVFX;`
3. In `TryUseSkill`, after `cooldownUntil[key] = ...` add:
   ```csharp
   if (abilityVFX != null) abilityVFX.PlaySkillVFX(skill);
   ```
4. In scene: Add **AbilityVFX** component to same GameObject as AbilitySystem (or player).
5. Assign **Hit Sparks** = VFX_SwordHit prefab, **Light Cut VFX** = VFX_LightCut prefab.
6. Assign **VFX Spawn Point** = weapon tip or hand transform.

---

# Phase 3: Environment & Atmosphere (Day 3)

## Step 3.1: Skybox for SAO Feel

1. Use existing **Mat_Skybox_Air** or create new skybox.
2. For anime sky: **Create → Material** → Shader: `Skybox/Procedural` or `6 Sided`.
3. Set **Sky Tint** to light blue. **Ground** to soft gray.
4. Assign to **Lighting → Environment → Skybox Material**.

---

## Step 3.2: Dungeon Room Materials

1. Ensure **DungeonRoomVisualizer** has materials assigned:
   - **Mat_Room_Start** — green/safe
   - **Mat_Room_Boss** — red/danger
   - **Mat_Room_Key** — gold
   - **Mat_Room_Trap** — dark
2. Apply **Mat_SAO_Toon** to room pads for consistent anime look.

---

## Step 3.3: Portal Glow (SAO Teleport Gates)

1. Select **PortalToWeaponRoom** (or any portal).
2. Ensure **PortalVFX** (Dungeons) is attached.
3. Set **Glow Color** to cyan/blue (R:0.2, G:0.6, B:1).
4. Adjust **Pulse Speed** to 2, **Pulse Min/Max** for breathing effect.

---

# Phase 4: AI-Generated Anime Textures (Day 4)

## Step 4.1: Update Terrain Prompts for Anime

1. Open `TerrainTextureGenerator.cs`.
2. Change preset prompts to anime style:
   - **Mountain:** `anime style mountain rock texture, cel shaded, soft colors, fantasy`
   - **Water:** `anime style water surface, soft blue, cel shaded, fantasy game`
   - **Grass:** `anime style grass texture, soft green, cel shaded, JRPG`
   - **Forest:** `anime style forest floor, leaves, soft lighting, anime game`
   - **Sand:** `anime style sand texture, golden, soft, fantasy`
   - **Snow:** `anime style snow texture, soft white, cel shaded`

---

## Step 4.2: Use Anime Model in ML Server (Optional)

1. In `ml-training/image_generator.py`, change model:
   ```python
   # From:
   pipe = DiffusionPipeline.from_pretrained("runwayml/stable-diffusion-v1-5", ...)
   # To (anime base):
   pipe = DiffusionPipeline.from_pretrained("Linaqruf/anything-v3.0", ...)
   ```
2. Or add LoRA for anime (see `ML_ANIME_TRAINING_ROADMAP.md`).

---

# Phase 5: Polish & Checklist (Day 5)

## Step 5.1: Scene Hierarchy

Organize HelloVR:

```
HelloVR
├── Environment
│   ├── Directional Light
│   ├── PostProcessing_SAO
│   └── WindZone (if AmbientWorld)
├── World
│   ├── WorldManager
│   └── IslandWorldManager
├── FX
│   ├── VFXTestPlane
│   ├── TerrainTextureGenerator
│   └── ModelPoolManager
├── XR Origin
└── UI (future)
```

---

## Step 5.2: VR-Specific Checks

- [ ] **URP Asset:** Depth Texture = On
- [ ] **URP Asset:** Shadow Distance = 40–50
- [ ] **Quality (Android):** Shadow Cascades = 2
- [ ] **Post-processing:** Avoid heavy DoF, motion blur (VR sickness)
- [ ] **Bloom:** Keep intensity ≤ 0.5 for Quest

---

## Step 5.3: Test Flow

1. **Play** scene.
2. Press **J** — Light Cut skill + VFX.
3. Press **K** — Heavy Cleave + VFX.
4. Press **L** — Finisher + VFX.
5. Say **"generate mountain"** — anime terrain texture.
6. Walk through portal — glow effect.
7. Check skybox, fog, post-processing.

---

# Quick Reference: File Checklist

| File | Action |
|------|--------|
| `Mat_SAO_Toon` | Create material |
| `SAO_PostProfile` | Create post-processing profile |
| `VFX_SwordHit` | Create particle prefab |
| `VFX_LightCut` | Create particle prefab |
| `AbilityVFX.cs` | Create script |
| `AbilitySystem.cs` | Add AbilityVFX hook |
| `TerrainTextureGenerator.cs` | Update prompts to anime |

---

# Next Steps (After Phase 5)

- **Sword trail:** Trail Renderer on weapon for slash effect
- **Health/Stamina UI:** World-space canvas with anime style
- **Enemy hit reactions:** Flash white on hit
- **Boss arena:** Dedicated lighting and VFX
- **Anime character models:** Replace capsules with proper models + Mat_SAO_Toon

---

*Follow phases in order. Each step is designed to be completable in 15–30 minutes.*
