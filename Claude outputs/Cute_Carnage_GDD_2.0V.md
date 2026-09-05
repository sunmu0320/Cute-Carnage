# Cute Carnage - Game Design Document

> **Project:** Cute Carnage
> **Studio:** Kyven Games
> **Engine:** Unity 6.3 LTS / URP / PC
> **Document status:** Current design baseline — finalized
> **Last updated:** 2026-09-05 (scene-model and loss-condition notes corrected against confirmed code state; supersedes 2026-08-12)

## 1. Game Identity

### High concept

**Cute Carnage** is a 3D top-down survival and base-defense game. During the day, the player explores a ruined town to gather Wood, Scrap, and Food, then uses the limited preparation time to maintain the base. At night, zombie waves assault the defenses. The player must continually choose whether to fight, repair, or reposition before the Base Core falls.

**Tagline:** *Build by day. Survive by night.*

### Player fantasy

- Turn a fragile safe zone into a survivable home base.
- Make meaningful decisions under time pressure: gather more, return early, repair, or invest in stronger defenses.
- Survive increasingly dangerous zombie attacks through preparation and fast moment-to-moment choices.

### Genre and pillars

| Area | Direction |
| --- | --- |
| Genre | 3D top-down survival, resource scavenging, base defense, wave defense |
| Tone | Cute, readable low-poly world contrasted with tense and violent night defense |
| Pillar 1 | **Day choices matter at night.** Resources and preparation determine defense readiness. |
| Pillar 2 | **Fight or repair.** The player cannot solve every night problem with damage alone. |
| Pillar 3 | **Readability under pressure.** Enemies, threats, structure state, and player feedback must be immediately legible. |
| Pillar 4 | **Defenses grow into builds.** Towers develop through branches and randomized ability choices, not only linear stat upgrades. |

## 2. Prototype Scope and Long-Term Goal

### Current prototype target

The current playable milestone is a stable **7-day / 7-night** loop. It validates the complete core loop: gather, prepare, defend waves, persist damage and construction state, and progress to the following day.

### Long-term progression target

The original 30-night concept remains a long-term expansion target, not the current prototype completion requirement.

## 3. Core Gameplay Loop

```text
DAY
Explore resource zones -> Gather Wood / Scrap / Food -> Return to base
       -> Install / repair / upgrade defenses -> Prepare

NIGHT
Zombie waves attack -> Fight or repair -> Keep Base Core alive
       -> Final wave cleared -> Transition to next day
```

### Day phase

The day is the player's planning window.

- Explore the world and gather resources.
- Manage hunger while deciding how far to travel.
- Return to the base before the next night.
- Install, repair, or upgrade fences and towers.

### Night phase

The night is the defense and recovery test.

- Zombies attack the base in configured waves.
- Defense priority is **Fence -> Tower -> Base Core**.
- The player may damage zombies or repair damaged structures; repairing sacrifices offensive pressure.
- A night ends only when the final configured wave has finished spawning **and** all zombies spawned for that night are dead. Do not design or implement a timer that ends the night early — this rule is load-bearing for both design and code.

## 4. World and Resource Map

| Area | Primary identity | Primary resource | Intended risk |
| --- | --- | --- | --- |
| South | Starting village / ruined residential area | Small amounts of all resources | Low-risk starting access |
| North | Forest | Wood | Longer gathering routes, later daytime danger |
| West | Market / mart | Food | Daytime zombie pressure and food-focused supply runs |
| East | Factory | Scrap | Daytime zombie pressure and high-value crafting material |

The world is a ruined village being reclaimed by nature: damaged buildings, abandoned roads, overgrowth, and improvised defensive structures around the home base.

## 5. Player Systems

### Movement and interaction

- Top-down player movement with follow camera.
- Contextual nearby interaction prompt using **E**.
- Resource gathering is day-only.
- Structure actions are accessed through their permanent interaction owners/slots.

### Health and hunger

| System | Current design |
| --- | --- |
| Health | Player dies when HP reaches zero. |
| Hunger | Continuously drains during play. |
| Starvation | When hunger reaches zero, HP drains over time. |
| Food | Restores hunger and supports longer scavenging runs. |

### Combat and repair

- Player combat targets nearby threats through the current automatic combat model.
- Repairing is a deliberate commitment: it reduces or interrupts offensive contribution while the player restores a structure.
- Zombie damage is intended to occur at animation-event timing, not continuously during the whole attack animation.

## 6. Resources and Economy

| Resource | Primary source | Main use |
| --- | --- | --- |
| Wood | Trees / forest | Fence work, repair, and construction costs |
| Scrap | Scrap piles / factory | Tower progression, crafting, future rerolls |
| Food | Abandoned food / mart | Hunger recovery |

### Economy direction

- Resource availability should create meaningful route and time tradeoffs, not hard-lock a player who has little time remaining.
- Resource respawn is planned around a multi-night cadence (roughly every 3-4 nights) rather than unlimited immediate replenishment.
- A future craft table converts **10 Scrap -> 1 Ability Hammer**.

## 7. Base Defense

### Base Core

- The central objective to protect.
- Base Core destruction is intended, by design, to cause a loss.
- Its HP is displayed prominently during night defense.
- **Implementation note:** as of the latest code audit, Base Core damage-to-zero does not yet trigger an actual game-over — this is a design-confirmed, not-yet-implemented system. See `Cute_Carnage_Game_Structure_Guide2.0V.md` §8 for the technical gap and what it blocks (the retry-checkpoint system in §9 below).

### Fences

- Fences form the outer defensive ring and absorb early pressure.
- Fence state includes health, repairability, and destroyed visuals/collider behavior.
- Fence progression is data-driven through `FenceData` and supports install, repair, upgrade, and max-tier rules.

Early balancing baseline:

| Fence tier | Max HP | Repair amount |
| --- | ---: | ---: |
| T1 | 100 | +30 |
| T2 | 160 | +30 |
| T3 | 210 | +30 |
| T4 | 260 | +30 |

### Towers

- Towers are placed through persistent tower slots and automatically attack enemies.
- Towers can be damaged, repaired, and destroyed.
- Current defense identity includes the Arrow Tower / Ballista direction; future branches include Flamethrower, Shotgun, Catapult, Sniper, Laser, and Machine Gun towers.

### Tower ability progression - planned

Tower growth will use branch identity plus random ability rolls.

| Item / system | Purpose |
| --- | --- |
| Ability Hammer | Randomize a tower's currently available abilities; first use may be free, later rerolls cost resources. |
| Card Key | Unlock an additional tower ability slot gradually through progression. |
| Ability pool | Includes damage, attack speed, projectile count, and gameplay-changing effects, not only flat stat increases. |

The Ballista ability-management open state, evolution presentation, and reroll direction are visually approved. Exact ability cards and final balance are still pending.

## 8. Enemies and Waves

### Current enemy behavior

- Zombies locate valid targets and navigate to attack.
- Attack damage is driven by animation events.
- Destroyed structures stop functioning and update their collision/visual state.

### Wave progression

- Night configuration supports multiple waves.
- Each wave specifies zombie count and spawn interval.
- Spawn modes include full-circle and directional-arc spawning; directional arcs are useful for controlled gameplay composition and testing.
- The final wave resolves only after all its spawns are complete and `AliveZombieCount` reaches zero. `SimpleZombieSpawner` is the sole system allowed to end the night.

### Planned enemy roster

| Type | Intended role |
| --- | --- |
| Basic | Standard zombie baseline |
| Runner | Fast pressure and player-position checks |
| Breaker | Fence-focused threat |
| Tank | High-health pressure unit |
| Elite / Boss | Major wave events communicated by night-wave UI markers |

## 9. Failure, Retry, and Progression

### Current loss conditions (design intent)

- Base Core is destroyed.
- Player HP reaches zero, including starvation damage.

**Implementation status:** these are the intended loss conditions by design, but they are not fully wired up in code yet — Base Core destruction currently only logs and does not end the game. Treat this section as design intent that implementation must still catch up to, not as a description of current player-facing behavior.

### Planned retry rule

If the player dies on a later night, retry should return to that day's daytime state (for example, Night 5 failure returns to Day 5), rather than resetting the whole run to Day 1.

**Design dependency:** this rule was originally written assuming a scene-reload-based reset would naturally clear night damage. Under the confirmed single-persistent-scene architecture, objects are never destroyed or reloaded, so this checkpoint needs an explicit save/restore design rather than relying on scene reload. This is now a blocking open design question, not just an implementation task — see `Cute_Carnage_Game_Structure_Guide2.0V.md` §8 and §10.

## 10. Visual and UI Direction

### Art direction

- Low-poly foundation.
- Small, cute character proportions.
- Simple, clear silhouettes.
- Day: calm but uneasy, nature-reclaimed village.
- Night: dark, high-contrast defense scenes with controlled fog and strong threat readability.

### Approved HUD baseline - Gameplay HUD V2.0

| HUD area | Approved purpose |
| --- | --- |
| Upper center - day | Sun/day timer presentation |
| Upper center - night | Moon-based wave timer with vertical event markers for elite/boss/major wave events |
| Left side | Health and hunger display |
| Lower left | Wood, Scrap, and Food counters |
| Lower center at night | Base Core HP |

Interaction feedback should be lightweight and clear. Gathering uses a simple semi-transparent dark bar with a green decreasing progress fill, rather than a large panel.

### Approved visual-concept baseline

- Night Defense Main Concept
- Home Base Defense Main Concept: inner fence ring, towers positioned inside near the fence, base lantern, restrained fence scale
- Structure Install UI
- Tower T1 -> T2 upgrade and Ballista evolution presentation

These approved visual/UI decisions are preserved as-is and should not be revised without an explicit request.

## 11. Future World Events and Escalation

Planned events make daytime supply runs more dynamic:

- Military helicopter / aircraft event.
- Burning military cargo-truck crash surrounded by zombies.
- Dangerous daytime zones containing zombies rather than completely safe resource fields.
- Escalating resource risk and enemy pressure as days progress.

## 12. Design Decisions and Open Items

| Topic | Current decision / open work |
| --- | --- |
| Scene model | **Confirmed implemented:** a single persistent scene is in use, with Day/Night as runtime phase toggles rather than separate loaded scenes. This is no longer a planned migration — it is the current architecture. See `Cute_Carnage_Game_Structure_Guide2.0V.md` §3 for the technical confirmation. |
| Resource interaction | Day-only gathering is implemented; movement-cancel behavior remains a desired refinement. |
| Tower abilities | System direction approved; final ability cards, UI content, and balance are not finalized. |
| Visual production | Wood gathering scene is the representative visual baseline; future gathering scenes follow its approved camera and HUD rules. |
| Enemy variants | Basic zombie loop is active; full differentiated roster remains future work. |
| Loss condition implementation | Design intent (§9) is confirmed and unchanged; the actual code path to end the game on Base Core destruction does not exist yet and is now a tracked gap — see roadmap. |

## 13. Companion Documents

- `Cute_Carnage_Game_Structure_Guide2.0V.md` — technical architecture baseline; authoritative for how systems are actually implemented.
- `CLAUDE.md` (repository root) — operational baseline for Claude Code / Cowork sessions: working protocol, regression checks, completion-report format.
- `PROJECT_HANDOFF.md` and `Single_Scene_Migration_Proposal.md` — retained in the repository as historical reference only (the original ChatGPT-era handoff notes, and the now-completed single-scene migration plan). They are not priority documents; where their content was still relevant, it has been folded into this GDD and into the Structure Guide.
