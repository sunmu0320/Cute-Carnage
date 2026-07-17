# Project Overview
- **Game Title**: Cute Carnage
- **High-Level Concept**: A survival defense game where players collect resources, build/repair defense fortifications, and survive waves of incoming enemies in a charmingly cute cartoon style.
- **Players**: Single-player vs AI (Zombies).
- **Inspiration / Reference Games**: Minecraft, Don't Starve, Project Zomboid.
- **Tone / Art Direction**: Cute, clean, cartoonish but with survival elements ("Cute Carnage").
- **Target Platform**: PC (StandaloneWindows64).
- **Screen Orientation / Resolution**: Landscape 1920x1080.
- **Render Pipeline**: Custom/URP PC_RPAsset.

# Game Mechanics
## Core Gameplay Loop
The player explores the environment to find resource nodes (Food, Wood, Scrap). They collect these resources over time (using interactive gather timing and animation), use them to build or repair defense barricades (Fences), and survive waves of enemies targeting their base.

## Controls and Input Methods
- **Keyboard & Mouse**: WASD or arrow keys for movement, E key for interaction (e.g., gathering resources, entering repair mode, interacting with structures).

# UI
- **World Prompt UI**: Floating contextual UI overlays above interactable objects showing interaction options (e.g., "Press E to Gather").
- **World Gather Bar**: Progress bar shown above resource nodes when holding interaction keys or gathering, tracking remaining gather duration.

# Key Asset & Context
1. **Target Prefab**: `Assets/Prefabs/Interactables/Resources/Food/BeanCan_WUI.prefab`
   - Needs child trigger collider renamed to `PromptRange` with radius `2.5`.
2. **ResourceNode Script**: `Assets/Scripts/Interaction/ResourceNode.cs`
   - Needs a `gatherDistance` serialized float field (default `1.0f`).
3. **PlayerInteractor Script**: `Assets/Scripts/Player/PlayerInteractor.cs`
   - Needs distance verification check against `resourceNode.GatherDistance` before initiating gathering in `HandleInteractInput`.

# Implementation Steps
## Step 1: Add gatherDistance to ResourceNode.cs
- **Description**: Add a serialized field `gatherDistance` with a default value of `1.0f` and public getter `GatherDistance` to `ResourceNode.cs`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## Step 2: Implement Distance Validation in PlayerInteractor.cs
- **Description**: Update `PlayerInteractor.cs` in `HandleInteractInput` to verify whether the horizontal distance (ignoring Y) between the player and the `ResourceNode` is within `resourceNode.GatherDistance`. If not, log/ignore the interact input while keeping the UI prompt visible.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Restructure BeanCan_WUI Prefab
- **Description**: Rename child `InteractionRange` to `PromptRange` and set its `SphereCollider` radius to `2.5f`. Ensure `Is Trigger` is checked. Ensure `ResourceNode` properties on root are preserved/correct.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
1. **Compilation Check**: Verify both scripts compile cleanly.
2. **Prefab Inspection**: Verify `PromptRange` has radius `2.5` and `ResourceNode` has `gatherDistance` set to `1.0` in the prefab.
3. **Gameplay Validation**:
   - Approaching `BeanCan_WUI`: UI Prompt should appear when the player is farther away (inside the 2.5 + player's radius overlap).
   - Pressing `E` while far away: Nothing happens (or debug message prints that the player is too far).
   - Moving closer (under 1.0 unit): Pressing `E` initiates the gather sequence successfully.
   - Walking through the object: The player can still freely walk through the can.
