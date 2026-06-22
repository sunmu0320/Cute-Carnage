# Project Overview
- Game Title: Top-Down Survival/Base-Defense
- High-Level Concept: Zombies attack a base, player defends using towers and fences.
- Players: Single Player
- Render Pipeline: URP (PC_RPAsset)
- Unity Version: 6000.3.11f1

# Game Mechanics
## Core Gameplay Loop
Zombies spawn and move toward the BaseCore. They stop and attack any Fences, Towers, or the Player in their path.
## Controls and Input Methods
Top-down view, autonomous zombie AI.

# UI
- **HomeBase HP Bar**: A world-space health bar that follows the camera and updates as the base takes damage.

# Key Asset & Context
- **BaseCore.cs**: Script for the central base. HP bar needs robust component caching and visual updates.
- **WorldGatherBar.cs**: Existing bar script with its own image reference and `SetProgress` logic.

# Implementation Steps
1. **Synchronize BaseCore with WorldGatherBar**:
    - **File**: `Assets/Scripts/Interaction/BaseCore.cs`
    - **Change**: In `RefreshWorldHpBar`, prioritize calling `worldGatherBar.SetProgress(fill)` if the component is present. This ensures we use the correct image reference assigned in the prefab.
    - **Change**: In `CacheWorldHpComponents`, add support for legacy `UnityEngine.UI.Text` if `TextMeshProUGUI` is not found, ensuring the HP label shows up.
2. **Fix Image Type Dependency**:
    - **File**: `Assets/Scripts/Interaction/BaseCore.cs`
    - **Change**: In `RefreshWorldHpBar`, if `worldHpFillImage` is found but its type is not `Filled`, log a warning and attempt to set its type to `Filled` (if possible) or find a better image. Actually, just using `worldGatherBar.SetProgress` should solve this if the prefab is correct.
3. **Robust Component Caching**:
    - **File**: `Assets/Scripts/Interaction/BaseCore.cs`
    - **Change**: Refactor `CacheWorldHpComponents` to be even more aggressive in finding the correct fill image, looking for "Fill" in the name AND checking for the `Type.Filled` property.
4. **Visual Debugging**:
    - **File**: `Assets/Scripts/Interaction/BaseCore.cs`
    - **Change**: Add a `Debug.Log` in `RefreshWorldHpBar` to output the calculated fill percentage to the console.

# Verification & Testing
1. **Attack Test**: Spawn a zombie and let it attack the BaseCore.
2. **Log Verification**: Check the console for "BaseCore Damaged" and "HP Bar Refresh" logs with percentages.
3. **UI Verification**: Observe the world-space health bar and verify it reduces and the text updates correctly.
4. **Prefab Check**: Verify the `UI/BaseCoreWorldHpBar` prefab has the fill image correctly assigned to the `WorldGatherBar` component.
