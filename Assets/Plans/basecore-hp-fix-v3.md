# Implementation Plan - BaseCore HP Visuals Final

## Steps

### 1. Modify `BaseCore.cs`
- Add `private UnityEngine.UI.Text legacyWorldHpText;` field.
- Update `CacheWorldHpComponents` to support legacy `Text` and improve Image selection logic.
- Update `RefreshWorldHpBar` to:
    - Prioritize `worldGatherBar.SetProgress(fill)`.
    - Support both TMP and standard Text.
    - Add detailed debug logging to verify the values being set.

### 2. Verify Fix
- Observe the console and the in-game UI.
