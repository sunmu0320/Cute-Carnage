# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: A single-player/co-op survival defense game where players gather resources (wood, scrap, food), build/repair structures like fences and towers, and survive waves of enemies.
- Players: Single player / Cooperative
- Inspiration / Reference Games: Base defense games
- Tone / Art Direction: Stylized Low-Poly RPG
- Target Platform: StandaloneWindows64
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: URP (Custom PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
The player navigates the map, approaches resource nodes (trees, metal piles, tool piles, barrels), and presses/holds 'E' to gather. Gathering shows a progress bar and triggers animations. Gathered resources are used to build and repair defensive structures, defending against enemy waves.
## Controls and Input Methods
- Movement: WASD/Keyboard
- Interacting/Gathering: E Key (hold to gather)
- Repairing: E Key (when structure is damaged and resources are available)

# UI
The project has a dual-UI design for interaction prompts:
- **Shared UI (Active System):** A single world-space prompt UI object under `WorldUI/RepairWorldPromptUIRoot/RepairWorldPromptUIRoot` with a `WorldCanvas`. The `PlayerInteractor` automatically communicates with this shared `WorldPromptUI` and positions it dynamically above the current targeted interactable.
- **Embedded UI (Redundant System):** Individual resource prefabs have nested `WorldPromptRoot` child GameObjects containing their own `WorldPromptCanvas` and `PromptText` components. Because the shared system is active, these 91 nested canvases in the scene are completely redundant, causing severe scene bloat (103 total canvases, 92 of which are prompt canvases) and emitting warnings in the Console about multiple prompt systems.

We will safely disable the nested `WorldPromptRoot` GameObjects in the 13 resource prefabs, reducing the scene's active canvas count significantly from 103 down to 12.

# Key Asset & Context
The following 13 resource node prefabs contain redundant `WorldPromptRoot` structures:
1. `Assets/Prefabs/Interactables/Resources/Scraps/Barrel1_WUI.prefab`
2. `Assets/Prefabs/Interactables/Resources/Scraps/Barrel2_WUI.prefab`
3. `Assets/Prefabs/Interactables/Resources/Scraps/MetalPile1_WUI.prefab`
4. `Assets/Prefabs/Interactables/Resources/Scraps/MetalPile2_WUI.prefab`
5. `Assets/Prefabs/Interactables/Resources/Scraps/ToolPile1_WUI.prefab`
6. `Assets/Prefabs/Interactables/Resources/Scraps/ToolPile2_Wui.prefab`
7. `Assets/Prefabs/Interactables/Resources/Scraps/ToolPile3_WUI.prefab`
8. `Assets/Prefabs/Interactables/Resources/Woods/NoLeafTree_WUI.prefab`
9. `Assets/Prefabs/Interactables/Resources/Woods/Tree1_WUI.prefab`
10. `Assets/Prefabs/Interactables/Resources/Woods/Tree2_WUI.prefab`
11. `Assets/Prefabs/Interactables/Resources/Woods/Tree3_WUI.prefab`
12. `Assets/Prefabs/Interactables/Resources/Woods/Tree4_WUI.prefab`
13. `Assets/Prefabs/Interactables/Resources/Woods/Tree5_WUI.prefab`

Each of these prefabs contains:
- `WorldPromptRoot` (with `WorldPromptUI` and `SimpleBillboard` components)
  - `WorldPromptCanvas` (with `Canvas`, `CanvasScaler`, and `GraphicRaycaster` components)
    - `PromptText` (with `TextMeshProUGUI` component)

We will change the prefab asset values to set `WorldPromptRoot`'s active state to `false`. This is the safest approach as it acts as a non-destructive disable and allows easy rollback if ever needed.

# Implementation Steps
## Step 1: Create an Editor Script to Disable Redundant WorldPromptRoots
- **Description:** Implement a temporary editor script `Assets/Editor/DisableRedundantResourcePrompts.cs` that can be run from the Unity menu. The script will:
  - Load each of the 13 prefabs using `AssetDatabase.LoadAssetAtPath`.
  - Traverse the prefab to find the `WorldPromptRoot` GameObject.
  - Call `gameObject.SetActive(false)` on it.
  - Use `PrefabUtility.SavePrefabAsset` to save the modified prefab.
  - Log success or failure for each prefab.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** No

## Step 2: Run the Editor Script
- **Description:** Execute the custom menu item in Unity (e.g., `Tools/Cute Carnage/Disable Redundant Resource Prompts`) to apply the changes to the prefabs in a single run.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

## Step 3: Clean up Editor Script
- **Description:** After successful verification, safely delete the temporary editor script `Assets/Editor/DisableRedundantResourcePrompts.cs` to keep the codebase clean.
- **Assigned role:** developer
- **Dependencies:** Step 2 & Verification
- **Parallelizable:** No

# Verification & Testing
1. **Console Check:** Verify that no missing reference errors, null pointers, or other UI-related warnings are logged upon opening the scene or entering Play Mode.
2. **Canvas Count Check:** Run a Read-Only C# Command Script to inspect the current active canvases in the scene.
   - Target Canvas Count: ~12 active canvases (down from 103).
   - Target Prompt Canvas Count: Exactly 1 active canvas (`WorldUI/RepairWorldPromptUIRoot/RepairWorldPromptUIRoot/WorldCanvas`).
3. **Gameplay Verification:**
   - Play the game in Play Mode.
   - Walk the player up to a wood resource node (Tree) and a scrap resource node (Barrel/Metal Pile).
   - Confirm that the interaction prompt ("Press E to Gather") appears correctly.
   - Confirm that holding 'E' triggers the gathering animation, plays the gather progress bar, and awards the resource correctly.
   - Confirm that no multiple prompt system warnings are emitted in the Console during play.
