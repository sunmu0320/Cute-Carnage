# Implementation Plan - Fix BaseCore HP Bar Visuals

## Issues Identified
1. **Ambiguous Image Caching**: `BaseCore` searches for the fill image but might pick the background or a non-filled image.
2. **Sync with WorldGatherBar**: The prefab has a `WorldGatherBar` component which has its own reliable reference to the fill image, but `BaseCore` doesn't use it.
3. **Missing HP Text**: The HP label is not showing up, possibly due to using standard `Text` instead of `TextMeshProUGUI` in the prefab.
4. **Fast Destruction**: The console logs show the base being destroyed extremely quickly (seconds), making it hard to see the transition if the refresh isn't perfect.

## Steps

### 1. Update BaseCore.cs
- Use `WorldGatherBar.SetProgress` if available for reliable image updating.
- Add fallback for legacy `UnityEngine.UI.Text` in component caching.
- Add debug logging for fill percentage.
- Ensure the bar instance is active during refresh.

### 2. Verify WorldGatherBar behavior
- Ensure `SetProgress` in `WorldGatherBar` is functioning as expected.

## Files to Modify
- `Assets/Scripts/Interaction/BaseCore.cs`
