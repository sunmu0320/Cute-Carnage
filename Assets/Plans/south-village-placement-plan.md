# Project Overview 
- Game Title: Cute Carnage
- High-Level Concept: Top-down cute post-apocalyptic zombie defense and gathering game.
- Players: Single player
- Inspiration / Reference Games: Cute, low-poly post-apocalyptic survival games.
- Tone / Art Direction: Cute, clean, low-poly post-apocalyptic.
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape 1920x1080
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics 
## Core Gameplay Loop
The player explores the world during the day to gather resources (wood, food, scrap) and build/repair defenses (fences, towers). At night, waves of zombies attack, and the player must defend their base and survive.
## Controls and Input Methods
Standard keyboard and mouse controls (WASD to move, E to interact/gather, mouse to aim and fight). Supports both New Input System and Legacy Input Manager.

# UI
The game displays a custom HUD with resource counts (wood, food, scrap), current time of day, player health, and a progress/gathering bar when interacting with ResourceNodes.

# Key Asset & Context
- **Gatherable Trees (Wood Nodes)**: Prefabs `Assets/Prefabs/Interactables/Resources/Woods/Tree1_WUI.prefab` to `Tree5_WUI.prefab` (and `NoLeafTree_WUI`). These are pre-configured with a `ResourceNode` component to yield `Wood` (with a World Prompt UI, ExamplePromptInteractable, and SimpleShake on mesh).
- **Decorative Rocks/Stones**: Prefabs `Assets/Prefabs/Props/Stone1.prefab` and `Stone2.prefab`. Rocks and stones are NOT gatherable resources. They carry no `ResourceNode` component and are decorative obstacles only.
- **South Village Area**: A rounded zone located in the southern part of the map containing light-colored houses, roads, farm plots, fence boundaries, and a water fountain. Bounded between `x: [-72.7, 59.3]` and `z: [-158.8, -53.2]`. Bounded on the north by fences around `z: -35 to -50` and on the south by a fence line around `z: -158`.

# Implementation Steps

## Step 1: Design Placement Layout
- **Description**: Propose and specify exact coordinates for 32 additional gatherable trees within the South Village Area, ensuring they do not overlap roads, doors, alleys, farms, and paths.
- **Assigned role**: explorer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: User Consultation and Approval
- **Description**: Present the exact coordinates, options, and recommended density to the user via chat, explaining the pros/cons of placement and resource configuration. Wait for confirmation.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Remove previously created Gatherable Stones
- **Description**: Remove/delete the 8 gatherable stone instances previously placed under `Gatherable_Resources_South` to ensure rocks/stones remain completely decorative and non-gatherable.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## Step 4: Placing Additional Gatherable Trees in Scene
- **Description**: Instantiate 32 additional gatherable tree prefabs (`Tree1_WUI` through `Tree5_WUI`) at the approved coordinates as root or child objects in the scene, preserving the 8 existing trees for a total of 40 trees.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 5: Verification & Testing
- **Description**: Playtest the scene, verifying player collision, pathing clearances, UI prompts, gathering animations, and resource increases.
- **Assigned role**: developer
- **Dependencies**: Step 3, Step 4
- **Parallelizable**: No


# Verification & Testing
- **Tree Gathering test**: Move the player to each of the 40 gatherable trees and verify the prompt "Press E to Gather" displays correctly when in range. Ensure gathering completing destroys the node and awards Wood.
- **Stones Decorative check**: Verify that NO stones or rocks in the South Village Area carry `ResourceNode` components, have world gathering prompts, or can be gathered. Ensure they remain purely physical decorative obstacles.
- **Clearance & Navigation test**: Ensure paths, roads, alleys, doors, and farm plots remain completely unobstructed for the player and enemy pathfinding.
- **No Painted Trees check**: Ensure no Terrain Paint Trees are placed in the playable village area.
- **No Prefab/Script modification check**: Confirm that no asset files (prefabs, scripts, terrain, etc.) were modified on disk; only scene-level overrides/instances were added.
