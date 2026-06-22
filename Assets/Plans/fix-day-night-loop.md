# Project Overview
- Game Title: Cute Carnage
- High-Level Concept: A day/night cycle survival prototype where players scavenge during the day and defend against zombies at night.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
- Day: Players explore and scavenge resources (Food, Scrap, Wood). Timer-based.
- Night: Zombies spawn and attack the base. Survival-based (clear all zombies or survive the timer).
- Goal: Survive for 7 days.

## Controls and Input Methods
- WASD for movement (implied by player scripts)
- 'N' key for debug transition to Day (in GameManager)
- 'K' key for debug damage to zombies (in Zombie)

# UI
- HUD displays Player HP, Hunger, Resources, Day count, and Day timer.
- Need to fix HUD to show the correct current day.

# Key Asset & Context
- **GameManager.cs**: Central system for game state and scene transitions.
- **DayTimeManager.cs**: Handles the day countdown.
- **HUDController.cs**: Manages the UI display.
- **SimpleZombieSpawner.cs**: Handles night clearing logic.

# Implementation Steps
## Step 1: Track and Increment Day in GameManager
- **Description**: Add `currentDay` variable to `GameManager.cs`. Increment it when transitioning back to Day. Implement the win condition (Day > 7).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Ensure Day Timer Starts
- **Description**: Update `GameManager.AfterEnterDayScene()` to explicitly call `StartDay()` on the `boundDayTimeManager` to ensure the loop continues correctly even if scene settings differ.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

## Step 3: Update HUD to Display Current Day
- **Description**: Modify `HUDController.cs` to fetch the current day from `GameManager`. Improve robustness by having it find the `DayTimeManager` if it was not assigned in the inspector.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: Yes

# Verification & Testing
- **Test Day 1 to Night 1**: Verify the timer ends and scene transitions.
- **Test Night 1 to Day 2**: Kill all zombies and verify it returns to Day 2.
- **Test HUD Day Display**: Verify HUD says "DAY 2" after returning.
- **Test Day 2 Timer**: Verify the timer starts counting down again.
- **Test Win Condition**: Use debug keys ('N' to skip night) to reach Day 8 and verify the prototype win log appears.
