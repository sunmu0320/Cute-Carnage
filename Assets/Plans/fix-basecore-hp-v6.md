# Project Overview
- Game Title: Top-Down Survival/Base-Defense
- High-Level Concept: Zombies attack a base, player defends using towers and fences.
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
Zombies spawn and move toward the BaseCore. They stop and attack any Fences, Towers, or the Player in their path. The BaseCore must be protected.

# UI
- **HomeBase HP Bar**: A world-space health bar that follows the camera and updates as the base takes damage.

# Key Asset & Context
- `BaseCore.cs`: Central script for the base. Manages HP and the world-space HP bar instance.
- `WorldGatherBar.cs`: A utility script often attached to HP bar prefabs that handles the fill amount and camera facing.

# Implementation Steps
1. **Improve BaseCore Component Caching**:
    - Support both `TextMeshProUGUI` and standard `UnityEngine.UI.Text` for the HP label.
    - Specifically check for the `WorldGatherBar` component and use its `SetProgress` method to update the fill bar, as this is the most reliable way to target the correct image assigned in the prefab.
2. **Refine HP Bar Update Logic**:
    - Ensure `RefreshWorldHpBar` is called immediately after damage.
    - Use `WorldGatherBar.SetProgress(fill)` as the primary update path.
    - Fallback to manual `fillAmount` update only if `WorldGatherBar` is missing.
3. **Robust Visibility Control**:
    - Ensure the `worldHpBarInstance` is active when the base is damaged, even if it was previously hidden.
4. **Debug Visuals**:
    - Add a `Debug.Log` in `RefreshWorldHpBar` to verify the calculated fill percentage is correct.

# Verification & Testing
1. **Attack Test**: Let zombies attack the base and verify the HP bar fill reduces.
2. **Label Test**: Verify the "Base HP X / Y" text updates correctly.
3. **Log Check**: Observe the console for damage logs and HP bar refresh logs to confirm internal state matches visuals.
