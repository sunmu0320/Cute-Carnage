# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: 7-day survival prototype where the player manages resources and health between Day and Night cycles.
- Players: Single player.
- Inspiration / Reference Games: Survival/Defense games like 7 Days to Die or similar resource-management hybrids.
- Tone / Art Direction: 2D stylized/cute (implied by title).
- Target Platform: PC (StandaloneWindows64).
- Screen Orientation / Resolution: Landscape.
- Render Pipeline: URP (PC_RPAsset).

# Game Mechanics
## Core Gameplay Loop
The player explores and gathers resources (wood, scrap, food) during the Day phase. At Night, the player defends against zombies. The game lasts for 7 days.
## Controls and Input Methods
- Movement: Keyboard/Mouse.
- Interaction: Keyboards (e.g., 'Q' to consume food, 'F' for debug damage).
- Persistence: Automatic saving of HP, Hunger, and resources during scene transitions.

# UI
- HUD: Displays HP, Hunger, Resources (Food, Scrap, Wood), Day, and Time.
- Persistence Feedback: Logs in the console confirming state save/restore.

# Key Asset & Context
- **GameManager.cs**: Manages scene transitions and overall run state.
- **PlayerRuntimeState.cs**: Serializable data container for player persistence.
- **PlayerHealth.cs**: Manages player HP in the scene.
- **HungerSystem.cs**: Manages player hunger and starvation logic in the scene.
- **HUDController.cs**: Updates the UI based on player and resource states.

# Implementation Steps
## 1. Ensure PlayerRuntimeState has sane defaults
**Description**: Modify `PlayerRuntimeState.cs` to initialize `currentHp` and `currentHunger` to 100f by default. This prevents the state from falling back to 0 during initialization or if components are missing.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: Yes

## 2. Fix HungerSystem initialization for Day 1 bootstrap
**Description**: Modify `HungerSystem.cs` to make `currentHunger` a serialized field with a default value of 100f. This ensures that when the `GameManager` captures the scene state on the very first Day, it captures the intended starting hunger instead of an uninitialized 0.
**Assigned role**: developer
**Dependencies**: None
**Parallelizable**: Yes

## 3. Implement Persistence Debug Logs in GameManager
**Description**: 
- Update `GameManager.CaptureRunStateFromScene` to log: `[PlayerRuntimeState] Saved Player HP={hp} Hunger={hunger} before {phase} transition.`
- Update `GameManager.ApplyRunRuntimeStateToScene` to log: `[PlayerRuntimeState] Restored Player HP={hp} Hunger={hunger} on {context} start.`
- Use logic to differentiate between "Night scene start" and "Day X start".
**Assigned role**: developer
**Dependencies**: Step 1, Step 2
**Parallelizable**: No

## 4. Final Review of Sync Logic
**Description**: Ensure `PlayerHealth.Awake` and `HungerSystem.Awake` logic for pulling state from `GameManager` is consistent with the `ApplyRunRuntimeStateToScene` method to avoid race conditions or double-initialization issues.
**Assigned role**: explorer
**Dependencies**: Step 3
**Parallelizable**: No

# Verification & Testing
1. **Day 1 Start**: Verify the player starts with 100 HP and 100 Hunger (not 0).
2. **Day -> Night Transition**: 
   - Spend some hunger (wait or use debug keys if available).
   - Verify log: `[PlayerRuntimeState] Saved Player HP=100 Hunger=90 before Day transition.`
   - Verify log: `[PlayerRuntimeState] Restored Player HP=100 Hunger=90 on Night scene start.`
3. **Night -> Day Transition**:
   - Take damage (e.g., zombie hit or debug key).
   - Verify log: `[PlayerRuntimeState] Saved Player HP=80 Hunger=70 before Night transition.`
   - Verify log: `[PlayerRuntimeState] Restored Player HP=80 Hunger=70 on Day 2 start.`
4. **Starvation Test**: Let hunger reach 0 and verify HP starts decreasing. Verify this persists through transitions.
5. **Full Loop**: Play through multiple days and verify values continue to persist accurately.
