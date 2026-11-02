using UnityEngine;

public class DayTimeTester : MonoBehaviour
{
    [SerializeField]
    private DayTimeManager dayTimeManager;

    [Header("Optional keys (Play Mode)")]
    [SerializeField]
    private KeyCode resetKey = KeyCode.Alpha0;

    [SerializeField]
    private KeyCode pauseKey = KeyCode.P;

    [SerializeField]
    private KeyCode resumeKey = KeyCode.Y;

    [SerializeField, Tooltip("Free T1 fence/tower (full HP) in every empty unlocked slot. Testing only.")]
    private KeyCode buildAllKey = KeyCode.B;

    private void OnEnable()
    {
        if (dayTimeManager == null)
        {
            return;
        }

        dayTimeManager.OnDayEnded += HandleDayEnded;
    }

    private void OnDisable()
    {
        if (dayTimeManager == null)
        {
            return;
        }

        dayTimeManager.OnDayEnded -= HandleDayEnded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(buildAllKey))
        {
            BuildAllEmptySlots();
        }

        if (dayTimeManager == null)
        {
            return;
        }

        if (Input.GetKeyDown(resetKey))
        {
            dayTimeManager.ResetDay();
            Debug.Log("[DayTimeTester] ResetDay()");
        }

        if (Input.GetKeyDown(pauseKey))
        {
            dayTimeManager.Pause();
            Debug.Log("[DayTimeTester] Pause()");
        }

        if (Input.GetKeyDown(resumeKey))
        {
            dayTimeManager.Resume();
            Debug.Log("[DayTimeTester] Resume()");
        }
    }

    /// <summary>Uses the checkpoint-restore paths, so no resources are spent. Upgraded slots are left alone.</summary>
    private static void BuildAllEmptySlots()
    {
        int fences = 0, towers = 0;
        foreach (FenceSlot slot in FindObjectsByType<FenceSlot>(FindObjectsSortMode.None))
        {
            if (slot.CanInstallFence())
            {
                slot.RestoreFenceInternal(slot.FenceDataForTier(1), float.MaxValue);
                fences++;
            }
        }

        foreach (TowerSlot slot in FindObjectsByType<TowerSlot>(FindObjectsSortMode.None))
        {
            if (!slot.HasTower)
            {
                slot.RestoreTowerInternal(true, float.MaxValue);
                towers++;
            }
        }

        Debug.Log($"[DayTimeTester] Build all: {fences} fences, {towers} towers installed (T1).");
    }

    private void HandleDayEnded()
    {
        Debug.Log("[DayTimeTester] OnDayEnded — day phase finished.");
    }
}
