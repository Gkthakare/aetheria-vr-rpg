# Consolidation - Optimization Notes

Records the optimizations applied during the islandProto + 3d-monster-gen
consolidation, and the remaining tuning that must be done in the Unity editor
(and ideally profiled on a Quest 3).

## Applied in code/config (in this repo)

- **One backend process** instead of two. `ml-training` (port 8000) was folded
  into the unified `backend/` FastAPI app (port 8001); all Unity clients now point
  at 8001. Fewer processes, one venv, one requirements file.
- **No chunk-water puddles by default.** `ChunkManager.suppressWaterTilesForSuimono`
  now defaults to `true`, so the world uses one continuous Suimono ocean instead of
  per-chunk water quads (a long-standing visual complaint). `SuimonoInfinityWaterBootstrap`
  still enforces this at runtime.
- **Repo hygiene.** Root `.gitignore` keeps multi-GB venvs, model caches, frame
  dumps, build APKs, and `node_modules` out of git. The 2.4 GB redundant
  `islandPromo` mesh set was deliberately not imported.
- Quality tiers in `ProjectSettings/QualitySettings.asset` are already
  Quest-appropriate (shadow distance 15-70, low pixel-light counts).

## Do in the Unity editor (requires the editor / on-device profiling)

1. **Single render pipeline (URP).** Confirm Graphics + Quality settings both use
   the URP asset (no Built-in fallback). Run `Aetheria > ... > URP Graphics Setup`
   and the `ReadyPlayerMeUrpFixer` / `FantasySkyboxUrpFixer` wizards so Suimono,
   Hovl VFX, RPM avatars, and the skybox all render under URP.
2. **Portal / magic-circle Y-sort.** Ensure portal discs and magic-circle VFX spawn
   slightly ABOVE the Suimono sea level (use `IslandWorldBuilder.SeaLevelWorldY`),
   so they are not hidden under the water surface.
3. **Shadow distance (URP asset).** In the active `UniversalRenderPipelineAsset`,
   keep shadow distance modest (40-70 m) for Quest; this is separate from
   QualitySettings.
4. **Batching / draw calls.** Enable SRP Batcher, static-batch the island hub
   geometry, and add LODs / mesh-combine for trees and rocks to hold 72-90 FPS.
5. **Avatar/hands wiring.** Follow `ISLANDPROTO_MERGE.md` to add the body-hub boot
   scene and wire the XR Hands bridge + humanoid upgrader onto the HelloVR rig.
