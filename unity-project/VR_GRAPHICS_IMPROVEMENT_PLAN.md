# Aetheria VR – Graphics & Immersion Improvement Plan

**Senior Unity VR Graphics Engineer Analysis**  
**Target:** SAO-style open-world VR exploration | **Platform:** Meta Quest | **Goal:** 90 FPS, AAA atmosphere

---

## Executive Summary

| Area | Current State | Priority |
|------|---------------|----------|
| **Pipeline** | URP 14.0.9 (correct) | ✅ Good |
| **Water Shader** | Built-in Surface Shader (incompatible) | 🔴 Critical |
| **World Gen** | Simple Perlin, no biomes/rivers | 🟡 High |
| **Performance** | No LOD, no pooling, no instancing | 🔴 Critical |
| **Lighting** | Single directional, no baked GI | 🟡 High |
| **Atmosphere** | No fog, no post-processing | 🟡 High |
| **Combat VFX** | None (TODO in AbilitySystem) | 🟡 High |
| **Vegetation** | Basic sway, no density maps | 🟢 Medium |

---

# Part 1: Graphics Pipeline

## 1.1 Current State

- **URP 14.0.9** is installed and active via `UniversalRenderPipelineGlobalSettings.asset`
- **WaterSimple.shader** uses `#pragma surface surf Standard` — **Built-in Surface Shader**. This will not render correctly in URP.

## 1.2 Critical Fix: Water Shader for URP

**Problem:** `WaterSimple.shader` uses Built-in CG surface shader. URP requires HLSL/Shader Graph.

**Solution:** Create a URP-compatible water shader or use PolyOne Water URP materials. For custom water:

```hlsl
// Assets/Shaders/WaterSimpleURP.shader - URP Lit-based water
Shader "Aetheria/Water Simple URP"
{
    Properties
    {
        _BaseColor ("Water Tint", Color) = (0.1, 0.35, 0.55, 0.88)
        _Smoothness ("Smoothness", Range(0,1)) = 0.95
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _BaseColor;
            half _Smoothness;
            half _FresnelPower;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 worldNormal : TEXCOORD0; float3 viewDir : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                o.viewDir = GetWorldSpaceViewDir(TransformObjectToWorld(v.positionOS.xyz));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.worldNormal);
                half3 V = normalize(i.viewDir);
                half fresnel = pow(1 - saturate(dot(V, N)), _FresnelPower);
                half alpha = _BaseColor.a * (0.7 + 0.3 * fresnel);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Transparent"
}
```

**Update ChunkManager** to use this shader when creating default water:

```csharp
// In GetOrCreateDefaultWaterMaterial():
var shader = Shader.Find("Aetheria/Water Simple URP") ?? Shader.Find("Aetheria/Water Simple");
```

## 1.3 URP Asset Settings for VR (Quest)

Create or edit your URP Asset:

1. **Edit → Project Settings → Graphics** → ensure URP Asset is assigned
2. In URP Asset Inspector:
   - **HDR:** On
   - **MSAA:** 2x or 4x (Quest 2/3)
   - **Render Scale:** 1.0 (avoid 0.8 on Quest)
   - **Depth Texture:** On (for fog, post-processing)
   - **Opaque Texture:** Off (save memory)
   - **Shadow Distance:** 40–60 (not 150)
   - **Shadows:** Soft, 1024 or 2048 resolution
   - **Cascade Count:** 2 (not 4)

3. **Quality Settings (Quest):**
   - Edit `QualitySettings.asset` or create Quest-specific override
   - **Shadow Distance:** 40
   - **Shadow Resolution:** 1 (1024)
   - **Pixel Light Count:** 1
   - **VSync:** 1 (Quest locks to 72/90)
   - **LOD Bias:** 0.8 (slightly lower for distant objects)

---

# Part 2: Lighting & Atmosphere

## 2.1 Baked Global Illumination

**Why:** Baked GI reduces runtime cost and improves immersion. SAO-style environments benefit from soft ambient lighting.

**Steps:**
1. **Window → Rendering → Lighting**
2. **Environment:** Set Skybox Material (e.g. Mat_Skybox_Air)
3. **Environment:** Ambient Source = Skybox
4. **Environment:** Ambient Intensity = 1.0
5. **Realtime Global Illumination:** Off (unless needed for day/night)
6. **Baked Global Illumination:** On
7. **Lighting Mode:** Baked Indirect
8. **Bake:** Click Generate Lighting

**Static objects:** Mark Island ground, mountains, buildings as **Static** for baking.

## 2.2 Mixed Lighting

- **Directional Light:** Set Mode to **Mixed**
- **Baked:** 1 (fully baked)
- **Realtime:** 0 (for now; add later for day/night)

## 2.3 Reflection Probes

**Why:** Water and metallic surfaces need reflections for realism.

1. **GameObject → Light → Reflection Probe**
2. Place in center of island or near water
3. **Type:** Baked
4. **Resolution:** 128 or 256
5. **Bake** from Lighting window

## 2.4 Light Probes

- Island: Add **Light Probe Group** around play area
- **Spacing:** 2–3 units
- Ensure prefabs (RoomPad, trees) use **Light Probes** in MeshRenderer

## 2.5 Dynamic Day/Night (Optional)

```csharp
// Assets/Scripts/World/TimeOfDayController.cs
using UnityEngine;

namespace Aetheria.World
{
    public class TimeOfDayController : MonoBehaviour
    {
        [SerializeField] private Light directionalLight;
        [SerializeField] private float dayLengthSeconds = 300f;
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private AnimationCurve sunIntensityCurve;

        private float _timeOfDay; // 0 = midnight, 0.5 = noon

        void Update()
        {
            _timeOfDay += Time.deltaTime / dayLengthSeconds;
            if (_timeOfDay > 1f) _timeOfDay -= 1f;

            float angle = _timeOfDay * 360f;
            directionalLight.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
            directionalLight.color = sunColorGradient.Evaluate(_timeOfDay);
            directionalLight.intensity = sunIntensityCurve.Evaluate(_timeOfDay);
        }
    }
}
```

---

# Part 3: Environment Detail & Procedural World

## 3.1 Biome Blending

**Current:** ChunkGenerator uses single noise → terrain type. No biome transitions.

**Improvement:** Add moisture/temperature for biome selection:

```csharp
// Add to ChunkGenerator.cs
public enum BiomeType { Ocean, Beach, Grassland, Forest, Mountain, Snow, Desert }

private (BiomeType biome, float blend) GetBiomeAt(Vector2 worldPos)
{
    float height = GenerateNoise(worldPos);
    float moisture = GenerateNoise(worldPos + new Vector2(seed, seed * 2) * 0.5f);
    float temp = height; // higher = colder

    if (height < -0.2f) return (BiomeType.Ocean, 1f);
    if (height < 0f) return (BiomeType.Beach, (height + 0.2f) / 0.2f);
    if (height > 0.6f) return (BiomeType.Mountain, 1f);
    if (height > 0.4f) return (BiomeType.Snow, (height - 0.4f) / 0.2f);
    if (moisture > 0.3f) return (BiomeType.Forest, 1f);
    if (moisture < -0.3f) return (BiomeType.Desert, 1f);
    return (BiomeType.Grassland, 1f);
}
```

## 3.2 Terrain Variation (Height)

**Current:** Flat tiles (cubes). No vertical variation.

**Improvement:** Add height variation per tile:

```csharp
// In ChunkGenerator: add height to ChunkData
public class ChunkData
{
    public Vector2Int chunkCoord;
    public TileType[,] tiles;
    public float[,] heights; // height offset per tile
    public bool isLoaded;
}

// In GenerateChunk: sample height noise
float heightNoise = Mathf.PerlinNoise((worldPos.x + seed * 2) * 0.05f, (worldPos.y + seed * 2) * 0.05f);
chunk.heights[x, z] = heightNoise * 0.5f; // 0-0.5m variation
```

**ChunkManager:** Apply `localPosition.y = heights[x,z]` when creating tiles.

## 3.3 Vegetation Density Maps

**Create:** `VegetationDensityGenerator.cs` — deterministic density per tile type:

```csharp
// Assets/Scripts/World/VegetationDensityGenerator.cs
using UnityEngine;

namespace Aetheria.World
{
    public static class VegetationDensityGenerator
    {
        public static float GetGrassDensity(Vector2 worldPos, int seed)
        {
            float n = Mathf.PerlinNoise((worldPos.x + seed) * 0.2f, (worldPos.y + seed) * 0.2f);
            return Mathf.Clamp01(n * 1.2f - 0.2f);
        }

        public static float GetTreeDensity(Vector2 worldPos, int seed)
        {
            float n = Mathf.PerlinNoise((worldPos.x + seed * 3) * 0.15f, (worldPos.y + seed * 3) * 0.15f);
            return n > 0.5f ? (n - 0.5f) * 2f : 0f;
        }
    }
}
```

Use in ChunkManager: only spawn grass/trees where density > threshold.

## 3.4 River Generation (Conceptual)

**Approach:** Use flow-field noise to carve river paths:

```csharp
// In ChunkGenerator: river mask
private bool IsRiverTile(Vector2 worldPos)
{
    float flowX = Mathf.PerlinNoise((worldPos.x + seed) * 0.02f, 0) * 2 - 1;
    float flowZ = Mathf.PerlinNoise(0, (worldPos.y + seed) * 0.02f) * 2 - 1;
    float flowMag = new Vector2(flowX, flowZ).magnitude;
    float height = GenerateNoise(worldPos);
    return height < -0.1f && flowMag < 0.3f; // low valleys with low flow
}
```

## 3.5 Random Rock & Tree Placement

**ChunkManager:** Use seeded random per tile:

```csharp
// Replace Random.value with seeded RNG
private int GetTileSeed(int chunkX, int chunkZ, int x, int z)
{
    return (chunkX * 1000 + chunkZ) * 256 + x * 16 + z;
}

// In CreateTile:
int tileSeed = GetTileSeed(chunkData.chunkCoord.x, chunkData.chunkCoord.y, x, z);
Random.InitState(tileSeed);
if (Random.value < aiDecorChance) { /* spawn */ }
```

---

# Part 4: VR Performance Optimization

## 4.1 Target: 90 FPS

- **Quest 2:** 72 Hz default; 90 Hz with App SpaceWarp
- **Quest 3:** 90 Hz native
- **Budget:** ~11ms frame time

## 4.2 GPU Instancing

**ChunkManager:** All tiles use same material. Enable instancing:

```csharp
// Ensure materials use URP Lit with "Enable GPU Instancing" checked
// Or: CreateMaterialForChunk() with enableInstancing = true
var mat = new Material(URPMaterialHelper.GetLitShader());
mat.enableInstancing = true;
```

**Important:** Each tile is a separate GameObject. For true instancing, use **Graphics.DrawMeshInstanced** or **Mesh.CombineMeshes** per chunk.

## 4.3 Mesh Combining (Chunk Batching)

**Biggest win:** Combine all tiles in a chunk into one mesh:

```csharp
// Assets/Scripts/World/ChunkMeshCombiner.cs
using UnityEngine;
using System.Collections.Generic;

namespace Aetheria.World
{
    public static class ChunkMeshCombiner
    {
        public static Mesh CombineChunkTiles(List<MeshFilter> tiles)
        {
            var combine = new CombineInstance[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                combine[i].mesh = tiles[i].sharedMesh;
                combine[i].transform = tiles[i].transform.localToWorldMatrix;
            }
            var mesh = new Mesh();
            mesh.CombineMeshes(combine, true, true);
            return mesh;
        }
    }
}
```

**ChunkManager:** Build chunk as list of tiles, then combine into single MeshFilter + MeshRenderer. Use **submeshes** for different materials (water vs grass vs sand).

## 4.4 Object Pooling for Chunks

**Current:** `Destroy`/`Instantiate` chunks. **Improvement:** Pool chunk GameObjects:

```csharp
// In ChunkManager: add pooling
private Queue<GameObject> chunkPool = new Queue<GameObject>();
private const int POOL_SIZE = 50;

private GameObject GetPooledChunk()
{
    if (chunkPool.Count > 0)
    {
        var go = chunkPool.Dequeue();
        go.SetActive(true);
        return go;
    }
    return null; // Or create new
}

private void ReturnToPool(GameObject chunk)
{
    chunk.SetActive(false);
    chunkPool.Enqueue(chunk);
}
```

## 4.5 LOD Groups

**IslandWorldBuilder trees:** Add LOD Group:

```csharp
// When creating tree prefab:
var lodGroup = tree.AddComponent<LODGroup>();
lodGroup.SetLODs(new LOD[]
{
    new LOD(0.5f, new Renderer[] { trunk.GetComponent<Renderer>(), foliage.GetComponent<Renderer>() }),
    new LOD(0.2f, new Renderer[] { foliage.GetComponent<Renderer>() }), // trunk only at far
    new LOD(0f, new Renderer[] { }) // cull
});
```

**ChunkManager:** For distant chunks, use lower-detail mesh (e.g. fewer subdivisions).

## 4.6 Occlusion Culling

- Mark large objects (mountains, buildings) as **Occluder Static**
- Use **Occlusion Culling** window (Window → Rendering → Occlusion Culling) for baked occlusion in indoor scenes

## 4.7 Reduce Draw Calls

- **Max:** 100–150 draw calls per frame on Quest
- **ChunkManager:** Combining meshes reduces from 256+ to 1–6 per chunk
- **Materials:** Use texture atlases for terrain to reduce material count

---

# Part 5: Shaders & Materials

## 5.1 PBR Best Practices

- **All terrain materials:** URP Lit with Albedo, Normal, Metallic/Smoothness
- **Normal maps:** Add for grass, rock, sand. Improves depth perception in VR.
- **Height maps:** Optional parallax for close-up rocks.

## 5.2 Glowing Swords / Magic

**Shader Graph:** Create `URP/Unlit` with:
- **Emission** color from `_BaseColor`
- **Fresnel** for edge glow
- **HDR** emission (2–4x intensity)

**Material:** Assign to sword blade; animate emission via script on attack.

## 5.3 Interactive Surfaces

**Portal:** Use `_BaseColor` + `UV scroll` (already in PortalVFX). Add `_Emission` pulse for SAO-style glow.

---

# Part 6: Particle Effects

## 6.1 Sword Hit Sparks

```csharp
// Add to AbilitySystem or create AbilityVFX.cs
[SerializeField] private ParticleSystem hitSparks;

public void PlayHitVFX(Vector3 position)
{
    if (hitSparks != null)
    {
        hitSparks.transform.position = position;
        hitSparks.Play();
    }
}
```

**Particle settings:** Burst 5–10, lifetime 0.2s, speed 2, size 0.05, color yellow-orange, Additive.

## 6.2 Magic Attack Glow

- **Emission:** Radial burst, blue glow
- **Trail:** Ribbon trail from weapon tip
- **URP Particles/Unlit** with `_BaseColor` HDR

## 6.3 Environmental Dust

- **AmbientWorld:** Add dust particles (low rate, large area, slow fall)
- **Wind:** Use `WindZone` to affect particles (already in AmbientWorld)

---

# Part 7: Post Processing (VR-Safe)

## 7.1 Safe Effects

- **Color Grading:** Slight saturation, contrast. Avoid heavy tint.
- **Bloom:** Low intensity (0.3–0.5), threshold 1.0
- **Ambient Occlusion:** SSAO or HBAO low. **Avoid:** Strong vignette, motion blur, depth of field (causes sickness)

## 7.2 Add Volume to HelloVR

1. **GameObject → Volume → Global Volume**
2. **Profile:** Create new or use PolyOne PostProcessing Profile
3. **Add Override:** Color Adjustments, Bloom (low)
4. **Post Process Layer:** Ensure Main Camera has it (URP adds automatically)

## 7.3 Fog (Volumetric)

**URP:** Add **Fog** override to volume:
- **Color:** Slight blue-gray
- **Density:** 0.01–0.02
- **Start:** 20, **End:** 80

**Why:** Adds depth, hides distant LOD pop-in, SAO atmosphere.

---

# Part 8: World Immersion

## 8.1 Ambient Animals

**AnimalWander:** Already exists. Improve:
- Add **AudioSource** for footsteps (optional)
- Add **Animator** with idle/walk cycles
- Use **NavMesh** for pathfinding instead of MoveTowards

## 8.2 Wind Effects

**WindZone:** Already in AmbientWorld. **TreeSway** and **GrassWobble** add motion. **ALPWindIntegration** connects to ALP trees.

**Improvement:** Add `_WindStrength` to custom grass shader for GPU-driven wind.

## 8.3 Dynamic Weather (Optional)

```csharp
// Assets/Scripts/World/WeatherController.cs
public enum Weather { Clear, Cloudy, Rain, Storm }
public class WeatherController : MonoBehaviour
{
    [SerializeField] private ParticleSystem rainParticle;
    [SerializeField] private Light directionalLight;
    [SerializeField] private float transitionDuration = 10f;

    public void SetWeather(Weather w)
    {
        if (w == Weather.Rain && rainParticle != null) rainParticle.Play();
        else if (rainParticle != null) rainParticle.Stop();
        // Lerp light intensity, skybox tint
    }
}
```

---

# Part 9: NPC Improvements

## 9.1 Head Tracking

```csharp
// Assets/Scripts/NPC/NPCHeadTracking.cs
using UnityEngine;

namespace Aetheria.NPC
{
    public class NPCHeadTracking : MonoBehaviour
    {
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform player;
        [SerializeField] private float turnSpeed = 5f;
        [SerializeField] private float maxAngle = 60f;

        void Update()
        {
            if (headBone == null || player == null) return;
            Vector3 dir = (player.position - headBone.position).normalized;
            Quaternion target = Quaternion.LookRotation(dir);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
```

## 9.2 Idle Animations

- Use **Animator** with idle/wave blend tree
- **Animation Events** for audio (greeting, etc.)

---

# Part 10: VR Interaction Improvements

## 10.1 Haptic Feedback

```csharp
// When grabbing or hitting:
UnityEngine.XR.InputDevice device = ...; // Get controller
device.SendHapticImpulse(0, 0.1f, 0.05f); // channel, amplitude, duration
```

## 10.2 Hand Pose Blending

- Use **XR Hand Interaction** components from Meta SDK
- **HandPoseBlender** for smooth transitions

---

# Part 11: Scene Organization

## Recommended Hierarchy

```
HelloVR
├── Environment
│   ├── Lighting (Directional Light, Reflection Probe)
│   ├── PostProcessing (Global Volume)
│   └── Ambient (WindZone, AmbientWorld)
├── World
│   ├── WorldManager (ChunkGenerator, ChunkManager)
│   ├── IslandWorldManager (IslandWorldBuilder, GameStartManager)
│   └── DungeonRoomVisualizer
├── Characters
│   └── XR Origin (XR Rig)
├── NPC
│   └── (future NPCs)
├── FX
│   ├── VFXTestPlane
│   ├── ModelPoolManager
│   └── TerrainTextureGenerator
├── Audio
│   └── (ambient audio sources)
└── UI
    └── (VR canvases)
```

---

# Part 12: Prioritized Implementation List

## Phase 1: Quick Visual Wins (1–2 days)

| # | Task | Impact |
|---|------|--------|
| 1 | Fix WaterSimple shader for URP (or use PolyOne water) | 🔴 Critical |
| 2 | Add URP Volume with Fog + Bloom + Color Grading | High |
| 3 | Reduce shadow distance to 40–50 for Quest | High |
| 4 | Bake lighting for island (static objects) | High |
| 5 | Add Reflection Probe near water | Medium |

## Phase 2: Medium Complexity (3–5 days)

| # | Task | Impact |
|---|------|--------|
| 6 | Chunk mesh combining (reduce draw calls) | 🔴 Critical |
| 7 | Biome blending in ChunkGenerator | High |
| 8 | Vegetation density maps | Medium |
| 9 | Seeded random for decor placement | Medium |
| 10 | Add Combat VFX (sparks, glow) to AbilitySystem | High |
| 11 | Object pooling for chunks | Medium |

## Phase 3: Advanced Graphics (1–2 weeks)

| # | Task | Impact |
|---|------|--------|
| 12 | LOD groups for trees | Medium |
| 13 | Dynamic day/night | High |
| 14 | River generation | Medium |
| 15 | NPC head tracking | Medium |
| 16 | Weather system | Medium |
| 17 | Height variation in terrain | High |

---

# Part 13: Biggest Visual Impact

**Top 5 changes for SAO-style immersion:**

1. **Fog + Atmosphere** — Depth, mood, hides distant pop-in
2. **Baked GI + Lighting** — Soft ambient, no flat look
3. **Water shader fix** — Realistic water surface
4. **Combat VFX** — Sword sparks, magic glow
5. **Chunk mesh combining** — Enables 90 FPS, more draw budget for effects

---

# Part 14: Code Structure Recommendations

- **Extract** `ChunkVisualBuilder` from ChunkManager — separates rendering from logic
- **Create** `WorldGenerationConfig` ScriptableObject — noise params, biome thresholds, LOD distances
- **Extract** `ChunkPool` — dedicated pooling class
- **Create** `VFXManager` — centralizes all particle/effect triggers

---

*Document generated for Aetheria VR RPG. Apply changes incrementally and profile on Quest hardware.*
