# Implementation Plan - Fix BaseCore HP Bar Visuals (Final Attempt)

## Issues Identified
- HP Bar not updating visually (stays full).
- HP Text not showing up.
- Potential conflict with `WorldGatherBar` component.

## Steps

### 1. Update BaseCore.cs
- Add `private Text legacyWorldHpText;` to support standard UI Text.
- Refactor `CacheWorldHpComponents` to find legacy `Text` and improve Image selection.
- Update `RefreshWorldHpBar` to use `worldGatherBar.SetProgress` if available and update both TMP and standard Text.
- Add `Debug.Log` for visual refresh confirmation.

### 2. Verification
- Attack the base and check console for "HP Bar Refresh: X%" logs.
- Verify bar reduces visually.
