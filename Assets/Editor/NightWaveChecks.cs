using UnityEditor;
using UnityEngine;

/// <summary>Assert-style checks for NightSchedule's pure math (no test assembly in this project).
/// Run from the menu or via unity CLI; logs PASS or one error per failed check.</summary>
public static class NightWaveChecks
{
    private static int passed;
    private static int failed;

    [MenuItem("Tools/Cute Carnage/Run Night Wave Checks")]
    public static void Run()
    {
        passed = 0;
        failed = 0;

        var waves = new[] { Wave(20f), Wave(40f), Wave(60f) };
        Near("WaveTime 0", NightSchedule.WaveTime(waves, 0), 20f);
        Near("WaveTime 1", NightSchedule.WaveTime(waves, 1), 60f);
        Near("WaveTime 2", NightSchedule.WaveTime(waves, 2), 120f);

        Near("HpMultiplier day 1", NightSchedule.HpMultiplier(0.15f, 1), 1f);
        Near("HpMultiplier day 2", NightSchedule.HpMultiplier(0.15f, 2), 1.15f);
        Near("HpMultiplier day 7", NightSchedule.HpMultiplier(0.15f, 7), 2.313f);

        var stream = new NightSchedule.Stream { intervalStart = 1.5f, intervalEnd = 0.4f };
        Near("StreamInterval 0", NightSchedule.StreamInterval(stream, 0f), 1.5f);
        Near("StreamInterval 1", NightSchedule.StreamInterval(stream, 1f), 0.4f);
        Near("StreamInterval 0.5", NightSchedule.StreamInterval(stream, 0.5f), 0.95f);

        ZombieData a = ScriptableObject.CreateInstance<ZombieData>();
        ZombieData b = ScriptableObject.CreateInstance<ZombieData>();
        stream.zombies = new[]
        {
            new NightSchedule.SpawnEntry { zombie = a, weightStart = 1f, weightEnd = 0f },
            new NightSchedule.SpawnEntry { zombie = b, weightStart = 0f, weightEnd = 1f },
        };
        for (int i = 0; i <= 10; i++)
        {
            float r = i / 10f * 0.999f;
            Check($"Pick at progress 0, r={r:0.00} is A", NightSchedule.PickStreamZombie(stream, 0f, r) == a);
            Check($"Pick at progress 1, r={r:0.00} is B", NightSchedule.PickStreamZombie(stream, 1f, r) == b);
        }

        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);

        if (failed == 0)
            Debug.Log($"[NightWaveChecks] PASS ({passed} checks)");
        else
            Debug.LogError($"[NightWaveChecks] {failed} FAILED, {passed} passed");
    }

    private static NightSchedule.WavePoint Wave(float delay)
    {
        return new NightSchedule.WavePoint { delayFromPrevious = delay };
    }

    private static void Near(string name, float actual, float expected)
    {
        Check($"{name}: expected {expected}, got {actual}", Mathf.Abs(actual - expected) <= 0.001f);
    }

    private static void Check(string name, bool ok)
    {
        if (ok)
        {
            passed++;
            return;
        }

        failed++;
        Debug.LogError($"[NightWaveChecks] FAIL {name}");
    }
}
