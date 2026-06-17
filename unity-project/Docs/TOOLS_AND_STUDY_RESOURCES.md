# Tools and study resources (Aetheria VR + hybrid AI pipeline)

> **Mirror:** Keep this file aligned with `3d-monster-gen/docs/TOOLS_AND_STUDY_RESOURCES.md`. When you update tools or links in one repo, copy the changes to the other so RPG runtime and content pipeline stay parallel.

Use this file alongside development: **runtime game** (`aetheria-vr-rpg`) and **content/generation tooling** (`3d-monster-gen`). Links point to official docs or stable primary sources where possible.

---

## 1. Core stack you are building on

### Unity (LTS) + URP + XR (Quest 3)

| Topic | What to use it for | Link |
|--------|--------------------|------|
| Unity Manual | Engine fundamentals, lifecycle, assets | [Unity Manual](https://docs.unity3d.com/Manual/index.html) |
| Unity Learn | Structured paths (filter by VR if needed) | [Unity Learn](https://learn.unity.com/) |
| Universal Render Pipeline (URP) | Lighting, performance, mobile VR | [URP documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest) |
| XR | Cross-vendor XR setup | [Unity XR](https://docs.unity3d.com/Manual/XR.html) |
| XR Interaction Toolkit (XRI) | Grab, UI, locomotion patterns | [XRI documentation](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest) |
| OpenXR | Low-level VR plugin layer | [Unity OpenXR](https://docs.unity3d.com/Manual/xr-plugin-architecture.html) |

### Meta Quest / Android VR

| Topic | Link |
|--------|------|
| Meta Quest developer hub (overview) | [Meta Quest Developers](https://developer.oculus.com/) |
| Unity on Quest (getting started) | [Unity - Quest](https://developer.oculus.com/documentation/unity/unity-gs-overview/) |
| Performance / VRC guidelines (read early) | [Performance guidelines](https://developer.oculus.com/documentation/unity/unity-perf/) |
| Android build / device setup | [Unity Android](https://docs.unity3d.com/Manual/android.html) |

---

## 2. Procedural world and streaming (your infinite world)

| Tool / topic | Role | Link |
|--------------|------|------|
| Your codebase: chunk streaming | Reference implementation | `Assets/Scripts/ChunkManager.cs`, `ChunkGenerator.cs` |
| Noise / terrain concepts | Theory for biomes and height | [Perlin noise (Unity)](https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html) |
| Addressables | Load/unload bundles at runtime (production) | [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) |
| Unity Job System / Burst (optional) | Heavy procedural work off main thread | [Job System](https://docs.unity3d.com/Manual/JobSystem.html), [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest) |

---

## 3. AI: voice, text, intent, and backend services

| Topic | Role | Link |
|--------|------|------|
| FastAPI | HTTP APIs for intent, catalog, generation jobs | [FastAPI](https://fastapi.tiangolo.com/) |
| Pydantic | Request/response models | [Pydantic](https://docs.pydantic.dev/) |
| OpenAI API (or similar) | LLM intent parsing, dialogue (choose one vendor) | [OpenAI API](https://platform.openai.com/docs) |
| Whisper (speech-to-text) | Voice to text (cloud or self-host) | [OpenAI Whisper](https://platform.openai.com/docs/guides/speech-to-text) |

**Design note:** Ship with TLS, auth, and non-localhost URLs for Quest builds. Localhost-only backends do not work on device without tunneling or a deployed server.

---

## 4. 3D content: “realistic” assets vs AI-generated meshes

### DCC and polish (production quality)

| Tool | Role | Link |
|------|------|------|
| Blender | Modeling, retopo, UVs, rigging basics | [Blender](https://www.blender.org/) |
| Blender Manual | Official learning | [Blender Manual](https://docs.blender.org/manual/en/latest/) |
| ZBrush | High-detail sculpting (creatures) | [ZBrush](https://www.maxon.net/en/zbrush) |
| Maya | Studio-style rigging/animation (optional) | [Maya](https://www.autodesk.com/products/maya/overview) |

### Texturing and materials (big visual win on Quest)

| Tool | Role | Link |
|------|------|------|
| Substance 3D Painter | PBR texturing | [Substance 3D Painter](https://www.adobe.com/products/substance3d-painter.html) |
| Substance 3D Designer | Procedural materials | [Substance 3D Designer](https://www.adobe.com/products/substance3d-designer.html) |
| Quixel Megascans | Scan-based surfaces (use selectively on mobile VR) | [Megascans](https://quixel.com/megascans/home) |

### AI-assisted 3D (pipeline tools, not magic on-device)

Treat these as **candidate generators**; then retopo, LOD, rig, and optimize for Quest.

| Area | Notes | Starting point |
|------|--------|----------------|
| Text/image to 3D APIs | Vendor landscape changes fast; evaluate on: mesh quality, licensing, export formats, latency | Search current docs for: Tripo, Meshy, Rodin, Hunyuan3D (compare TOS + pricing) |
| ComfyUI | Node-based pipelines for concept art and control | [ComfyUI (GitHub)](https://github.com/comfyanonymous/ComfyUI) |

---

## 5. Animation and motion (believable creatures)

| Tool | Role | Link |
|------|------|------|
| Mixamo | Quick rigged humanoid animations (placeholders) | [Mixamo](https://www.mixamo.com/) |
| Rokoko | Motion capture | [Rokoko](https://www.rokoko.com/) |
| Cascadeur | Physics-aware posing / animation assistance | [Cascadeur](https://cascadeur.com/) |

---

## 6. Audio and “real-life feel” in VR

| Topic | Role | Link |
|--------|------|------|
| FMOD for Unity | Advanced audio middleware | [FMOD Unity](https://fmod.com/docs/2.02/unity/) |
| Wwise for Unity | Alternative AAA audio middleware | [Wwise Unity](https://www.audiokinetic.com/en/library/edge/?source=Unity&id=main) |
| Unity Audio | Basics and spatialization | [Unity Audio](https://docs.unity3d.com/Manual/class-AudioSource.html) |

---

## 7. Terrain and large environments (optional advanced pipeline)

| Tool | Role | Link |
|------|------|------|
| Houdini | Procedural environments, scattering, pipelines | [SideFX Houdini](https://www.sidefx.com/) |
| Houdini Engine for Unity | Bring Houdini tools into Unity | [Houdini Engine](https://www.sidefx.com/products/houdini-engine/) |
| SpeedTree | Vegetation tuned for games | [SpeedTree](https://store.speedtree.com/) |

---

## 8. Photogrammetry / scans (reference and hero assets)

| Tool | Role | Link |
|------|------|------|
| RealityCapture | Photogrammetry | [RealityCapture](https://www.capturingreality.com/) |
| Agisoft Metashape | Photogrammetry | [Metashape](https://www.agisoft.com/) |

---

## 9. Your repos: what maps where

| Repo | Primary purpose |
|------|------------------|
| `aetheria-vr-rpg` | Quest 3 client: streaming world, VR, combat hooks, `AIClient` / voice / intent wiring |
| `3d-monster-gen` | Studio-style generation jobs, asset gallery, provider seam; evolve toward production asset pipeline |

---

## 10. Suggested parallel study track (side by side with coding)

**Weeks 1–2: Unity VR foundation**

- URP lighting basics, XR rig, performance constraints on Android
- Read Meta performance guidance early

**Weeks 2–4: Architecture**

- Intent JSON contracts (spawn vs biome vs quest)
- Catalog-driven spawning (no raw mesh gen on device for core gameplay)
- Addressables mental model

**Ongoing: Content realism**

- Substance Painter + Blender retopo/LOD workflow
- Audio spatialization plan (FMOD/Wwise vs Unity-only)

**Ongoing: AI**

- FastAPI service boundaries
- Async job queues for long generation tasks (when you outgrow synchronous MVP)

---

## 11. Versioning

- File created for project planning and learning. Update vendor links if a tool rebrands or docs move.
