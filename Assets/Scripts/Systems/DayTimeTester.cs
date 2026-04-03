using UnityEngine;

public class DayTimeTester : MonoBehaviour
{
    [SerializeField]
    private DayTimeManager dayTimeManager;

    [Header("Optional keys (Play Mode)")]
    [SerializeField]
    private KeyCode resetKey = KeyCode.R;

    [SerializeField]
    private KeyCode pauseKey = KeyCode.P;

    [SerializeField]
    private KeyCode resumeKey = KeyCode.Y;

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

    private void HandleDayEnded()
    {
        Debug.Log("[DayTimeTester] OnDayEnded — day phase finished.");
    }
}
