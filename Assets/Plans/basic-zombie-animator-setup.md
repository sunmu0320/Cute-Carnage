# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: A top-down survival and base-defense game where zombies attack a core base, and the player defends using towers, fences, and active combat.
- Players: Single player (VS AI)
- Inspiration / Reference Games: Plants vs. Zombies, Orcs Must Die!
- Tone / Art Direction: Cute low-poly stylized aesthetics.
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape 1920x1080 (PC Standard)
- Render Pipeline: URP (using PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
Zombies spawn continuously and navigate towards the main BaseCore. If their path is blocked, they attack obstacles like FenceSegment, ArrowTower, or the Player. The player must actively build, upgrade, and defend the core to survive.
## Controls and Input Methods
Zombie locomotion and state transitions are handled procedurally through their state machine, but movement remains physics/gameplay-driven (no root motion).

# UI
Not applicable (this is an under-the-hood gameplay/animation connection task).

# Key Asset & Context
- **Zombie.cs**: The enemy behavior script containing locomotion, targeting, attack, and health.
- **BasicZombie prefab**: Located at `Assets/Prefabs/Enemy/BasicZombie.prefab`. It contains the `Zombie` script and a child Animator under `Visual/BasicZombie`.
- **Animator parameters**:
  - `Speed` (Float): Controls transitioning between Idle and Walk states.
  - `Attack` (Trigger): Triggers the attack animation.
  - `Die` (Trigger): Triggers the death animation (not implemented yet).

# Implementation Steps

## Step 1: Update Zombie.cs with Animator Reference and Caching
- **Description**: 
  - Add a serialized `Animator` reference: `[SerializeField] private Animator animator;`
  - In `Awake()`, if `animator` is null, assign it using `GetComponentInChildren<Animator>(true)`.
  - If the Animator has no `RuntimeAnimatorController`, assign `animatorController` (loaded from `ZombieData` via `ApplyZombieData()`). Do not override an already assigned controller.
  - Cache parameter hashes for `Speed` and `Attack` using `Animator.StringToHash("Speed")` and `Animator.StringToHash("Attack")`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Implement Animator Logic in Zombie.cs
- **Description**:
  - In `Update()`, set the `Speed` parameter to `0` at the very beginning of the living update loop. (All Animator calls must be null-safe).
  - In `HandleMovement()`, check the position of the zombie before and after `Vector3.MoveTowards`. Set `Speed` to `1` only if the position actually changed. (All Animator calls must be null-safe).
  - In `HandleAttack()`, when the damage cycle is successfully executed, trigger the `Attack` parameter on the animator exactly once (fired only inside the existing successful damage cycle, preserving DoAttackLunge() and timing). (All Animator calls must be null-safe).
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Configure BasicZombie Prefab
- **Description**: 
  - Open the `BasicZombie` prefab (`Assets/Prefabs/Enemy/BasicZombie.prefab`).
  - Assign the child Animator (`Visual/BasicZombie`) to the new `animator` field on the `Zombie` component on the prefab root.
  - **No external scripts, backup folders, or temporary assets will be created.**
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

# Verification & Testing
1. **Compilation Check**: Verify that there are no new compilation errors or warnings. (Report only new compilation errors or warnings, not pre-existing project warnings).
2. **Animator Reference Check**: Directly inspect the serialized properties of `BasicZombie.prefab` after saving to ensure that the `animator` reference is correctly assigned to the child `Visual/BasicZombie` GameObject.
3. **Behavioral Integrity**: Ensure all animator calls are null-safe and the existing movement/rotation/targeting/stopping-distance or attack/damage logic in `Zombie.cs` has not been altered.


