# Milestone: water orb (voice + finger) · Meshy character · AI monsters · dragon · portal VFX

## 1. Water orb on the beach (implemented scripts)

### Components

| Script | Role |
|--------|------|
| `MagicTargetingRay` | Ray from fingertip / controller |
| `WaterGatherController` + `WaterOrbVisual` | Orb in hand; `ForceFullOrb()` after ritual |
| `WaterOrbFormationRitual` | Voice/event → ray must hit **Water** → preview sphere animates surface → hand → fills orb |
| `WindowsSpeechKeywordDriver` | **Windows / Editor:** listens for **"water orb"** or **"water ball"** |
| `MagicIntentChannel` | Optional: intent id `magic.water.orb` → same UnityEvent as voice |

### Wiring (XR rig)

1. **Fingertip** empty child: `MagicTargetingRay` (`rayOrigin` = that transform, `maxDistance` ~120, **Hit Layers** include **Water**).
2. **Hand** attach: assign to `WaterOrbVisual.attachPoint` and `WaterOrbFormationRitual.handAttachPoint`.
3. **Same rig:** `WaterGatherController` (probe can stay for close gather; ritual bypasses hold).
4. **`WaterOrbFormationRitual`:** assign ray, **water** mask, hand, `WaterGatherController`. Optional URP Lit transparent material on formation sphere; optional particle burst prefab.
5. **`WindowsSpeechKeywordDriver`:** `On Water Orb Heard` → `WaterOrbFormationRitual.BeginFormation`.
6. **Quest:** disable `WindowsSpeechKeywordDriver` (no Windows speech). Use **XR button** → `SimulateWaterOrbPhrase()` **or** Convai / your API → same event chain.

### Mic / permissions (Windows)

Allow microphone for the Editor / standalone build; if keywords never fire, check **Privacy → Microphone**.

---

## 2. Player character from Meshy

1. Generate character in Meshy → download **GLB**.
2. Import to `Assets/ImportedContent/Player/` (see `3d-monster-gen/docs/CONTENT_VAULT_AND_UNITY_IMPORT.md`).
3. In Unity: **Rig → Humanoid** if you need Mecanim; otherwise use as generic with your XR avatar (often you keep **XR hands** and only swap **body** mesh under tracking root).
4. **Scale** to ~1.8 m, **pivot at feet**, parent under **XR Origin** follow target.

---

## 3. Island monsters from `ai-3d-agent-starter-main`

1. Run starter → GLB in vault `approved/creature.<id>.v1/`.
2. Import FBX/GLB to `Assets/ImportedContent/Creatures/`.
3. Add **collider**, **NavMeshAgent** (ground) or **FlyingPatrolSimple** (air).
4. Register **`creature.<id>.v1`** on `SpawnDirector` catalog (see `INTENT_VERTICAL_SLICE.md`).

---

## 4. Dragon flying

1. Import dragon mesh (Meshy or starter).
2. Empty at island center: assign as **`FlyingPatrolSimple.center`** (or player transform).
3. Add **`FlyingPatrolSimple`** to dragon root; tune **radius / height / orbitSpeed**.
4. Later: wing **Animator**, SFX, fire breath as separate prefab.

---

## 5. Portal — replace with real VFX

1. Keep trigger **Collider** + your **load scene** / **teleport** script.
2. Add **`PortalVfxRoot`** on visual root; assign a **Light** and parent your **Hovl / VFX Graph** prefab under it.
3. Replace primitive cylinder with **particle system** + **distortion**-friendly URP material when ready.

---

## Priority order (your ask)

1. **Water orb** — wire table in §1 until it works on beach.  
2. **One Meshy character** in scene (scale + parent).  
3. **One catalog creature** from ai-3d-agent on island.  
4. **Dragon** = same pipeline + `FlyingPatrolSimple`.  
5. **Portal** = VFX prefab + `PortalVfxRoot`.
