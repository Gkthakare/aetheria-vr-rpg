# Aetheria VR RPG — SAO-Inspired Design Overview

## Goals
- Realistic, perilous dungeons themed after Sword Art Online (SAO) sensibilities: readable patterns, lethal mistakes, mastery rewarded.
- Deterministic generation from a seed to ensure fairness, reproducibility, and multiplayer synchronization.
- Ability system modeled on SAO-style weapon arts, timed windows, and combo chains; grounded (no magic spam), reads from stats.

## Dungeon Philosophy (SAO-flavored)
- Floors and Labyrinths: Each dungeon is a “floor” with a labyrinth leading to a boss arena. Floors introduce 1–2 new mechanics.
- Telegraphs > RNG: Enemies and traps clearly telegraph; players must read, position, and punish.
- Resource Tension: Limited safe rooms; progression encourages calculated risk.

## Procedural Dungeon (Prototype Scope)
- Generator: `DungeonGenerator` creates a graph of rooms (Start → Hubs → Keys → Boss). Layout is deterministic per `(seed, floorIndex)`.
- Room Types: Start, Corridor, Hub, KeyRoom, TrapRoom, MiniBoss, Boss. Each type constrains content budget.
- Keys & Locks: Some hubs gate via colored keys; guarantees solvable routes.
- Spawn Budget: Allocated via difficulty curve; enemies/traps picked by theme with clamped counts.
- Determinism: Uses our `DeterministicRng` wrapper; no `Random.Range`.

## Enemy Design (Prototype)
- Roles: Scout (fast, low HP), Bruiser (slow, high HP), Sentinel (shield, blocks frontal), Rogue (backstab window), Boss (pattern phases).
- Telegraph Windows: 0.6–1.2s wind-ups; parryable arcs; backstab cones; dodge i-frames (short).
- Scaling: Enemy stats scale with floor and player rank but obey caps.

## Ability System (Weapon Arts)
- Inputs: Light, Heavy, Finisher. Chained within timing windows to form “skills”.
- Stamina: Costs scale with weapon weight and skill tier.
- Cooldowns: Per-skill internal CD and global “focus” meter (anti-spam).
- Effects: Damage, stagger, guard-break, bleed; all server-verifiable via signed parameters.

## Networking & Authority (Later)
- Server holds dungeon graph, room clears, key state; clients stream rooms as they approach.
- Combat results are verified via hit timestamps and positional cones.

## Safety & Moderation
- Voice/LLM only maps to numeric parameters (theme, aggression, density). No free-form content injection.

## Files (this increment)
- `Assets/Scripts/Dungeons/DungeonGenerator.cs` — deterministic room graph + simple room spawn stubs.
- `Assets/Scripts/Combat/AbilitySystem.cs` — minimal stat + cooldown scaffold.
- `Assets/Tests/DungeonTests.cs` — verifies deterministic generation.

## Manual Test Plan
1. Add `DungeonGenerator` to an empty GameObject; set seed/floor; press Play; observe logged graph.
2. Change seed → different graph; restore seed → same graph.
3. Attach `AbilitySystem` to a dummy player; press keys to see cooldown and stamina logs.
