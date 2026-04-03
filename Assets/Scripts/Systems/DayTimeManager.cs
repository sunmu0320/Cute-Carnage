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

    private void Awake()
    {
        totalDuration = SafeDuration(dayDurationSeconds);
        currentTime = dayDurationSeconds;
        hasDayEnded = false;
        isPaused = false;
        isCountingDown = autoStartOnAwake;
    }

    private void Update()
    {
        if (!isCountingDown || isPaused || hasDayEnded)
        {
            return;
        }

        currentTime -= Time.deltaTime;
        currentTime = Mathf.Max(0f, currentTime);

        if (currentTime <= 0f && !hasDayEnded)
        {
            hasDayEnded = true;
            OnDayEnded?.Invoke();
        }
    }

    public void StartDay()
    {
        totalDuration = SafeDuration(dayDurationSeconds);
        currentTime = dayDurationSeconds;
        hasDayEnded = false;
        isPaused = false;
        isCountingDown = true;
    }

    public void ResetDay()
    {
        totalDuration = SafeDuration(dayDurationSeconds);
        currentTime = dayDurationSeconds;
        hasDayEnded = false;
        isPaused = false;
        isCountingDown = true;
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

    private static float SafeDuration(float seconds)
    {
        return seconds <= 0f ? Mathf.Epsilon : seconds;
    }
}
