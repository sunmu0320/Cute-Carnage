# Project Overview
- Game Title: Cute Carnage
- Goal: Fix HUD synchronization so it accurately displays restored player HP and Hunger values after scene transitions.

# Game Mechanics
## Core Gameplay Loop
The player's state is persisted between scenes. The HUD must update immediately upon scene load to show the restored values.

# UI
- **HUDController.cs**: The central script managing the in-game HUD. It needs to correctly reference player components and refresh its display when state is restored.

# Key Asset & Context
- **HUDController.cs**: Manages HP, Hunger, and resource display.
- **GameManager.cs**: Handles scene transitions and triggers state application.

# Implementation Steps
## 1. Update HUDController to auto-resolve references and support manual refresh
**Description**: 
- Modify `HUDController.cs` to auto-resolve `playerHealth`, `hungerSystem`, and `resourceManager` if they are null in `RefreshFromSources`.
- Add a public `RefreshHudFromPlayerStats()` method that handles re-binding events and refreshing all UI elements.
- This ensures that even if the scene instance is missing references (as discovered in the Night scene), the HUD will find the correct components.
- Assigned role: developer
- Dependencies: None
- Parallelizable: Yes

## 2. Trigger HUD Refresh from GameManager
**Description**:
- In `GameManager.ApplyRunRuntimeStateToScene`, after applying player state, find the `HUDController` and call `RefreshHudFromPlayerStats()`.
- This guarantees the UI is updated immediately after the data is restored.
- Assigned role: developer
- Dependencies: Step 1
- Parallelizable: No

# Verification & Testing
1. **Scene Transition**: Transition from Day to Night with non-default HP/Hunger (e.g. 80 HP).
2. **Verification**: Verify the Night scene HUD immediately shows 80 HP instead of 100 HP.
3. **Transition Back**: Transition from Night back to Day.
4. **Verification**: Verify the Day scene HUD also correctly shows the restored state.
