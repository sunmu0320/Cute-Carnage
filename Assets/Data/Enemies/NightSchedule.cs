using System;
using UnityEngine;

/// <summary>Per-night plan read by SimpleZombieSpawner (night = GameManager.CurrentDay; days past the last
/// entry reuse the last night). Each night is a continuous stream (all around, near to off-screen, interval
/// shrinking as the night goes on) plus wave points. Wave time = running sum of delayFromPrevious, so wave 1
/// starts delayFromPrevious seconds after nightfall and every later wave that many seconds after the previous
/// one started — never waiting for kills. The night ends only once every wave group has fully spawned and
/// every zombie is dead.</summary>
[CreateAssetMenu(fileName = "NightSchedule", menuName = "Cute Carnage/Night Schedule")]
public class NightSchedule : ScriptableObject
{
    /// <summary>Map zones: North = forest, East = factory, South = village, West = mall. North is +Z.</summary>
    public enum Compass { North, East, South, West }

    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Empty = the spawner's default prefab with its own data.")] public ZombieData zombie;
        [Min(0f), Tooltip("Pick weight at nightfall.")] public float weightStart = 1f;
        [Min(0f), Tooltip("Pick weight at the last wave.")] public float weightEnd = 1f;
    }

    [Serializable]
    public class Stream
    {
        public SpawnEntry[] zombies = new SpawnEntry[0];
        [Min(0.05f), Tooltip("Seconds between stream spawns at nightfall.")] public float intervalStart = 1.5f;
        [Min(0.05f), Tooltip("Seconds between stream spawns at the last wave.")] public float intervalEnd = 0.4f;
    }

    [Serializable]
    public class Group
    {
        [Tooltip("Empty = the spawner's default prefab with its own data.")] public ZombieData zombie;
        [Min(1)] public int count = 20;
        [Tooltip("Spawn all around instead of from one compass direction.")] public bool allAround;
        public Compass from = Compass.North;
        [Range(10f, 180f), Tooltip("Spread of the pack, in degrees.")] public float arcWidth = 40f;
        [Min(0f), Tooltip("Seconds after the wave starts.")] public float startDelay;
        [Min(0.01f)] public float spawnInterval = 0.2f;
    }

    [Serializable]
    public class WavePoint
    {
        [Min(0f), Tooltip("Seconds after the previous wave started (the first wave: after nightfall).")]
        public float delayFromPrevious = 60f;
        public Group[] groups = new Group[0];

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (Group g in groups) total += g.count;
                return total;
            }
        }
    }

    [Serializable]
    public class Night
    {
        public Stream stream = new Stream();
        public WavePoint[] waves = new WavePoint[0];
    }

    [SerializeField] private Night[] nights = new Night[0];
    [SerializeField, Min(0f), Tooltip("Zombie max HP grows by this fraction per day, compounding (0.15 = +15%/day).")]
    private float hpGrowthPerDay = 0.15f;

    public int NightCount => nights.Length;
    public float HpGrowthPerDay => hpGrowthPerDay;

    public Night GetNight(int day)
    {
        if (nights.Length == 0) return null;
        return nights[Mathf.Clamp(day - 1, 0, nights.Length - 1)];
    }

    /// <summary>Seconds after nightfall at which wave <paramref name="index"/> starts.</summary>
    public static float WaveTime(WavePoint[] waves, int index)
    {
        float t = 0f;
        for (int i = 0; i <= index && i < waves.Length; i++) t += waves[i].delayFromPrevious;
        return t;
    }

    public static float HpMultiplier(float growthPerDay, int day)
    {
        return Mathf.Pow(1f + growthPerDay, Mathf.Max(0, day - 1));
    }

    public static float StreamInterval(Stream s, float progress01)
    {
        return Mathf.Lerp(s.intervalStart, s.intervalEnd, Mathf.Clamp01(progress01));
    }

    /// <summary>Weighted pick with each entry's weight lerped by night progress. Null when there are no
    /// entries or every weight is zero (the spawner then uses its default prefab).</summary>
    public static ZombieData PickStreamZombie(Stream s, float progress01, float random01)
    {
        if (s.zombies == null || s.zombies.Length == 0) return null;

        float p = Mathf.Clamp01(progress01);
        float total = 0f;
        foreach (SpawnEntry e in s.zombies) total += Mathf.Lerp(e.weightStart, e.weightEnd, p);
        if (total <= 0f) return null;

        float target = Mathf.Clamp01(random01) * total;
        foreach (SpawnEntry e in s.zombies)
        {
            float w = Mathf.Lerp(e.weightStart, e.weightEnd, p);
            if (w > 0f && target < w) return e.zombie;
            target -= w;
        }

        // random01 == 1 lands past the end: last entry with weight.
        for (int i = s.zombies.Length - 1; i >= 0; i--)
        {
            if (Mathf.Lerp(s.zombies[i].weightStart, s.zombies[i].weightEnd, p) > 0f) return s.zombies[i].zombie;
        }

        return null;
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
