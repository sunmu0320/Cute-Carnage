# Project Overview 
- Game Title: Cute Carnage
- High-Level Concept: Top-down survival game where the player gathers resources, builds/repairs structures, and defends against zombies.
- Players: Single player
- Inspiration / Reference Games: Stylized survival and base-defense games.
- Tone / Art Direction: Stylized low-poly 3D models.
- Target Platform: PC (Standalone Windows 64-bit)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics 
## Core Gameplay Loop
Move around, collect resources, avoid/combat zombies, and interact with base structures/houses.
## Controls and Input Methods
- Keyboard: WASD / Arrow keys for movement, Left Shift for sprinting.

# UI
Uses World HUD prompts, HP/hunger meters, and gathering/repairing timers.

# Key Asset & Context
- `Assets/Scripts/Interaction/HouseRoofVisibility.cs`: New script to manage roof visibility.
- `Assets/Prefabs/Buildings/HouseRoot1.prefab`: The house prefab to be modified.

# Implementation Steps

## Step 1: Create HouseRoofVisibility.cs Script
- **Description**: Implement a clean C# MonoBehaviour script that manages roof visibility by tracking player entry and exit in a trigger volume.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Configure the Prefab Hierarchy and Add InteriorTrigger
- **Description**: Edit the `HouseRoot1` prefab to:
  - Create an empty child GameObject named `InteriorTrigger`.
  - Add a `BoxCollider` component to `InteriorTrigger`, and set `Is Trigger` to `true`.
  - Add the `HouseRoofVisibility` component to `InteriorTrigger`.
  - Set the `roofObject` reference on `HouseRoofVisibility` to point to the `Roof1` GameObject.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Size the Interior Trigger Box Collider
- **Description**: Position and scale the `BoxCollider` on `InteriorTrigger` so it precisely covers the floor/inside walk-in area of the house, ensuring it doesn't extend through walls/doorways where the player stands outside.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Enter House**: Play scene `Day.unity`. Run the player inside the house. Verify that the roof (`Roof1`) becomes inactive (hidden) immediately upon crossing the doorway.
2. **Exit House**: Walk the player back outside. Verify that the roof (`Roof1`) becomes active (visible) immediately.
3. **No Collision Interference**: Ensure the player still collides correctly with the solid wall colliders and cannot walk through them.
4. **No Multiple Trigger Count Issues**: Quickly enter and exit the house to confirm the tag/player tracking is robust and does not lead to stuck invisible/visible states.
