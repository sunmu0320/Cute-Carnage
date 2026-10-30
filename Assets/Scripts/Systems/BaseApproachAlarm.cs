using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Trigger ring around the home base. When alarmThreshold zombies of one wave have entered it, raises
/// HordeApproaching once for that wave and shows a screen-edge "!" toward where they came in. Stream zombies
/// are ignored. The marker goes away when the spawner reports that wave mostly cleared, after maxShowSeconds,
/// or on any phase change (new night, Retry, GameOver). Keep it on Ignore Raycast with a kinematic Rigidbody,
/// like HomeFogZone.</summary>
[RequireComponent(typeof(Collider))]
public class BaseApproachAlarm : MonoBehaviour
{
    [SerializeField, Min(1)] private int alarmThreshold = 5;
    [SerializeField, Min(0.5f)] private float maxShowSeconds = 10f;
    [SerializeField, Tooltip("Empty = the indicator template's own sprite.")] private Sprite alarmIcon;
    [SerializeField] private Color alarmColor = new Color(1f, 0.82f, 0.2f, 1f);

    /// <summary>(wave index, average entry point)</summary>
    public event Action<int, Vector3> HordeApproaching;

    private class WaveTally
    {
        public int count;
        public Vector3 sum;
        public bool fired;
        public int handle;
    }

    private readonly Dictionary<int, WaveTally> tallies = new Dictionary<int, WaveTally>();
    private SimpleZombieSpawner spawner;

    private void Start()
    {
        spawner = FindFirstObjectByType<SimpleZombieSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
            spawner.OnWaveMostlyCleared += HandleWaveMostlyCleared;
        if (GameManager.Instance != null)
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.OnWaveMostlyCleared -= HandleWaveMostlyCleared;
        if (GameManager.Instance != null)
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        Zombie zombie = other.GetComponentInParent<Zombie>();
        if (zombie == null || zombie.WaveIndex < 0 || zombie.IsDead)
            return;

        if (!tallies.TryGetValue(zombie.WaveIndex, out WaveTally t))
        {
            t = new WaveTally();
            tallies[zombie.WaveIndex] = t;
        }

        if (t.fired)
            return;

        t.count++;
        t.sum += zombie.transform.position;
        if (t.count < alarmThreshold)
            return;

        t.fired = true;
        Vector3 point = t.sum / t.count;
        HordeApproaching?.Invoke(zombie.WaveIndex, point);
        if (EdgeIndicatorUI.Instance != null)
            t.handle = EdgeIndicatorUI.Instance.Show(point, alarmIcon, alarmColor, maxShowSeconds);
    }

    private void HandleWaveMostlyCleared(int waveIndex)
    {
        if (tallies.TryGetValue(waveIndex, out WaveTally t) && t.fired && EdgeIndicatorUI.Instance != null)
            EdgeIndicatorUI.Instance.Hide(t.handle);
    }

    private void HandlePhaseChanged(GameManager.GamePhase _)
    {
        foreach (WaveTally t in tallies.Values)
        {
            if (t.fired && EdgeIndicatorUI.Instance != null)
                EdgeIndicatorUI.Instance.Hide(t.handle);
        }

        tallies.Clear();
    }
}
