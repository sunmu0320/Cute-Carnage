# Project Overview
- **Game Title**: Cute Carnage
- **High-Level Concept**: Technical adjustment of the Barn environmental asset to ensure pivot alignment and physics accuracy.
- **Players**: Single player (exploration/interaction).
- **Inspiration**: Standard environmental asset setup in Unity.
- **Tone / Art Direction**: Likely stylized (given the title), requires precise collision for gameplay.
- **Target Platform**: Standalone Windows 64.
- **Render Pipeline**: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
The Barn serves as an interactive or traversable structure. Correct pivot placement allows for easier placement and manipulation, while aligned colliders ensure the player can enter, walk on the floor, and be blocked by walls correctly.
## Controls and Input Methods
Standard character movement. The doorway must be open to allow entry.

# UI
N/A

# Key Asset & Context
- **Barn Root**: `[Barn]` (currently at `(27.40, 6.09, -76.80)`, scale 8).
- **Visual Meshes**: `[Barn]` (body) and `[BarnRoof]`.
- **Colliders**: `WallCollider_Floor`, `WallCollider_Back`, `WallCollider_Left`, `WallCollider_Right`, `WallCollider_Front_Left`, `WallCollider_Front_Right`, `WallCollider_Step`.
- **Trigger**: `InteriorTrigger`.

# Implementation Steps

## 1. Analysis and Preparation
- **Description**: Verify the exact visual floor level and footprint of the Barn mesh using the `MeshRenderer` bounds.
- **Assigned Role**: explorer
- **Dependencies**: None
- **Parallelizable**: No

## 2. Re-Pivot and Re-Parenting
- **Description**:
  1. Create a new empty GameObject named `[Barn]_New` at the visual floor center (approx. `(27.40, 7.84, -76.79)`).
  2. Copy the rotation and parent (`[Zone_South_RuinedVillage]`) from the old root.
  3. Re-parent `[BarnRoof]`, `[Barn]` (mesh), `Colliders`, and `InteriorTrigger` to the new root.
  4. Adjust local transforms to preserve world positions ("without visually moving the building").
  5. Delete the old root and rename `[Barn]_New` to `[Barn]`.
- **Assigned Role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## 3. Collider Realignment
- **Description**:
  1. **Floor**: Adjust `WallCollider_Floor` to match the mesh footprint (`~15.2m x 15.4m`) and set its top surface at the root Y level (`0` local).
  2. **Walls**: Adjust `WallCollider_Back`, `Left`, and `Right` to align with the outer edges of the `[Barn]` mesh renderer bounds.
  3. **Front Walls & Doorway**: Split the front wall into `WallCollider_Front_Left` and `WallCollider_Front_Right`, leaving a gap from `X = 27.4` to `33.1` (aligned with the `WallCollider_Step`).
  4. **Step**: Re-position `WallCollider_Step` to the new doorway floor level.
  5. **Trigger**: Resize `InteriorTrigger` to fit the interior volume (approx. `14m x 8m x 14m`).
  6. **Settings**: Ensure `InteriorTrigger` is a trigger; others are NOT.
- **Assigned Role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## 4. Final Review
- **Description**: Perform a final pass to ensure names are correct, the roof is a separate child, and materials/meshes are untouched.
- **Assigned Role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: Yes

# Verification & Testing
- **Editor Inspection**: Select the Barn in the Scene view and verify the green collider boxes align with the visible walls and floor.
- **Doorway Check**: Ensure no collider blocks the gap between `X=27.4` and `X=33.1`.
- **Pivot Check**: Verify the root transform handle is at the center of the barn floor.
- **Trigger Check**: Verify `InteriorTrigger.isTrigger == true`.
- **Visual Check**: Ensure the building has not shifted its world position.
