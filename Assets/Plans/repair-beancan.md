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
   - Currently broken because of misconfigured hierarchy, incorrect layer/colliders, legacy components (`ExamplePromptInteractable`), and redundant UI canvases causing duplication errors.
2. **Detection Logic Script**: `Assets/Scripts/Player/PlayerInteractor.cs`
   - Needs to be safely updated in `ResolveInteractable` to prioritize a `ResourceNode` on parent or self when matching colliders, ensuring child colliders correctly bubble up to the driving `ResourceNode`.

# Implementation Steps
## Step 1: Update Player Interactor Detection Logic
- **Description**: Modify `PlayerInteractor.cs` in `ResolveInteractable` to perform a safe lookup for parent `ResourceNode` components when resolving interactables.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## Step 2: Restructure the BeanCan_WUI Prefab Hierarchy
- **Description**: Load and edit `BeanCan_WUI.prefab` to achieve the following clean, standard structure:
  - **Root Object**: Keep `BeanCan_WUI` as root (pivot at identity, Layer: `Interactable`).
    - Move `ResourceNode` component to root if not already there.
    - Remove blocking / unnecessary components from root.
  - **VisualPivot**: Create an empty child GameObject `VisualPivot` under Root.
    - Move the existing `BeanCan` mesh under `VisualPivot`.
    - Remove the trigger `CapsuleCollider` from the `BeanCan` mesh GameObject so players can walk through it and there are no redundant triggers.
  - **InteractionRange**: Create or clean up the `InteractionRange` child under Root.
    - Add/keep a `SphereCollider` on it. Ensure `Is Trigger` is checked (`true`). Ensure its radius is sufficiently large (around ~3.9).
    - Remove `ExamplePromptInteractable` component from `InteractionRange` to prevent duplicate or non-functional interactions.
  - **UIAnchor_Prompt**: Create or rename an empty child GameObject `UIAnchor_Prompt` under Root at a height of `(0, 0.8, 0)` for the prompt UI.
  - **UIAnchor_GatherBar**: Create or rename an empty child GameObject `UIAnchor_GatherBar` under Root at a height of `(0, 0.5, 0)` for the progress bar UI.
  - **Clean Up duplicates**: Remove the duplicate/standalone `WorldPromptRoot` Canvas since the system uses a shared `WorldPromptUI` managed by the player interactor.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Wire up ResourceNode Serialized Properties
- **Description**: Set up the serialized fields on the root `ResourceNode` component of `BeanCan_WUI`:
  - **UI Anchor**: Assign `UIAnchor_Prompt` (Transform).
  - **Gather Bar Anchor**: Assign `UIAnchor_GatherBar` (Transform).
  - **Interact Name**: `Canned Food`
  - **Resource Type**: `Food` (Index 1)
  - **Amount**: `1`
  - **Can Use**: `true`
  - **One Time Use**: `true`
  - **Gather Animation Type**: `Pickup` (Index 0)
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Prefab Inspection**: Verify the prefab structure and serialization values using C# scripts or Unity inspector.
2. **Console Verification**: Run the scene and ensure no `WorldPromptUI` duplicate warning or `ExamplePromptInteractable` warning is generated.
3. **Gameplay Testing**:
   - Player can walk straight through the `BeanCan_WUI` (no physical collision blocking movement).
   - Approaching `BeanCan_WUI` displays the interaction prompt "Press E to Gather" beautifully aligned at the `UIAnchor_Prompt` position.
   - Pressing and holding `E` triggers the "Pickup" gather animation on the player and displays the `WorldGatherBar` progress bar centered at `UIAnchor_GatherBar`.
   - Completing the gather adds `1 Food` to the player inventory (logged in console and visible in UI) and disables the bean can node (one-time use).
