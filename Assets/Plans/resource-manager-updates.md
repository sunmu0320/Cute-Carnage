# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: Survival/Base Defense game where players collect resources during the day and defend against waves at night.
- Players: Single player
- Target Platform: Standalone Windows 64
- Render Pipeline: PC_RPAsset (likely URP or Custom)
- Input System: Both New and Legacy

# Game Mechanics
## Core Gameplay Loop
- Day: Explore, collect Wood, Scrap, and Food. Build/Upgrade base defenses.
- Night: Survive waves of enemies by defending the base.
- Resources: Wood, Scrap, and Food are used for construction and survival.

# UI
- Inspector-based adjustments for testing.

# Key Asset & Context
- `ResourceManager.cs`: Manages the inventory of resources.
- `ResourceType.cs`: Enum defining Wood, Food, and Scrap.
- `ResourceRuntimeState.cs`: Serializable state for resources used for scene transitions.

# Implementation Steps
1. **Modify `ResourceManager.cs`**:
    - Add serialized fields for `startingWood`, `startingScrap`, and `startingFood`.
    - Update `InitializeIfNeeded` to populate the `resources` dictionary using these starting values instead of hardcoded 0.
2. **Verification**:
    - Open the `Day` scene in the Unity Editor.
    - Select the `ResourceManager` object.
    - Verify that the new fields appear in the Inspector.
    - Enter non-zero values (e.g., Wood: 100, Scrap: 50, Food: 20).
    - Enter Play Mode and check if the resources start with these values.

# Verification & Testing
- **Manual Test**:
    1. Set Wood to 10 in the Inspector of the `ResourceManager` in the `Day` scene.
    2. Start Play Mode.
    3. Verify (via UI or Console logs) that Wood is 10.
- **Scene Transition Test**:
    1. Set Wood to 50 in `Day` scene.
    2. Start Play Mode.
    3. Transition to `Night` scene (e.g., via `DayTimeManager` timeout or `GameManager` debug key).
    4. Verify that Wood remains 50 in the `Night` scene (carried over by `GameManager`).
