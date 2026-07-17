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
- `Assets/Scripts/Player/PlayerMovement.cs`: Needs to be modified to use Rigidbody physics (`rb.MovePosition` inside `FixedUpdate`) instead of `transform.position += ...` inside `Update`. Rotation must be calculated smoothly using `transform.rotation` in `Update()` without conflicting manual constraints.
- `Assets/Prefabs/PlayerRoot.prefab`: Requires Rigidbody updates (non-kinematic, constraints locked on X/Z rotation, gravity enabled, continuous collision detection).

# Implementation Steps

## Step 1: Refactor PlayerMovement.cs
- **Description**: Replace the direct transform teleportation inside `Move()` with physics-compliant `Rigidbody.MovePosition` calls in `FixedUpdate()`. Rotate the player smoothly toward the input direction using `transform.rotation` Slerping in `Update()` to ensure maximum visual smoothness without stutter. Remove redundant yaw locking codes which fight with physics.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Validate Player Prefab Physics Configuration
- **Description**: Confirm and set `PlayerRoot` prefab settings:
  - `Rigidbody.isKinematic = false`
  - `Rigidbody.useGravity = true`
  - `Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous`
  - `Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ`
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Verify and Adjust Character Visual Feet Level
- **Description**: Test grounding on terrain and against solid barriers. Adjust local position of `CharacterVisual` child if minor visual vertical offset is needed to keep feet touching the exact terrain surface.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: Yes

# Verification & Testing
1. **Movement Controls**: Play scene `Day.unity`. Move around to confirm input controls, smooth acceleration/deceleration blending, and sprint multiplier feel exactly like the original.
2. **Wall Collision**: Walk directly into the wall of `House_1` (or any other building). The player must slide against the surface rather than clipping through.
3. **Slope/Terrain Gravity**: Walk up and down hills/slopes on the terrain. Player feet must remain grounded rather than sinking or floating.
4. **Doorway Entrance**: Confirm the player can still walk into open doorways while being blocked by actual wall elements.
