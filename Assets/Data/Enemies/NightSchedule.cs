using System;
using UnityEngine;

/// <summary>Per-night wave plan read by SimpleZombieSpawner (night = GameManager.CurrentDay; days past the
/// last entry reuse the last night). Each wave's main body spawns evenly all around the base; optional
/// hordes are small packs that arrive from one compass direction partway through the wave. A wave is
/// cleared only after its main body and every horde have fully spawned and all zombies are dead.</summary>
[CreateAssetMenu(fileName = "NightSchedule", menuName = "Cute Carnage/Night Schedule")]
public class NightSchedule : ScriptableObject
{
    /// <summary>Map zones: North = forest, East = factory, South = village, West = mall. North is +Z.</summary>
    public enum Compass { North, East, South, West }

    [Serializable]
    public class Horde
    {
        [Min(1)] public int count = 5;
        public Compass from = Compass.North;
        [Range(10f, 180f), Tooltip("Spread of the pack, in degrees.")] public float arcWidth = 40f;
        [Min(0f), Tooltip("Seconds after the wave starts.")] public float startDelay = 4f;
        [Min(0.01f)] public float spawnInterval = 0.4f;
    }

    [Serializable]
    public class Wave
    {
        [Min(0), Tooltip("Main body, spawned evenly all around the base.")] public int zombieCount = 5;
        [Min(0.01f)] public float spawnInterval = 1f;
        public Horde[] hordes = new Horde[0];

        public int TotalCount
        {
            get
            {
                int total = zombieCount;
                foreach (Horde h in hordes) total += h.count;
                return total;
            }
        }
    }

    [Serializable]
    public class Night
    {
        public Wave[] waves = new Wave[0];
    }

    [SerializeField] private Night[] nights = new Night[0];

    public int NightCount => nights.Length;

    public Night GetNight(int day)
    {
        if (nights.Length == 0) return null;
        return nights[Mathf.Clamp(day - 1, 0, nights.Length - 1)];
    }

    public static Vector3 CompassDirection(Compass c)
    {
        switch (c)
        {
            case Compass.East: return Vector3.right;
            case Compass.South: return Vector3.back;
            case Compass.West: return Vector3.left;
            default: return Vector3.forward;
        }
    }
}
