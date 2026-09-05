# Cute Carnage - Project Handoff for Claude

> **Owner:** Sean
> **Studio:** Kyven Games
> **Project:** Cute Carnage (formerly Survive!)
> **Engine:** Unity 6.3 LTS / URP / PC
> **Last consolidated:** 2026-09-05, America/Los_Angeles
> **Purpose:** Give Claude enough verified context to continue design, engineering, UI, and production work without resetting established decisions.

---

## 0. Instructions for Claude

Treat this document as the **entry point**, not as proof that every described class or scene is unchanged in the current repository.

Before editing code:

1. Inspect the actual Unity repository and current working tree.
2. Identify the owning scene, prefab, ScriptableObject, runtime-state record, and persistent-ID dependency.
3. Preserve unrelated user changes.
4. Report what you found before making broad architectural changes.
5. Distinguish these labels throughout your work:
   - **Verified:** previously play-tested or explicitly confirmed by Sean.
   - **Verified in code (static):** confirmed by direct file/code inspection (grep, reading the actual script/scene/prefab), but not play-tested. Treat as one notch below full Verified.
   - **Reported implemented:** implemented in a prior step but should be rechecked in the current repository.
   - **Approved design:** locked design direction; it may not be implemented yet.
   - **Planned:** future work, not current functionality.
6. Do not silently replace an approved design or architecture decision. Explain the conflict and ask Sean before changing it.

### Source-of-truth order

When sources conflict, use this priority:

1. Sean's newest explicit instruction.
2. Newer verified implementation or approved visual/UI decision.
3. This `PROJECT_HANDOFF.md`.
4. `Cute_Carnage_Game_Structure_Guide.md`.
5. `Cute_Carnage_GDD.md`.
6. Old PDFs, screenshots, prompts, and files using the former title **Survive!**.

The old `GDD.pdf` and `GameStructureGuide.pdf` are April 2026 snapshots. They are historical references and are not the current baseline.

**Known drift as of 2026-09-05:** `Cute_Carnage_GDD.md` and `Cute_Carnage_Game_Structure_Guide.md` (both last updated 2026-08-12) still describe a **separate Day/Night scene architecture with an explicit capture/apply runtime-state pipeline**. That architecture has since been replaced — see Section 4. Until those two documents are updated, prefer this file and `Single_Scene_Migration_Proposal.md` over them for anything related to scene structure, `BaseManager`, or `RunRuntimeState`.

---

## 1. Project Identity

**Cute Carnage** is a single-player, 3D top-down survival and base-defense game. During the day, the player scavenges resources, manages hunger, and prepares a home base. At night, zombie waves attack the defenses while the player decides whether to keep fighting or repair failing structures.

**Tagline:** *Build by day. Survive by night.*

### Experience target

- Cute, readable, toy-like low-poly forms.
- A calm but uneasy daytime scavenging phase.
- Darker, high-pressure nighttime combat.
- Stylized violence rather than realistic gore.
- Strong contrast between preparation and carnage.

### Core pillars

1. **Attack or repair:** the player cannot solve every night problem through damage alone.
2. **Day decisions matter at night:** route, time, hunger, and resource spending shape the next defense.
3. **Readable pressure:** threats, structure condition, objectives, and interaction feedback must be clear at a glance.
4. **Defense build expression:** tower branches, evolutions, and later randomized abilities create distinct builds.

### Scope boundary

| Scope | Current decision |
| --- | --- |
| Current prototype goal | Stable, understandable **7-day / 7-night** loop with a clear Night 7 ending. |
| Long-term goal | A complete **30-night** experience with more areas, defenses, enemies, progression, replayability, and a satisfying ending. |
| Current scene model | **One persistent gameplay scene.** Day and Night are runtime phase states toggled in place (see Section 4). Not separate loaded scenes. |
| Retry/checkpoint model | Not yet implemented. Needs a new design — see Section 10, Known Risk 8. |

---

## 2. Core Gameplay Loop

```text
DAY
Explore -> gather Wood / Scrap / Food -> manage Hunger
-> return to base -> install / repair / upgrade defenses

NIGHT
Zombie waves attack -> fight OR repair -> protect Base Core
-> final configured wave finishes -> all spawned zombies die
-> transition to next day
```

### Day phase

- Explore under a time limit.
- Gather Wood, Scrap, and Food through nearby `[E]` interaction.
- Manage Hunger; at zero Hunger, player HP drains.
- Install, repair, or upgrade fences and towers.
- Gathering is day-only.
- Desired refinement: moving cancels the gathering action. **Not yet implemented.**

### Night phase

- Zombies pressure the base and the player through configured waves.
- Base-defense priority is **Fence -> Tower -> Base Core**.
- Repairing intentionally reduces or interrupts the player's combat contribution.
- Night ends only after the final configured wave has finished spawning and every spawned zombie is dead.
- Do not restore a generic night-survival countdown that can force an early Day transition.

### Night exploration direction - approved design, not confirmed implementation

- Do not force the player to return to the home base when Night begins.
- If the player remains outside, zombies should emerge or become detectable around the player through the fog and attack.
- The home base must still be attacked even if the player is away.
- Night fog should feel denser and more dimensional and may partially obscure houses, while gameplay threats remain readable.

### Failure and retry

- Base Core destruction is a loss condition.
- Player HP reaching zero is a loss condition; starvation can cause this through HP drain.
- **Planned retry (needs re-design, see Known Risk 8):** a Night 5 failure should return to **Day 5 daytime**, not Day 1. The mechanism this originally assumed (scene reload naturally resetting damage) no longer applies now that the game runs in a single persistent scene with no capture/apply pipeline. A real rollback/checkpoint system has to be designed before this can be implemented.

---

## 3. World and Resource Layout

| Region | Primary resource / role | Design intent |
| --- | --- | --- |
| South - starting village | Small/basic amounts of all resources | Early survival access; not enough to sustain every long-term need. |
| North - forest | Wood | Main wood-gathering zone. |
| West - mart | Food | Main food-gathering zone. |
| East - factory | Scrap | Main scrap-gathering zone. |
| River / bridge north of village | Landmark / route boundary | Helps navigation and future area expansion. |

### Planned world escalation

- Forest, mart, and factory eventually contain daytime zombies instead of being fully safe.
- Resources may respawn on a multi-night cadence, roughly every 3-4 nights.
- Resource limits should create route decisions without hard-locking the player late in a day.
- Planned world events include a military aircraft/helicopter event and a burning military cargo-truck crash surrounded by zombies.
- Proposed crafting conversion: **10 Scrap -> 1 Ability Hammer**.

---

## 4. Current Technical Architecture

### Core rule

**One persistent gameplay scene. Day and Night are runtime phase states, not separate scenes.**

This replaces the earlier "separate Day/Night scenes with capture/apply" model. Confirmed by direct code inspection (2026-09-05):

- `GameManager` contains no `SceneManager.LoadScene` call for phase transitions.
- `DontDestroyOnLoad` calls tied to the old cross-scene handoff have been removed entirely.
- Phase transition is a `nightRoot.SetActive(true/false)`-style toggle, not a scene load.
- `BaseManager.cs` is currently an **empty stub** (`public class BaseManager : MonoBehaviour {}`). The old capture/apply pipeline that used to move Fence/Tower/BaseCore state between scenes has been deleted, not just changed.
- `BaseRuntimeState.cs` now only carries `baseLevel` and `baseUpgradeId` — it has **no HP or destroyed-state fields**.
- Because the scene and its objects are never destroyed between Day and Night, Fence/Tower/BaseCore damage persists naturally (the objects simply stay alive) rather than through an explicit save/restore step. This is a real architectural difference from what earlier documents describe, not just an implementation detail.

```text
Game entry / Bootstrap
  -> initialize run state
  -> single persistent scene
       Day phase active: gather + prepare + manage structures
       Night phase active (same scene, same objects): waves + combat + repair
       -> next Day phase (same scene continues)
```

**Do not** reintroduce a second scene load path for Day/Night transitions, and do not assume the old `RunRuntimeState` capture/apply pipeline still exists — it does not.

### Persistent structure identity

- `TowerSlot.PersistentSlotId` and `FenceSlot.PersistentSlotId` resolve from a `PersistentId` component on the same GameObject.
- An editor-only `PersistentSlotIdValidator` was implemented and confirmed working.
- Persistent IDs, slot hierarchy, and data IDs are migration-sensitive. Do not casually regenerate or rename them.
- Confirmed by code audit: real Fence/Tower slots are correctly consolidated under `HomeBase.prefab` (10 `FenceSlot`, 6 `TowerSlot`, 17 `PersistentId`, no duplicates). Standalone/decorative fence-shaped props exist in `Day` (21 objects under `Assets/Prefabs/Props/`, e.g. `FenceSTRONG`) but carry **zero** gameplay `MonoBehaviour`s — they are not zombie targets and are not tracked by the validator. This has been left alone deliberately; do not "clean it up" without Sean's go-ahead, since it is currently harmless.

### Known open risk: tower reference adoption

- `TowerSlot.EnsureCurrentTowerReference()` (around lines 506-554) still contains proximity-based adoption logic: if `hasTower == true` but `currentTower == null`, it searches all `ArrowTower` instances in the scene and adopts the nearest one within `towerReferenceSearchRadius` (default 2m).
- The single-scene migration reduces how often this path triggers (no more scene-reload reference loss), but the logic itself has not been removed and can still misfire (e.g. editor domain reload, manual Inspector edits setting `hasTower = true`).
- Treat this as an **open, unresolved risk**, not something the migration silently fixed. A fix should be proposed as a diff for Sean's review, not auto-merged, since it touches a core file.

---

## 5. Current Systems and Status

| System | Status | Notes |
| --- | --- | --- |
| Day/Night phase transition | **Verified in code (static)** | Single persistent scene, in-place activation toggle. No scene load. |
| Base Core persistence across phases | **Verified in code (static), not play-tested** | No explicit capture/apply; damage persists because the object is never destroyed. Needs a real Night→Day→Night regression test in the Editor. |
| Resource gathering | **Verified baseline** | Wood/Scrap/Food nodes; day-only gating; short progress interaction. |
| Hunger | **Implemented; UI verification needed** | Hunger drains and zero Hunger damages HP; HUD display remains a priority check. |
| Fence install/repair/destruction | **Verified baseline** | Destroyed visuals and barrier-collider disable behavior exist. Slots correctly consolidated (see Section 4). |
| Fence Destroyed vs Damaged UI distinction | **Not implemented** | Code currently treats both as a single `needsRepair = true` state; no separate visual/interaction state exists yet. |
| Fence/Tower Upgrade flow | **Not implemented** | `upgradeButton.interactable` is hardcoded `false` in the current panel code. |
| Tower slot/repair/persistence | **Verified baseline** | See "Known open risk: tower reference adoption" above — proximity-based adoption logic is still present and unresolved. |
| Persistent slot ID validation | **Verified** | Editor-only validator confirmed. |
| Zombie attack damage | **Verified** | Damage occurs through Animation Event timing, not continuous attack-state damage. |
| Wave arrays and intervals | **Verified** | Multi-wave configuration, per-wave count/interval, directional arc option. |
| Night completion | **Verified** | Final wave complete + `Zombie.AliveZombieCount == 0`; Day transition once. |
| Full 7-day completion | **Not yet final-verified** | Requires complete end-to-end polish and regression pass. |
| Retry / checkpoint (Night N fail -> Day N) | **Not implemented; needs new design** | Previously assumed scene-reload would provide a natural reset. That assumption no longer holds — see Known Risk 8. |
| Disk save / Continue | **Verified in code (static): absent** | Repository-wide search found no `PlayerPrefs`, `JsonUtility` save path, `File.WriteAllText/ReadAllText`, `BinaryFormatter`, or `SaveGame`/`LoadGame` code. |
| Day/night panel architecture | **Verified in code (static)** | `StructureActionPanelUI` (day: install/upgrade/repair, both Fence and Tower) and `NightRepairPanelUI` (night: repair only) are two separate classes, each handling both Fence and Tower. The old "install/info/upgrade split into several panels" problem is gone. `GameManager` explicitly closes the inactive-phase panel on transition. |

---

## 6. Fence System

### Data and ownership

`FenceData` is the data source for fence identity and progression. It includes fields such as:

- ID, display name, tier, icon, and prefab.
- Max HP.
- Install, repair, and upgrade costs.
- Repair amount.
- `NextFence` progression reference and max-tier state.

`FenceSlot` is the permanent interaction and persistence owner. `FenceSegment` owns the live installed fence behavior, HP, destroyed state, visuals, and barrier colliders.

Confirmed by code audit: `FenceSlot.CanInteract()` allows interaction any time the slot is unlocked during Day (regardless of damage state), and allows repair-triggered interaction at Night whenever `FenceSegment.CanRepair()` is true (which includes the destroyed state). So a destroyed fence remains interactable and repairable through its permanent slot — this part of the system works correctly.

### Reported recent implementation: `Starting FenceData`

Phase 3 changed the initial install source in `FenceSlot` toward data-driven configuration:

```csharp
[SerializeField] private FenceData startingFenceData;
public FenceData StartingFenceData => startingFenceData;
```

When assigned, installation resolves from:

- Prefab: `startingFenceData.FencePrefab`
- Wood cost: `startingFenceData.InstallWoodCost`
- Scrap cost: `startingFenceData.InstallScrapCost`

When unassigned, the existing serialized `fencePrefab`, `woodCost`, and `scrapCost` remain as fallbacks. The reported change was limited to `Assets/Scripts/Interaction/FenceSlot.cs`. Verify this against the current repository before expanding the migration.

### Provisional fence balance

| Tier | Max HP | Repair amount |
| --- | ---: | ---: |
| T1 | 100 | +30 |
| T2 | 160 | +30 |
| T3 | 210 | +30 |
| T4 | 260 | +30 |

Treat these as balancing baselines, not immutable final values.

### Latest Fence UI rule - approved

- Use **one unified Fence Management Panel**.
- Do not design separate install, information, and upgrade panels.
- Show upgrade and repair costs directly inside their action buttons.
- Current fence progression for this UI is linear: **Empty -> T1 -> T2**.
- Initial panel states: Empty, Installed/Full HP, Damaged, Destroyed, and Max Tier.
- UI must be concise, consistent, intuitive, and avoid player confusion.
- Figma is the editable source of truth for approved HUD/panel structure when the Figma file is available.

**Current implementation gap (confirmed 2026-09-05):** the day/night panel split described in Section 5 is a reasonable structural decision (different action sets per phase) and is not the "several confusing panels" problem this rule originally warned against. But the panel does **not yet** distinguish Destroyed from Damaged as separate visual/interaction states, and the Upgrade action is hardcoded disabled. Building these two things out is the next concrete step toward this approved design.

---

## 7. Tower System and Ability Direction

### Current tower baseline

- `ArrowTower` uses yaw aiming and projectile attacks.
- Frequent target queries use a non-alloc overlap approach where applicable.
- Destroyed tower logic stops operating.
- Tower slots own build location, interaction, persistent identity, and installed tower state.
- Earlier repair baseline: `+20 HP` for `1 Wood`; treat as provisional.
- **Open risk:** `TowerSlot.EnsureCurrentTowerReference()` still does proximity-based tower adoption — see Section 4.

### Progression direction

Towers should branch or evolve instead of relying only on a single linear stat ladder.

Planned tower families:

- Flamethrower
- Shotgun
- Catapult
- Sniper
- Laser
- Machine Gun

### Planned randomized ability system

| Item / concept | Direction |
| --- | --- |
| Ability Hammer | Randomizes a tower's current ability set; first use may be free, later rerolls cost resources. |
| Card Key | Gradually unlocks tower ability slots, approximately around Days 5-10 in the long-term design. |
| Ability pool | Damage, attack speed, projectile count, firing behavior, and other gameplay-changing effects. |
| Management action | Three current abilities with one `REROLL ALL` action. |

Exact ability-card designs are waiting for Sean's future sketches. Do not invent final cards and present them as approved.

---

## 8. Enemy and Wave Systems

### Zombie behavior

- Finds a valid nearest or priority target.
- Uses surface navigation; root motion is disabled.
- Delivers attack damage through an Animation Event at the intended impact frame.
- Maintains `Zombie.AliveZombieCount` for wave resolution.

### `SimpleZombieSpawner` authoritative flow

```text
Initial wave delay
-> spawn configured wave
-> wait until spawning is complete AND AliveZombieCount == 0
-> inter-wave delay
-> next configured wave
-> final wave resolves
-> request Day transition exactly once
```

Inspector configuration includes:

- Initial Wave Start Delay.
- Inter Wave Delay.
- Night Waves array with Zombie Count and Spawn Interval.
- `FullCircle` and `DirectionalArc` spawn modes.
- Arc Angle and gizmos using spawner forward direction.

A prior bug transitioned to Day while Wave 2 was still spawning. It was fixed by removing competing night countdown logic from `GameManager`. Never reintroduce another independent phase-transition authority.

### Planned enemy roles

| Enemy | Intended role |
| --- | --- |
| Basic | Standard baseline. |
| Runner | Fast movement pressure. |
| Breaker | Fence-focused threat. |
| Tank | High-health sustained pressure. |
| Elite / Boss | Major night events communicated through wave markers and stronger presentation. |

---

## 9. Approved HUD and Visual Direction

### Final Gameplay HUD V2.0

| Location | Approved content |
| --- | --- |
| Top center - Day | Sun/day illustration, day number, and day timer. |
| Top center - Night | Moon/night illustration, `NIGHT 1`, red or threat-focused horizontal wave progress, vertical markers for elites/bosses/events. |
| Left | Health and Hunger. |
| Lower left | Wood, Scrap, and Food. |
| Bottom center - Night | Base Core HP. |

### Interaction feedback

- Keep the shared prompt concise: `[E]`.
- Gathering gauge: translucent charcoal/black background with a simple green decreasing fill.
- Do not turn the gathering gauge into a large panel.
- Interaction feedback should stay readable without covering gameplay.

### Approved structure/tower concepts

- Structure Install UI is world-anchored and includes useful stats.
- Tower T1 -> T2 upgrade presentation is approved.
- T3 Basic and the evolution list direction are approved.
- Ballista evolution preview uses `E EVOLVE`, `R REPAIR`, `ESC CANCEL`, and a small 3D header miniature.
- The standard Ballista ability-management screen displays three current abilities and `REROLL ALL`; it is not an unlock screen, reroll confirmation, or reroll-result animation.

### Art and composition

- Low-poly foundation, cute character proportions, and clear silhouettes.
- Day: nature-reclaimed village, calm but uneasy.
- Night: dark, dense, dimensional fog and strong threat contrast without unreadable clutter.
- Home base: inner fence ring only, towers positioned inside near the fence, warm base lantern focal point.
- Approved defense composition reduced an earlier oversized fence ring by roughly 15-20%.

### Wood gathering benchmark

The Wood gathering scene is the reference for later Scrap and Food gathering visuals.

- Match the approved camera-to-character distance and angle closely.
- Select the target tree with `[E]` before gathering begins.
- Use player gathering animation, tree shake, falling leaves, dust, and light impact feedback.
- Preserve locked HUD and composition decisions.

---

## 10. Known Risks to Audit First

These are not permission to rewrite the architecture immediately. Inspect and report the actual repository state first. **Status column reflects the 2026-09-05 code audit — see Section 0 for what "Verified in code" means.**

| # | Risk | Status (2026-09-05) |
| --- | --- | --- |
| 1 | Standalone structures may bypass slots | **Resolved for real slots.** No duplicated/standalone Fence or Tower slots found; all consolidated under `HomeBase.prefab`. 21 decorative fence-shaped props exist with zero gameplay components — harmless, deliberately left alone. |
| 2 | Tower adoption or duplication risk | **Still open.** `TowerSlot.EnsureCurrentTowerReference()` proximity-adoption logic is unchanged. Less likely to trigger under the single-scene model, but not removed. |
| 3 | Base Core persistence may be incomplete | **Resolved differently than expected.** No capture/apply pipeline exists (deleted), but damage persists naturally because the object is never destroyed. Needs a Play Mode regression test to confirm end-to-end. |
| 4 | Destroyed-fence interaction may be incomplete | **Interaction logic: resolved.** Destroyed fences remain interactable/repairable via their slot. **Visual state distinction: not implemented** — Destroyed and Damaged are not visually or functionally distinguished yet. |
| 5 | Runtime state is not necessarily disk save | **Confirmed absent.** No save/load code exists anywhere in `Assets/Scripts`. |
| 6 | Old and new Fence UI architecture conflict | **Old multi-panel conflict: resolved** (no leftover install/info/upgrade split found). Day/Night panel split remains, for a defensible reason. Upgrade flow and Destroyed/Max-Tier UI states are still unimplemented. |
| 7 | Document/code drift | **Confirmed and scoped.** `Cute_Carnage_GDD.md` and `Cute_Carnage_Game_Structure_Guide.md` (2026-08-12) describe the old separate-scene/capture-apply architecture and have not been updated to match the single-scene migration. Also: this file (`PROJECT_HANDOFF.md`) had never actually been committed to the git repository until this update — treat that as a resolved process gap now that it's committed, but double-check on future audits that it's still there. |
| 8 | **(New)** Retry/checkpoint design has no implementation path | The originally assumed mechanism (scene reload -> natural reset) no longer applies under the single-scene architecture. A Night N failure returning to Day N daytime now requires an explicit, newly designed rollback/checkpoint system. This needs Sean's architectural decision before any implementation is attempted. |

---

## 11. Immediate Priorities

Work in this order unless Sean changes the goal:

1. Keep this file (`PROJECT_HANDOFF.md`) committed and current in the repository — it was previously missing entirely.
2. Propose a fix for the tower proximity-adoption risk (Known Risk 2) as a reviewable diff; do not auto-merge.
3. Build out the unified Fence Management Panel's remaining gaps: Destroyed vs Damaged visual state, and the Upgrade flow.
4. Update `Cute_Carnage_GDD.md` and `Cute_Carnage_Game_Structure_Guide.md` to reflect the single-scene architecture (draft only; Sean approves before treating as canonical).
5. Fix or verify the HUD Hunger display.
6. Run a Night -> Day -> Night regression test to confirm Base Core / Fence / Tower damage actually persists correctly end to end.
7. Design the retry/checkpoint system (Known Risk 8) — architectural decision needed from Sean before implementation.
8. Run and polish the complete 7-day / 7-night loop.
9. Finalize the graybox map using South/North/West/East resource identities.
10. Add proper zombie models, animation polish, and spawner variants.
11. Implement the approved Wood gathering benchmark for Scrap and Food.
12. Later, build tower ability cards from Sean's sketches.

### Later backlog

- Movement-cancels-gathering refinement.
- Daytime danger zones.
- Resource respawn and world events.
- Additional tower branches.
- Random tower abilities, Card Keys, and Ability Hammer economy.
- Full enemy variants, elites, bosses, and stronger wave progression.
- `HUDController.ResolveMissingSources()` — polls every ~1s in `Update()` via `nextSourceResolveTime`, violating the event-driven-over-polling principle. Out of scope for unrelated passes; fix only in a dedicated task.
- Polished audio, VFX, content capture, and demo presentation.

---

## 12. Required Regression Checks

### Persistence changes

- Test Day -> Night and Night -> Day.
- Confirm each fence slot maps to the same persistent ID.
- Confirm each tower slot maps to the same persistent ID.
- Verify resources, Hunger, installed types, HP, and destroyed states.
- Verify no duplicate scene structure is adopted.
- **New:** verify Base Core / Fence / Tower damage taken at Night is still present after transitioning to Day and back to Night again — this now relies on objects never being destroyed rather than an explicit save, so confirm it actually holds under real play.

### Wave changes

- Verify every configured wave count and interval.
- Verify `AliveZombieCount` returns to zero correctly.
- Verify the final completion callback fires once.
- Verify no Day transition occurs while spawning or living zombies remain.

### Interaction/UI changes

- Test at 1920x1080.
- Test `[E]` prompt follow behavior and visibility.
- Test gathering start, cancellation, and phase transition cleanup.
- Test `ESC` close and reopening the same structure panel.
- Test Empty, Full HP, Damaged, Destroyed, and Max Tier fence states (Damaged/Destroyed distinction and Upgrade flow are not implemented yet — see Section 5).
- Confirm costs displayed in buttons match the actual consumed resources.

### Structure changes

- Damage to zero HP.
- Destroyed visuals and collider changes.
- Repair/rebuild interaction from the slot.
- Upgrade and max-tier behavior (not yet implemented — see Section 5).
- Scene transition with damaged and destroyed structures.

---

## 13. Working Protocol with Sean

- Sean is the solo developer and final decision-maker.
- Explain Unity steps clearly and sequentially when manual Inspector or scene setup is required.
- For code requests, state the exact intended scope and avoid unrelated refactors.
- For diagnosis requests, diagnose first; do not modify code until implementation is requested.
- Preserve existing working-tree changes and mention files actually changed.
- Use a Git checkpoint after a stable, tested milestone.
- When an image is marked approved, preserve locked camera, crop, HUD, lighting, object positions, and base plate unless Sean explicitly unlocks them.
- Do not describe planned visual concepts as implemented gameplay.
- Cloud/unattended sessions cannot run the Unity Editor: no compilation check, no Play Mode test, no visual verification. Treat all cloud-produced results as "Reported implemented / Verified in code (static)" pending Sean's local confirmation, and say so explicitly in every completion report.

---

## 14. Companion Documents

- `PROJECT_HANDOFF.md` - first file Claude should read; latest operational context and risks. **Must be kept committed to the repository root** (see Known Risk 7).
- `Cute_Carnage_GDD.md` - design baseline. **Currently stale on scene architecture — see Section 0.**
- `Cute_Carnage_Game_Structure_Guide.md` - technical architecture baseline. **Currently stale on scene architecture — see Section 0.**
- `Single_Scene_Migration_Proposal.md` - documents the actual single-scene migration that GDD/Structure Guide have not caught up to. Treat as more current than those two on scene-structure questions.
- Original PDFs - historical reference only.

When updating GDD/Structure Guide, add a code-audit appendix containing actual scene names, authoritative script paths, prefab ownership, ScriptableObject asset locations, and confirmed save schemas.

---

## Fast Start Prompt for Claude

> You are continuing Sean's Unity 6.3 LTS solo game **Cute Carnage** for **Kyven Games**. Read `PROJECT_HANDOFF.md` first, then `Single_Scene_Migration_Proposal.md`, then the GDD and Game Structure Guide (noting those two are currently stale on scene architecture). The prototype target is a stable 7-day/7-night loop; 30 nights is long-term. The game runs in **one persistent scene** with Day/Night as runtime phase states — there is no scene reload and no capture/apply runtime-state pipeline; Fence/Tower/BaseCore damage persists because objects are never destroyed. Night may transition to Day only after the final configured wave finishes spawning and all spawned zombies are dead. Preserve persistent slot IDs, approved HUD V2.0, locked visual base plates, and the latest unified Fence Management Panel decision (Destroyed/Damaged distinction and Upgrade flow are still unbuilt). The tower proximity-adoption risk in `TowerSlot.EnsureCurrentTowerReference()` is still open. Before changing code, inspect the current repository, identify document/code conflicts, preserve unrelated changes, and distinguish verified implementation from approved design and future plans.
