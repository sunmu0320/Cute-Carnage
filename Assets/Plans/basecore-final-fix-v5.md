# BaseCore HP Fix - Detailed Implementation

1. Update `BaseCore` class fields to include `legacyWorldHpText`.
2. Update `CacheWorldHpComponents` to reliably find images and both types of text components.
3. Update `RefreshWorldHpBar` to:
    - Use `worldGatherBar.SetProgress(fill)` as the primary update method.
    - Set `fillAmount` on `worldHpFillImage` as a backup.
    - Update TMP or legacy Text with formatted HP string.
    - Add `Debug.Log` showing the refresh percentage.
4. Update `LateUpdate` to ensure rotation happens even if `WorldGatherBar` is disabled.
