# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: Top-down survival base defense game where the player defends a central base against zombies while exploring surrounding zones for resources.
- Players: Single player
- Inspiration / Reference Games: Project Zomboid, Vampire Survivors, various base defense games.
- Tone / Art Direction: 3D Top-down, currently using a graybox/placeholder style for map planning.
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP (Universal Render Pipeline)

# Game Mechanics
## Core Gameplay Loop
- Exploration: Move from the central Home Base to surrounding zones (Forest, Mall, Factory, Village).
- Scavenging: Collect resources (Wood, Food, Scrap) - *Note: Actual resource placement is excluded from this plan.*
- Defense: Return to Home Base to defend against zombie waves using fences and towers.
- Progression: Expand and upgrade the base using gathered resources.

## Controls and Input Methods
- Top-down movement (keyboard/gamepad).
- Mouse/Controller for targeting/interaction.

# UI
- HUD: Existing HUD handles health, resources, and wave info.
- Minimap (Future): The blockout will help define the boundaries for a minimap.

# Key Asset & Context
- **Map_Blockout (GameObject)**: The root container for all new elements.
- **Materials (Assets)**:
  - `Mat_Graybox_Forest`: Green (URP Lit)
  - `Mat_Graybox_Mall`: Light Gray (URP Lit)
  - `Mat_Graybox_Factory`: Blue-Gray (URP Lit)
  - `Mat_Graybox_Village`: Yellow-Brown (URP Lit)
  - `Mat_Graybox_Road`: Dark Gray (URP Lit)
  - `Mat_Graybox_Boundary`: Transparent Red or Wireframe (URP Lit)
- **HomeBase Position**: `(-2.19, 2.49, 28.04)`

# Implementation Steps
## Step 1: Material Creation
- **Description**: Create simple URP Lit materials in `Assets/Art/Materials/Graybox/` to visually distinguish zones and roads.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Hierarchy Setup
- **Description**: Create the root `Map_Blockout` GameObject at `(0, 0, 0)` and the sub-groups: `Ground`, `Roads`, `Zone_North_Forest`, `Zone_West_MallStore`, `Zone_East_RuinedFactory`, `Zone_South_RuinedVillage`, `Boundary`, `Landmarks`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 3: Road Placement
- **Description**: Place four wide road planes (Dark Gray) originating from `HomeBase` towards North, South, East, and West.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 4: Zone Blockout - South (Ruined Village)
- **Description**: 
  - Location: Close to base, South (Negative Z).
  - Content: Small house footprints (Cubes), broken walls (thin Cubes), and open yards (Planes).
  - Material: `Mat_Graybox_Village`.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 5: Zone Blockout - North (Forest)
- **Description**: 
  - Location: Far, North (Positive Z).
  - Content: Scattered tree placeholders (Cylinders/Cones) and rocks (Cubes).
  - Material: `Mat_Graybox_Forest`.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 6: Zone Blockout - West (Mall / Store)
- **Description**: 
  - Location: Far, West (Negative X).
  - Content: Large building footprint (low walls), parking lot lines (Quads), and internal aisle blocks.
  - Material: `Mat_Graybox_Mall`.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 7: Zone Blockout - East (Ruined Factory)
- **Description**: 
  - Location: Far, East (Positive X).
  - Content: Cluttered layout with containers (Cubes), tanks (Cylinders), and pipes.
  - Material: `Mat_Graybox_Factory`.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

## Step 8: Boundary and Final Organization
- **Description**: Add a large boundary marker (wireframe or colored edges) around the entire playable area. Ensure all objects are correctly nested.
- **Assigned role**: developer
- **Dependencies**: Steps 4-7
- **Parallelizable**: No

# Verification & Testing
- **Visual Check**: Use `CaptureMultiAngleSceneView` to verify the layout matches the `MapSketch.png` reference and user requirements.
- **Hierarchy Check**: Verify all new objects are under `Map_Blockout`.
- **Scale Check**: Ensure roads are wide enough for the player and zones are appropriately spaced.
- **Integrity Check**: Confirm that `HomeBase`, `Player`, and other existing objects were NOT modified.
