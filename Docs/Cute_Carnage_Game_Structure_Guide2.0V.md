# Cute Carnage - Game Structure Guide

> **Project:** Cute Carnage
> **Engine:** Unity 6.3 LTS / URP / PC
> **Document status:** Current technical architecture baseline — finalized, confirmed against repository code
> **Last updated:** 2026-09-05 (single-scene architecture confirmed via code audit; supersedes 2026-08-12 dual-scene description)

## 1. Architecture Principles

1. **Scene is presentation; runtime data is state.** Day and night are runtime phase toggles within one persistent scene, not separate loaded scenes. Scene-local objects are not rebuilt on phase transition — they stay alive and simply enable/disable.
2. **Persistent slot IDs own structure identity.** Fences and towers are identified by stable `PersistentId` values, not by scene object references. This still matters even in a single scene, since it is what lets runtime state address a specific fence/tower slot.
3. **Data defines types; components execute behavior.** Tower, fence, resource, enemy, and wave configuration is data-driven where possible.
4. **Day and night are separate gameplay contexts within one run and one scene.** Resources, structures, and relevant player state persist naturally because the objects are never destroyed or reloaded.
5. **UI does not need to rebind after a scene load, because there is no scene load between Day and Night.** HUD, prompts, gather bars, and panels stay wired for the life of the run.

## 2. Runtime Flow

```text
Bootstrap / game entry
  -> GameManager initializes run state (single persistent scene)
  -> Day phase (nightRoot inactive / dayRoot active)
       gather resources, install / repair / upgrade
       DayTimeManager.OnDayEnded -> GameManager.HandleDayEnded() -> TransitionToNight()
  -> Night phase (dayRoot inactive / nightRoot active)
       spawn configured waves
       final wave complete + all zombies dead
       SimpleZombieSpawner.CompleteNight() -> GameManager.Instance.TransitionToDay()
  -> next Day phase
```

### Night completion rule

`GameManager` must not end the night through a generic survival countdown. The authoritative night-completion condition is:

```text
Final configured wave finished spawning
AND
Zombie.AliveZombieCount == 0
```

This prevents premature day transitions while a later wave is still spawning or active. `SimpleZombieSpawner` is the sole authority that calls `TransitionToDay()` — no other system should end the night.

## 3. Scene Responsibilities

| Scene / context | Responsibility | Key systems |
| --- | --- | --- |
| Bootstrap / entry | Initialize global systems and start or load a run | `GameManager`, runtime state |
| Day phase (in-scene) | Exploration, gathering, hunger management, base preparation | `ResourceNode`, `PlayerInteractor`, `DayTimeManager`, structure panels |
| Night phase (in-scene) | Wave defense, zombie combat, emergency repairs | `SimpleZombieSpawner`, zombie AI, tower defense, night repair UI |

### Current scene model — confirmed single persistent scene

- **Verified in code:** there is a single persistent gameplay scene. `GameManager` contains no `SceneManager.LoadScene` call, and `DontDestroyOnLoad` has been removed entirely from the codebase.
- Day and Night are runtime phase toggles (`dayRoot` / `nightRoot` active-state switching), not separate loaded scenes.
- There is no capture-before-transition / apply-after-load pipeline. Fence, tower, and Base Core objects persist automatically because they are never destroyed — damage state carries over by default rather than through an explicit save/restore step.
- `BaseManager.cs` is currently an empty stub; it does not coordinate capture/apply because there is nothing to capture or apply across a scene load that no longer happens.
- **Known drift, corrected here:** earlier project documentation (this guide and the GDD, both dated 2026-08-12) described the single-scene model as a future migration target. That migration is complete. This document is the corrected baseline.

## 4. Persistent Run State

### Purpose

`RunRuntimeState` (and `BaseRuntimeState`) carries the run-level data that must survive across Day/Night phase changes and, eventually, across a future save/reload feature. Because objects are no longer destroyed on transition, this state is now minimal by design rather than a full snapshot.

### Current shape (confirmed)

- `BaseRuntimeState` currently holds only `baseLevel` and `baseUpgradeId`. It does **not** hold Base Core HP or a destroyed flag — Base Core HP persistence currently relies entirely on the `BaseCore` object never being destroyed, not on this state object.
- Fence and tower HP/destroyed state is likewise carried by the live scene objects (`FenceSegment`, tower components) persisting through the phase toggle, not by being written into and read back from `RunRuntimeState`.
- There is no disk save/load. All persistence described here is in-memory, for the duration of one play session only. **Verified in code: no save-to-disk system exists.**

### Persistent identity

- `TowerSlot.PersistentSlotId` and `FenceSlot.PersistentSlotId` resolve from a same-object `PersistentId` component.
- An editor-only `PersistentSlotIdValidator` verifies slot ID validity and helps prevent duplicate/missing identities.
- Persistent IDs remain useful for addressing a specific slot even without cross-scene transfer — they are the stable handle other systems use to refer to "this fence" or "this tower slot."

### Open risk: `Day.unity` decorative fences

Twenty-one decorative, non-functional fence objects exist in `Day.unity` outside the persistent-slot system. **Verified in code: functionally harmless** (they do not participate in gameplay logic), but they are visual clutter in the scene hierarchy. Intentionally left alone; low-priority cleanup only.

## 5. Base System

```text
GameManager (owns phase transitions)
  -> BaseCore (persists via not being destroyed; BaseManager is currently an empty stub)
  -> FenceSlot / FenceSegment
  -> TowerSlot / Tower
```

### `BaseManager`

- **Verified in code: empty stub.** It does not currently coordinate capture/apply or rebuild anything, because the single-scene model removed the need for cross-scene state transfer.
- If a future disk-save/checkpoint system is built (see §10 loss-condition gap and the retry-checkpoint backlog item), this is the most likely place for that logic to live — but it does not exist yet.

### Fences

| Component | Responsibility |
| --- | --- |
| `FenceData` | ID, display name, tier, prefab, max HP, install/repair/upgrade costs, repair amount, next fence, max-tier state |
| `FenceSlot` | Permanent interaction owner and persistent identity anchor |
| `FenceSegment` | Runtime HP, destruction state, visual toggling, barrier-collider behavior, repair interface |

- Destroyed fences disable their barrier colliders and show destroyed visuals.
- Fence health bars display when damaged or destroyed.
- **Verified in code:** destroyed fences can still be repaired through their slot — this works correctly.
- **Known gap:** the fence UI does not distinguish "Damaged" from "Destroyed" — both states are only tracked as `needsRepair = true`. This is scoped work for the integrated Fence Management Panel (see roadmap).
- **Known gap:** the fence Upgrade flow is not implemented — `upgradeButton.interactable` is currently hardcoded `false`.

### Towers

| Component | Responsibility |
| --- | --- |
| `TowerSlot` | Permanent tower location, interaction owner, persistent identity |
| `Tower` / tower behavior | Target search, aiming, projectile attack, damage, destroyed-state shutdown |
| Tower data | Type-specific configuration and upgrade/evolution identity |

- Current Arrow Tower behavior uses yaw targeting and projectile attacks.
- Targeting uses a non-alloc overlap query where applicable.
- Destroyed towers stop their functional logic.

### Open risk: `TowerSlot.EnsureCurrentTowerReference()`

- This method auto-adopts the nearest `ArrowTower` within a search radius (default 2m) and runs only from `Awake()`. It was written for the old dual-scene model, to recover a tower reference after a scene reload broke the object link.
- Under the confirmed single-scene model, object references are never broken by a scene reload, so this recovery path is **plausibly dead code** — but this is not yet confirmed either way.
- **Not yet resolved:** whether any `TowerSlot` in the scene has a pre-placed-but-unwired tower that still depends on this logic. This must be checked before removing or modifying it. Treat as an open investigation, not a confirmed no-op.
- `currentTower` is not `[SerializeField]` (non-serialized); `hasTower` is serialized. This asymmetry is part of why the proximity-adoption fallback exists, and is relevant to any fix.

## 6. Player, Interaction, and UI

### Interaction

`PlayerInteractor` manages nearby interaction behavior and must cancel gathering during the Day -> Night phase transition.

| Interaction | Current rule |
| --- | --- |
| Resource gathering | Day-only; starts through nearby **E** interaction |
| Fence / tower actions | Owned by permanent slots rather than temporary world-only interaction objects |
| Repair | Structure-specific action; consumes configured resource cost |
| Panel close | Escape/close behavior should reliably release the current interaction state |

### UI composition

```text
UIRoot
  -> HUDCanvas
       -> HUDRoot
  -> InteractionCanvas
       -> InteractionPromptUI
       -> WorldGatherBar
       -> StructureActionPanelUI (day)
       -> NightRepairPanelUI (night)
```

- UI uses Screen Space Overlay architecture.
- `InteractionPromptUI` follows its world anchor.
- **Verified in code:** the old dual-scene "scene-load rebind + retry path" is gone — there is no scene load between Day and Night, so there is nothing to rebind. What remains is `GameManager` explicitly closing the opposite phase's panel on transition (day panel closes when night starts, and vice versa) rather than a unified single panel.
- `StructureActionPanelUI` serves daytime install/upgrade/repair actions.
- `NightRepairPanelUI` serves damaged/destroyed structure repair actions during defense.
- **Backlog note (also tracked in `CLAUDE.md`):** `HUDController` currently uses polling rather than an event-driven update path; a refactor is planned but should be done as its own isolated session, not mixed with other changes.

## 7. Resources and Hunger

| System | Architecture responsibility |
| --- | --- |
| `ResourceNode` | Day-gated resource target and gathering behavior |
| Inventory / runtime resource values | Tracks Wood, Scrap, Food across phase transitions |
| Hunger system | Depletes hunger and applies HP loss at zero; state persists naturally since the player object is never destroyed |
| HUD controller | Reflects health, hunger, resources, and phase-specific base/wave data |

## 8. Enemy and Combat Systems

### Zombie

- Finds an eligible nearest/priority collider target.
- Uses navigation to reach its target.
- Delivers damage from an animation event, avoiding frame-by-frame damage during the entire attack animation.
- Maintains `AliveZombieCount` for wave resolution.

### Structures as combat targets

- Fence and tower damage state feeds health-bar visibility and destroyed visuals.
- Destroyed-state behavior must prevent a structure from continuing to block, attack, or operate incorrectly.

### Base Core loss condition — confirmed gap

- **Verified in code:** `BaseCore.TakeDamage()` currently only logs via `Debug.Log` when HP would reach zero. There is no actual game-over handling and no event raised.
- This means the game currently has no real "you lost" path from Base Core destruction, despite the GDD listing it as a current loss condition (see GDD §9). Treat the GDD's loss-condition description as the *design intent*, not as an implemented system.
- This gap directly blocks the planned Day-based retry checkpoint (Night N fail -> return to Day N): a checkpoint system needs a real loss event to trigger from. Player-HP-zero handling location is also unconfirmed and should be traced together with this.

## 9. Night Waves - `SimpleZombieSpawner`

### Authoritative flow

```text
Initial wave delay
  -> Spawn a configured wave
  -> Wait until spawning is complete AND AliveZombieCount is zero
  -> Inter-wave delay
  -> Next wave
  -> After final wave resolves, request Day transition once
```

### Inspector configuration

| Setting | Purpose |
| --- | --- |
| Initial Wave Start Delay | Delay before night combat begins |
| Inter Wave Delay | Pause between fully resolved waves |
| Night Waves array | Per-wave zombie count and spawn interval |
| Spawn Mode | `FullCircle` or `DirectionalArc` |
| Arc Angle | Directional spawning width; spawner forward axis defines center direction |

### Safeguards

- Day transition is invoked once, only from final-wave completion, and only by `SimpleZombieSpawner`.
- No generic night timer should compete with the spawner for phase transition authority. **Do not implement a night timer that ends the night early** — night must end only after the final wave has spawned and every spawned zombie is dead.
- Gizmos visualize directional arc and center direction for level testing and capture setup.

## 10. Current Implementation Status

### Implemented and verified

- **Single persistent scene** with Day/Night as runtime phase toggles (`nightRoot.SetActive()` style). No scene reload, no `DontDestroyOnLoad`.
- Fence install, repair, damage, destruction, and visuals/collider handling — including repairing a fence after it is destroyed.
- Tower slots, repair/damage flow, and core defense behavior.
- Persistent slot IDs plus editor validation.
- Multi-wave spawning with per-wave counts, intervals, and directional arcs.
- Final-wave-only day transition after all spawned zombies are dead, sole-authority held by `SimpleZombieSpawner`.
- Zombie animation-event damage.
- Day/Night panel split (`StructureActionPanelUI` / `NightRepairPanelUI`) with explicit opposite-panel closing on transition — not a unified single panel, but the old scene-load rebind risk is gone.
- No fence/tower slot duplication in the real slot system (decorative-only fences in `Day.unity` are the one exception, and are intentionally left alone).

### Confirmed gaps / open risks

- **Base Core loss condition is not actually implemented** — `Debug.Log` only, no game-over handling, no event. See §8.
- **No disk save/continue system exists.**
- Fence UI does not distinguish Damaged vs Destroyed (`needsRepair` only); Fence Upgrade flow is hardcoded disabled.
- `TowerSlot.EnsureCurrentTowerReference()` proximity-adoption logic is plausibly dead code under the single-scene model, but not yet confirmed either way — needs a pre-placed-tower check before touching it.
- HUD Hunger display needs verification (possible display bug, not yet root-caused).
- Full 7-day/7-night loop has not yet been final-verified end to end.

### In progress / integration validation needed

- Complete graybox map and resource-zone placement.
- Replace placeholder/simple zombies with final models, animations, and spawner variants.

### Planned, not implemented architecture

- Day-based retry checkpoints (blocked on the Base Core loss-condition gap above — needs explicit design, not just a scene-reload assumption, since objects no longer reset via reload).
- Resource respawn cycle, day-danger enemies, and world events.
- Craft table and Ability Hammer economy.
- Card Key tower-ability slot unlocks, rerolls, and complete tower-ability data/UI.
- Full enemy variant roster and elite/boss event behavior.

## 11. Engineering Rules

- Do not assume scene reload will reset or rebuild anything — there is no scene reload between Day and Night. Any "reset on transition" behavior must be written explicitly.
- Do not let both a timer and a spawner independently end the night.
- Treat `PersistentId` changes as data-migration-sensitive changes.
- Add new tower/fence types through ScriptableObject data before adding special-case logic.
- Validate Day -> Night -> Day persistence whenever a structure, slot, or runtime-data schema changes (Base Core damage carrying across Night -> Day -> Night is a required regression check — see `CLAUDE.md`).
- Keep visual/UI redesign decisions separate from gameplay-code status: approved concepts are implementation targets, not evidence of code completion.
- Before designing the Day-based retry checkpoint, first implement a real Base Core loss event and trace player-HP-zero handling — do not build a checkpoint system on top of the current placeholder `Debug.Log`.
