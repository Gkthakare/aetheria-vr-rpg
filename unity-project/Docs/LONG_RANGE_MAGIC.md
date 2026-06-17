# Long-range magic (no per-frame AI)

**Infinite / versatile** oceans and fields come from **math + streaming + global shader data**, not from running an LLM every frame. **AI** (your backend or future voice) should only **choose intent** — e.g. call `MagicIntentChannel.Dispatch("water.push")` — while these scripts do the work deterministically.

## Components (`Assets/Scripts/Aetheria/Magic/`)

| Type | Role |
|------|------|
| `MagicTargetingRay` | Ray from hand / finger / wand; set `maxDistance` for “how far magic reaches”. |
| `RemoteWaterInfluence` | On `CastAtAim()`: ray vs **Water** layers → ripple + `WaterWorldInfluenceHub` pulse. |
| `WaterWorldInfluenceHub` | Sets globals `_MagicWaterPoint` (xyz + w=radius), `_MagicWaterStrength` (decays). Add these to your **water Shader Graph** to displace or tint in a world-space sphere. |
| `MagicIntentChannel` | Maps string ids → **UnityEvents** (wire `RemoteWaterInfluence.CastAtAim`, spells, etc.). AI/voice calls `Dispatch(id)` only. |
| `PocketDimensionDescriptor` | Data: scene/address for additive load. |
| `DimensionGateway` | `EnterDimension()` / `ExitToSpawn()` — assign **player rig root** manually. |

## Water Shader Graph (URP)

Create **Blackboard** variables matching:

- `_MagicWaterPoint` (Vector4) — xyz world center, w = radius  
- `_MagicWaterStrength` (Float) — 0 when idle  

Use **Distance**(Position, `_MagicWaterPoint.xyz`) and `_MagicWaterStrength` to add vertex offset or normal perturbs (cheap “telekinesis” on the whole sea).

## Wiring

1. Child empty on **index fingertip** (or controller) → `MagicTargetingRay` (`rayOrigin` = that transform).
2. Same rig: `RemoteWaterInfluence` → assign targeting, **water** `LayerMask`, optional `WaterRippleSpawner`, `WaterWorldInfluenceHub`.
3. XR **Select** or UI → `RemoteWaterInfluence.CastAtAim` or `MagicIntentChannel.Dispatch("water.remote")`.

## Space storage / dimensions

- **Additive scenes**: put pocket scenes in **Build Settings**; `DimensionGateway.EnterDimension()` loads by name.
- **Addressables**: later replace `sceneOrAddress` load with `Addressables.LoadSceneAsync`.
- **No AI required** for the load itself — only for *when* to trigger if you want voice-driven travel.
