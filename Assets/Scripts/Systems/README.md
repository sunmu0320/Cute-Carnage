# Day time system (prototype)

## Inspector setup

1. Create an empty GameObject in your scene (e.g. name it `DayTimeManager`).
2. Add the **DayTimeManager** component.
3. Set **Day Duration Seconds** (e.g. `10` for quick tests; default is `300`).
4. Leave **Auto Start On Awake** enabled if the day should begin when the scene loads. Disable it if another system will call **Start Day** later.
5. Optional: on the same object or a child, add **DayTimeTester**.
6. On **DayTimeTester**, assign the **Day Time Manager** reference to the `DayTimeManager` in the scene.

## Play Mode checks

1. Enter Play Mode with **Auto Start On Awake** on and a short duration.
2. Confirm the countdown behaves as expected (values like **Remaining Time Seconds** / **Normalized Time** are available from code; they are not shown on the default Inspector unless you add a small binder or temporary debug log).
3. When time reaches zero, the Console should show **DayTimeTester**’s log: `OnDayEnded — day phase finished.` That message should appear **once** per full countdown until you **Reset** or **Start Day** again.
4. Keyboard (defaults): **R** = `ResetDay`, **P** = `Pause`, **Y** = `Resume`. Confirm pause stops the countdown and resume continues it; confirm reset refills time and allows another end event after the next run to zero.

## Next prototype to build

After this timer is stable, the next useful slice is a **day/night phase manager** (or small **game loop manager**) that subscribes to **`DayTimeManager.OnDayEnded`**, switches a high-level phase enum (e.g. Day → Night), and runs a stub night phase (placeholder UI, dimmed light, or a single “night ended” timer). That gives you one place to hook future systems—combat, economy, saves—without scattering day-end logic across scenes.
