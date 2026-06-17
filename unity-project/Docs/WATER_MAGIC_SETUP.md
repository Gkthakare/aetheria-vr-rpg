# Water magic (Aetheria.WaterMagic) — setup

Scripts live in `Assets/Scripts/Aetheria/WaterMagic/`. Target editor: **Unity 2022.3 LTS** (your Hub project).

**Project layout:** see `Docs/PROJECT_STRUCTURE.md`.

## 1. Fix compile errors (if any)

- **Input System:** `Aetheria.WaterMagic.asmdef` references `Unity.InputSystem`. Install package **Input System** (`com.unity.inputsystem`) via Package Manager, *or* remove the `Unity.InputSystem` line from the asmdef and delete `WaterGatherInputBridge.cs` / `WaterCastInputBridge.cs` if you only use XR events.

## 2. Project layers

- **Layers:** Unity’s built-in **Water** layer (default) is enough. Put the surface collider on **Water**.
- Any collider you want the hand probe to hit must match **WaterHandProbe → Water Layers**.

## 3. Water surface

- Create a large **Plane** (or use existing SUIMONO / your ocean). Add **Mesh Collider** (or Box) so **raycasts hit** the top surface.
- Set the object’s **Layer** to **Water**.

## 4. Ripple prefab (optional)

- **GameObject → Effects → Particle System** — short lifetime, burst, soft alpha; save as prefab `RippleBurst`.
- You will assign this on **WaterRippleSpawner → Ripple Prefab**.

## 5. Water bolt prefab

- Create empty `WaterBolt` with:
  - **Sphere** (scale ~0.12), **Rigidbody** (useGravity off in script), **Sphere Collider**
  - **WaterProjectile** component
- Save as prefab. (Optional child particle for trail.)

## 6. Spell asset

- **Assets → Create → Aetheria → Water Spell**
- Set **Spawn Prefab** to `WaterBolt`, **Charge Cost** ~0.35.

## 7. Scene wiring (desktop test)

Use your **XR Origin** or main camera rig.

1. **Hand probe:** Empty child under **Right Controller** (or camera for mouse test): add **WaterHandProbe** — set **Water Layers** to Water, **Ray Direction** (0, -1, 0), tune **Max Distance**.
2. Same object or child: **WaterRippleSpawner** — link probe + ripple prefab.
3. **Orb:** New sphere with URP Lit / transparent material (`_BaseColor`). Add **WaterOrbVisual** — **Attach Point** = right controller; **Tint Targets** = sphere **Mesh Renderer**.
4. Empty on player: **WaterGatherController** — Dominant Probe = probe, Orb Visual = orb component.
5. Same object: **WaterMagicCaster** — Gather = above, **Cast Origin** = camera or controller, **Primary Spell** = your Water Spell asset.
6. For **Editor / mouse:** add **WaterGatherInputBridge** + **WaterCastInputBridge** on the same player object and wire references.
   - **Hold right mouse** over water (probe must hit) to charge.
   - **Left click** to cast (needs charge ≥ spell cost).

## 8. VR (Quest)

- Disable or remove the two `*InputBridge` components.
- From **XR Interaction Toolkit**, on **activate** (grip/select): `WaterGatherController.SetGathering(true)`; on deactivate: `SetGathering(false)`.
- On **trigger pressed**: `WaterMagicCaster.TryCastPrimary()`.

## 9. SUIMONO note

If you use **SUIMONO - WATER SYSTEM 2**, ensure the surface your probe hits has a **collider** on the **Water** layer; SUIMONO meshes sometimes need a separate invisible collider plane if the stock setup does not raycast reliably.
