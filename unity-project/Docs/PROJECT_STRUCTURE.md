# Aetheria Unity project — structure

**Editor:** Unity **2022.3 LTS** (Quest / URP). Root: `unity-project/`.

## First-party code layout (`Assets/Scripts/`)

| Path | Assembly | Purpose |
|------|----------|---------|
| `Aetheria/Environment/` | `Aetheria.Environment` | Global wind / time / darkness / sea level → shader globals (`EnvironmentShaderHub`) |
| `Aetheria/WaterMagic/` | `Aetheria.WaterMagic` | Water touch, orb, spells (`Docs/WATER_MAGIC_SETUP.md`) |
| `Aetheria/Magic/` | `Aetheria.Magic` | Long-range rays, remote water, shader pulses, intent bridge, dimensions (`Docs/LONG_RANGE_MAGIC.md`) |
| `AI/` | *(default)* | Intent client, terrain texture helpers |
| `Combat/` | *(default)* | Combat hooks |
| `Dungeons/` | *(default)* | Dungeon flow |
| `Gameplay/` | *(default)* | Spawn director, catalog |
| `Training/` | *(default)* | Training / ML hooks |
| `VR/` | *(default)* | VR-specific helpers |
| `World/` | *(default)* | Island builder, chunk world |

**Rule:** New Aetheria gameplay systems go under `Scripts/Aetheria/<SystemName>/` with an **asmdef** when the folder is large enough to benefit (same pattern as `WaterMagic`).

## Third-party & content

- Vendor assets stay in their existing top-level folders (`SUIMONO - WATER SYSTEM 2`, `XR`, etc.).
- Pipeline-generated meshes: import under `Assets/ImportedContent/...` (create if missing); do not mix with `Scripts/`.

## Documentation

| Doc | Topic |
|-----|--------|
| `Docs/WATER_MAGIC_SETUP.md` | Water magic components |
| `Docs/PROJECT_STRUCTURE.md` | This file |

Pipeline-side index: `3d-monster-gen/docs/README.md`.
