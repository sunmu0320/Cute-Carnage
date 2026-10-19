using System;
using UnityEngine;

public class DayTimeManager : MonoBehaviour
{
    [Header("Day Time Settings")]
    [Tooltip("Total length of the daytime exploration phase, in seconds.")]
    [SerializeField]
    private float dayDurationSeconds = 300f;

    [Tooltip("If true, the day timer starts automatically when the scene loads.")]
    [SerializeField]
    private bool autoStartOnAwake = true;

    private float currentTime;
    private float totalDuration;
    private bool hasDayEnded;
    private bool isPaused;
    private bool isCountingDown;
    private int lastWholeSecond = -1;

    public float RemainingTimeSeconds => Mathf.Max(0f, currentTime);

    public float NormalizedTime
    {
        get
        {
            float duration = totalDuration <= 0f ? Mathf.Epsilon : totalDuration;
            if (duration <= Mathf.Epsilon)
            {
                return hasDayEnded ? 1f : 0f;
            }

            if (hasDayEnded)
            {
                return 1f;
            }

            return 1f - Mathf.Clamp01(currentTime / duration);
        }
    }

    public bool HasDayEnded => hasDayEnded;

    public bool IsPaused => isPaused;

    public event Action OnDayEnded;

    /// <summary>Fires when the displayed whole second (ceil of remaining time) changes, and on reset.</summary>
    public event Action<int> OnSecondChanged;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TEMP-DEBUG][DayTimeManager] Awake id={GetInstanceID()} scene='{gameObject.scene.name}' autoStartOnAwake={autoStartOnAwake} duration={dayDurationSeconds}", this);
#endif
        ResetTimerState(autoStartOnAwake);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TEMP-DEBUG][DayTimeManager] OnDestroy id={GetInstanceID()}", this);
#endif
    }

    private void Update()
    {
        if (!isCountingDown || isPaused || hasDayEnded)
        {
            return;
        }

        currentTime -= Time.deltaTime;
        currentTime = Mathf.Max(0f, currentTime);
        NotifySecondIfChanged();

        if (currentTime <= 0f && !hasDayEnded)
        {
            hasDayEnded = true;
            OnDayEnded?.Invoke();
        }
    }

    public void StartDay()
    {
        ResetTimerState(true);
    }

    public void ResetDay()
    {
        ResetTimerState(true);
    }

    private void ResetTimerState(bool shouldCountDown)
    {
        totalDuration = SafeDuration(dayDurationSeconds);
        currentTime = totalDuration;
        hasDayEnded = false;
        isPaused = false;
        isCountingDown = shouldCountDown;
        lastWholeSecond = -1;
        NotifySecondIfChanged();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TEMP-DEBUG][DayTimeManager] ResetTimerState id={GetInstanceID()} shouldCountDown={shouldCountDown} totalDuration={totalDuration}", this);
#endif
    }

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        if (hasDayEnded)
        {
            return;
        }

        isPaused = false;
    }

    private void NotifySecondIfChanged()
    {
        int second = Mathf.CeilToInt(currentTime);
        if (second == lastWholeSecond)
        {
            return;
        }

        lastWholeSecond = second;
        OnSecondChanged?.Invoke(second);
    }

    private static float SafeDuration(float seconds)
    {
        return seconds <= 0f ? Mathf.Epsilon : seconds;
    }
}
