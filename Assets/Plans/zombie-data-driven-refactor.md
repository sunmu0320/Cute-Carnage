# Project Overview
- **Game Title:** Cute Carnage / Survive!
- **High-Level Concept:** A 3D top-down survival defense game where waves of zombies attack the player, fences, towers, and a central base core.
- **Players:** Single player
- **Inspiration / Reference Games:** Top-down survival / base-defense (wave defense)
- **Tone / Art Direction:** "Cute Carnage" — stylized, light-hearted carnage
- **Target Platform:** PC (StandaloneWindows64)
- **Screen Orientation / Resolution:** Landscape (PC)
- **Render Pipeline:** URP (PC_RPAsset)

# Goal
Refactor the existing single-type `BasicZombie.cs` prototype into a **reusable, data-driven** zombie system so future zombie types (Basic, Runner, Breaker, Tank) can be created from one shared `Zombie.cs` script + multiple `ZombieData` assets — **without** rewriting AI, changing target priority, adding animation logic, or creating per-type scripts.

This is a **safe, minimal** refactor: stats move into an optional `ZombieData` ScriptableObject, but the existing serialized fields are kept as fallback defaults so the current prototype behaves identically if no data asset is assigned.

# Game Mechanics
## Core Gameplay Loop
Unchanged by this refactor. Zombies spawn at night, walk toward the base, and attack the first valid target in their forward detection cone (fences, towers, player, base core). Player and towers shoot zombies. When all zombies are cleared, the night wave completes.

## Controls and Input Methods
Unchanged. Debug damage key (`K`) on zombies is preserved.

# UI
No UI changes in this refactor.

# Key Asset & Context

## Current architecture (verified)
- `Assets/Scripts/Enemies/BasicZombie.cs` — class `BasicZombie : MonoBehaviour`, GUID `77a738595f64b4941a6646d5cbcdef13`.
  - Static `public static int AliveZombieCount` (incremented OnEnable / decremented OnDestroy).
  - Serialized stats: `moveSpeed`, `attackRange`, `attackDamage`, `attackInterval`, `targetRefreshInterval`, `forwardDetectRange`, `forwardDetectRadius`, `maxHp`.
  - Serialized config/debug (stay on the component, NOT moved to data): `forwardDetectStartOffset`, `forwardDetectMask`, `baseCoreNameCandidates`, `enableFrontTargetDebugLogs`, gizmo flags/colors, `debugDamageKey`, `debugDamageAmount`, `lungeDistance`, `lungeDuration`.
  - Public API used by other scripts: `IsDead`, `CurrentHp`, `MaxHp`, `TakeDamage(float)`.
  - Target priority = nearest valid collider inside the forward overlap-capsule (no preference weighting).

## Files that reference `BasicZombie` (must be updated on rename)
- `Assets/Scripts/Player/PlayerAutoCombat.cs` — many uses of the `BasicZombie` type (fields, params, `FindObjectsByType<BasicZombie>`, `GetComponentInParent<BasicZombie>`).
- `Assets/Scripts/Towers/ArrowTower.cs` — `GetComponentInParent<BasicZombie>` and `BasicZombie` params.
- `Assets/Scripts/Player/SimpleProjectile.cs` — `BasicZombie` field + param.
- `Assets/Scripts/Systems/SimpleZombieSpawner.cs` — `BasicZombie.AliveZombieCount` (static).

## Prefab / scene references (verified)
- Only **one** asset references the script: `Assets/Prefabs/Enemy/Zombie1.prefab`.
  - It references the script by **GUID** (`m_Script: {fileID: 11500000, guid: 77a738595f64b4941a6646d5cbcdef13}`) — preserved by keeping the `.meta`.
  - It also has `m_EditorClassIdentifier: Assembly-CSharp::BasicZombie` which must be updated to `Assembly-CSharp::Zombie`.
- No `.unity` scene references the script directly (zombies are spawned at runtime by `SimpleZombieSpawner`).

## Why renaming is SAFE here
Unity links MonoBehaviour components to scripts by the script's **GUID**, not by class name. As long as:
1. We keep the existing `.meta` file (so GUID `77a73859...` is preserved), and
2. The new file name matches the new class name (`Zombie.cs` ↔ class `Zombie`), and
3. We update the 4 C# references and the prefab's `m_EditorClassIdentifier` string,
then the prefab's component, its serialized values, and all wiring remain intact.

# Design Decisions

### Decision 1 — Rename strategy (recommended: full rename)
- **Option A (RECOMMENDED): Rename file + class `BasicZombie` → `Zombie`, update the 4 referencing scripts, preserve `.meta` GUID.**
  - Pros: Exactly what the user asked for (one shared `Zombie.cs`); clean; static `AliveZombieCount` lives on `Zombie`; `FindObjectsByType<Zombie>` finds all zombie types in future.
  - Cons: Must edit 4 other files + 1 prefab string. Low risk because GUID is preserved.
- Option B: Keep `BasicZombie`, add an empty `Zombie` base and make `BasicZombie : Zombie`.
  - Cons: Splits the static count and `FindObjectsByType` across types; messier; not "one shared script". Rejected.
- Option C: Keep class name `BasicZombie`, only add `ZombieData`.
  - Cons: Doesn't meet the rename goal. Rejected.

### Decision 2 — Stats source (recommended: data overrides, serialized fallback)
- **RECOMMENDED:** Keep the existing serialized stat fields as defaults. Add an optional `[SerializeField] ZombieData zombieData`. In `Awake()`, if `zombieData != null`, copy its values onto the runtime stat fields **before** they are used; otherwise keep the serialized values.
  - Pros: Backward compatible — the existing `Zombie1.prefab` (no data assigned) behaves identically. New zombies just assign a `ZombieData` asset. Safe & minimal.
  - This satisfies "read its values from ZombieData instead of hardcoded fields where appropriate" while guaranteeing no behavior change for the existing prototype.

### Decision 3 — Target preference (recommended: store but do NOT wire yet)
- `ZombieTargetPreference` is stored on `ZombieData` and cached on the component, but the current nearest-in-cone priority logic is **left unchanged** (default `Normal`). Per the requirement "do not change current target priority unless necessary." Preference weighting can be implemented later. Documented as a future hook.

### Decision 4 — Animator controller (store only)
- `animatorController` is stored on `ZombieData` and the field exists on the component, but **no Animator wiring** is added now (per "do not add animation controller logic yet").

# Key Asset & Context — New API shapes

`ZombieData.cs` (new ScriptableObject):
```csharp
public enum ZombieType { Basic, Runner, Breaker, Tank }
public enum ZombieTargetPreference { Normal, PreferStructures, PreferPlayer, PreferBaseCore }

[CreateAssetMenu(fileName = "ZombieData", menuName = "Cute Carnage/Zombie Data")]
public class ZombieData : ScriptableObject
{
    public string zombieId = "basic";
    public ZombieType zombieType = ZombieType.Basic;
    public ZombieTargetPreference targetPreference = ZombieTargetPreference.Normal;

    [Header("Stats")]
    public float maxHp = 30f;
    public float moveSpeed = 2f;
    public float attackDamage = 8f;
    public float attackRange = 1.4f;
    public float attackInterval = 1f;
    public float targetRefreshInterval = 1f;

    [Header("Detection")]
    public float forwardDetectRange = 1.6f;
    public float forwardDetectRadius = 0.6f;

    [Header("Animation (optional, used later)")]
    public RuntimeAnimatorController animatorController;
}
```

`Zombie.cs` additions (on top of renamed `BasicZombie`):
```csharp
[Header("Data (optional)")]
[SerializeField] private ZombieData zombieData;

// cached at runtime from data (not used by AI yet)
private ZombieType zombieType;
private ZombieTargetPreference targetPreference;
private RuntimeAnimatorController animatorController;

private void ApplyZombieData()
{
    if (zombieData == null) return;          // keep serialized fallback values
    maxHp                 = zombieData.maxHp;
    moveSpeed             = zombieData.moveSpeed;
    attackDamage          = zombieData.attackDamage;
    attackRange           = zombieData.attackRange;
    attackInterval        = zombieData.attackInterval;
    targetRefreshInterval = zombieData.targetRefreshInterval;
    forwardDetectRange    = zombieData.forwardDetectRange;
    forwardDetectRadius   = zombieData.forwardDetectRadius;
    zombieType            = zombieData.zombieType;
    targetPreference      = zombieData.targetPreference;   // stored, not yet driving priority
    animatorController    = zombieData.animatorController;  // stored, not wired yet
}
```
`ApplyZombieData()` is called at the very start of `Awake()`, before `maxHp`/`currentHp` are clamped, so HP initialization uses the data value.

# Implementation Steps

### Step 1 — Create `ZombieData.cs` (ScriptableObject + enums)
- **Description:** Create `Assets/Scripts/Enemies/ZombieData.cs` exactly as in the API shape above (enums `ZombieType`, `ZombieTargetPreference`, and the `ZombieData` ScriptableObject with `CreateAssetMenu`). Defaults match the current `BasicZombie` defaults.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (independent of the rename)

### Step 2 — Rename `BasicZombie.cs` → `Zombie.cs` and class → `Zombie`
- **Description:**
  1. Rename the file `Assets/Scripts/Enemies/BasicZombie.cs` to `Zombie.cs`, **keeping the existing `.meta`** so GUID `77a738595f64b4941a6646d5cbcdef13` is preserved (in the Unity Editor: rename the script asset in the Project window, which renames the `.meta` automatically; if editing on disk, rename both `BasicZombie.cs` and `BasicZombie.cs.meta` → `Zombie.cs` / `Zombie.cs.meta`).
  2. Rename `class BasicZombie` → `class Zombie`.
  3. Update the internal self-reference `col.GetComponentInParent<BasicZombie>()` (the "skip self" check inside `SelectFrontTarget`) → `GetComponentInParent<Zombie>()`.
  4. Update internal debug log prefixes `"[BasicZombie]"` → `"[Zombie]"` (cosmetic only).
  5. Keep `public static int AliveZombieCount` on `Zombie` unchanged in behavior.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** No (other steps depend on the class name)

### Step 3 — Add `ZombieData` integration to `Zombie.cs`
- **Description:** Add the `zombieData` serialized field, cached `zombieType` / `targetPreference` / `animatorController` fields, and the `ApplyZombieData()` method (see API shape). Call `ApplyZombieData()` as the **first line of `Awake()`**, before the existing `maxHp = Mathf.Max(...)` / `currentHp = ...` lines so HP uses data values. Do **not** change target selection, movement, attack, death, gizmo, or lunge logic. Keep all serialized fields as fallbacks.
- **Assigned role:** developer
- **Dependencies:** Step 1 (needs `ZombieData` type), Step 2 (same file)
- **Parallelizable:** No

### Step 4 — Update the 4 referencing scripts (`BasicZombie` → `Zombie`)
- **Description:** Replace every `BasicZombie` type reference with `Zombie` in:
  - `Assets/Scripts/Player/PlayerAutoCombat.cs` (fields, method params/returns, `FindObjectsByType<BasicZombie>`, `GetComponentInParent<BasicZombie>`, `IsValidTarget`, `IsWithinRange`, `GetZombieAimPoint`, raycast helpers).
  - `Assets/Scripts/Towers/ArrowTower.cs` (`GetComponentInParent<BasicZombie>`, params).
  - `Assets/Scripts/Player/SimpleProjectile.cs` (`deferredTarget` field + `Initialize` param).
  - `Assets/Scripts/Systems/SimpleZombieSpawner.cs` (`BasicZombie.AliveZombieCount` → `Zombie.AliveZombieCount`).
  - Public API used (`IsDead`, `CurrentHp`, `MaxHp`, `TakeDamage`) is unchanged, so only the type name changes.
- **Assigned role:** developer
- **Dependencies:** Step 2
- **Parallelizable:** Yes (the 4 files are independent of each other, but all depend on Step 2)

### Step 5 — Fix the prefab's class identifier string
- **Description:** In `Assets/Prefabs/Enemy/Zombie1.prefab`, change `m_EditorClassIdentifier: Assembly-CSharp::BasicZombie` → `Assembly-CSharp::Zombie`. The `m_Script` GUID stays the same, so the component keeps its serialized values. (If done in the Editor, Unity rewrites this automatically on save; manual edit is only needed if the file isn't re-saved by the Editor.)
- **Assigned role:** developer
- **Dependencies:** Step 2
- **Parallelizable:** Yes

### Step 6 — Create `BasicZombieData.asset` matching the current prefab values
- **Description:** Create `Assets/Data/Enemies/BasicZombieData.asset` (folder created if missing) via `Assets > Create > Cute Carnage > Zombie Data`. Set values to match the current `Zombie1.prefab`: zombieId `basic`, zombieType `Basic`, targetPreference `Normal`, maxHp 30, moveSpeed 2.5, attackDamage 10, attackRange 1.4, attackInterval 1, targetRefreshInterval 1, forwardDetectRange 1.6, forwardDetectRadius 0.6, animatorController none. Then assign it to the `Zombie Data` field on `Zombie1.prefab` (optional — leaving it null keeps the current serialized values).
- **Assigned role:** developer
- **Dependencies:** Step 1, Step 3
- **Parallelizable:** No

# Verification & Testing
1. **Compile check:** No console compile errors after Steps 1–5 (search project for any remaining `BasicZombie` identifier — should only appear in old plan docs, not in `.cs`/`.prefab`).
2. **Prefab integrity:** Open `Zombie1.prefab` — the `Zombie` component shows all prior values intact (moveSpeed 2.5, maxHp 30, etc.) and a new empty `Zombie Data` slot.
3. **Backward-compat behavior (no data):** Enter Play Mode with `zombieData` left null. Verify: zombies spawn, move toward the base, detect/attack fences (`FenceSegment`), towers (`ArrowTower`), player (`PlayerHealth`), and base core (`BaseCore`); death + destroy works; debug key `K` damages; gizmos draw on selection. Behavior identical to before.
4. **Data-driven behavior:** Assign `BasicZombieData.asset` to the prefab, Play, confirm identical behavior. Then edit `moveSpeed` in the asset (e.g., 5) and confirm spawned zombies move faster — proving data drives stats.
5. **Wave count:** Confirm `Zombie.AliveZombieCount` still tracks correctly and `SimpleZombieSpawner` transitions to Day after all zombies are cleared.
6. **Player/Tower targeting:** Confirm `PlayerAutoCombat` and `ArrowTower` still find, hit, and kill zombies (their `FindObjectsByType<Zombie>` / `GetComponentInParent<Zombie>` resolve correctly).

# How this supports Runner / Breaker / Tank later
- Create additional `ZombieData` assets (`RunnerData`, `TankData`, `BreakerData`) with different stats and `zombieType`, and assign each to its own zombie prefab variant — **no new scripts required**.
- `zombieType`, `targetPreference`, and `animatorController` are already parsed and cached on the component, providing ready hooks to later (a) swap the Animator controller, (b) implement preference-weighted target selection, and (c) branch type-specific behavior (e.g., Breaker bonus damage vs structures, Tank high HP) inside the single `Zombie.cs` using the cached `zombieType`/`targetPreference` — all additive, without touching the data plumbing established here.
