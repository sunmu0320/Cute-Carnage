# Project Overview
- **Game Title:** Cute Carnage
- **Task:** Link the Tower HP bar to the **Tower Slot** using the existing simple World UI system (camera-facing, progress-based) instead of the previous complex UI system.

# Game Mechanics
- **Slot-Based UI:** Each `TowerSlot` manages its own HP bar instance. This bar remains with the slot but dynamically displays the health of whatever tower is currently installed/upgraded.
- **Unified World UI:** The system will use the same "Face Camera" and "Fill Image" logic found in the Resource and Home Base systems.

# Key Asset & Context
- **Assets/Scripts/Interaction/TowerSlot.cs**: Will be updated to manage a simple World Space HP bar and sync it with the active tower's health.
- **Assets/Scripts/UI/WorldGatherBar.cs**: This existing component will be used or referenced for the "Face Camera" and "SetProgress" functionality.

# Implementation Steps
1. **Modify `TowerSlot.cs`**:
    - Add a `towerHpBarLocalOffset` (Vector3) field to allow adjusting the bar's position relative to the slot.
    - Update the UI reference to use `WorldGatherBar` (or a generic GameObject with a fillable image).
    - In `Update()`, if a tower is present:
        - Calculate the HP ratio: `tower.CurrentHp / tower.MaxHp`.
        - Call `SetProgress()` on the HP bar.
        - Set the bar's visibility based on health state (e.g., hide when full or show always).
    - Update `EnsureTowerHpBarBinding` to apply the `towerHpBarLocalOffset` to the spawned bar's local position.
    - Add logic to `OnValidate` to update the bar's position in the Editor when the offset is changed.
    - Ensure the HP bar correctly follows the Slot's UI anchor and faces the camera.
2. **Clean up `StructureHpAnchorUI.cs`**:
    - This script will no longer be used for towers, maintaining the separation between the old screen-space system and the new world-space system.
3. **Prefab Configuration**:
    - Update the `TowerSlot` prefab to use a simple world-space bar prefab (like `WorldGatherBarRoot`) as its UI template.

# Verification & Testing
1. **Visual Test:** Build a tower and verify the HP bar stays anchored to the slot and faces the camera.
2. **Upgrade Test:** Verify that if the tower is replaced or upgraded, the HP bar continues to show the correct health of the new instance.
3. **Destruction Test:** Verify the HP bar stays with the slot even if the tower visual is destroyed or disabled.
4. **Consistency Test:** Ensure the movement and visibility logic matches the "Resources" world UI.