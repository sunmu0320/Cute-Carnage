#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>Playtest recorder (editor/dev builds only, toggled from Tools/Cute Carnage/Perf Log While Playing).
/// Writes one CSV row per real-time second to Logs/ (frame times, hitches, zombie count, memory) and a short
/// summary file when Play stops, so the session can be reviewed from the summary alone. Per-frame Update is a
/// deliberate rule-6 exception: it's a debug recorder, not UI/state polling.</summary>
public class PerfLogger : MonoBehaviour
{
    private const string PrefKey = "CuteCarnage.PerfLog";
    private const float HitchMs = 50f;
    private const float WarmupSeconds = 3f;

    private readonly List<float> allFrames = new List<float>(100000);
    private readonly List<KeyValuePair<float, string>> worstSeconds = new List<KeyValuePair<float, string>>();
    private StreamWriter csv;
    private string summaryPath;
    private ProfilerRecorder gcAlloc;
    private float secondStart;
    private float startedAt;
    private int frames, over33, over50;
    private float sumMs, maxMs;
    private long gcBytes;
    private long memStart, memPeak;
    private int hitches;

    public static bool Enabled
    {
#if UNITY_EDITOR
        get => UnityEditor.EditorPrefs.GetBool(PrefKey, false);
        set => UnityEditor.EditorPrefs.SetBool(PrefKey, value);
#else
        get => true;
        set { }
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled)
            return;

        var go = new GameObject("[PerfLogger]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<PerfLogger>();
    }

    private void Awake()
    {
        string dir = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        csv = new StreamWriter(Path.Combine(dir, $"PerfLog_{stamp}.csv"));
        csv.WriteLine("time_s,scene,phase,day,zombies,frames,avg_ms,max_ms,frames_over_33ms,frames_over_50ms,gc_alloc_kb,mono_used_mb,total_alloc_mb");
        summaryPath = Path.Combine(dir, $"PerfLog_{stamp}_summary.txt");
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        memStart = Profiler.GetTotalAllocatedMemoryLong();
        startedAt = secondStart = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        // Skip the scene-load frames right after Play starts.
        if (Time.realtimeSinceStartup < startedAt + WarmupSeconds)
        {
            secondStart = Time.realtimeSinceStartup;
            return;
        }

        float ms = Time.unscaledDeltaTime * 1000f;
        allFrames.Add(ms);
        frames++;
        sumMs += ms;
        maxMs = Mathf.Max(maxMs, ms);
        if (ms > 33.3f) over33++;
        if (ms > HitchMs) { over50++; hitches++; }
        if (gcAlloc.Valid) gcBytes += gcAlloc.LastValue;

        float now = Time.realtimeSinceStartup;
        if (now - secondStart >= 1f)
            FlushSecond(now);
    }

    private void FlushSecond(float now)
    {
        long total = Profiler.GetTotalAllocatedMemoryLong();
        memPeak = System.Math.Max(memPeak, total);
        GameManager gm = GameManager.Instance;
        string phase = gm != null ? gm.CurrentPhase.ToString() : "-";
        int day = gm != null ? gm.CurrentDay : 0;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string row = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0},{1},{2},{3},{4},{5},{6:0.0},{7:0.0},{8},{9},{10:0},{11:0.0},{12:0.0}",
            now, scene, phase, day, Zombie.AliveZombieCount, frames, sumMs / Mathf.Max(1, frames), maxMs, over33, over50,
            gcBytes / 1024f, Profiler.GetMonoUsedSizeLong() / 1048576f, total / 1048576f);
        csv.WriteLine(row);

        // Keep the 5 seconds with the worst single frame for the summary.
        worstSeconds.Add(new KeyValuePair<float, string>(maxMs, row));
        worstSeconds.Sort((a, b) => b.Key.CompareTo(a.Key));
        if (worstSeconds.Count > 5) worstSeconds.RemoveAt(5);

        secondStart = now;
        frames = over33 = over50 = 0;
        sumMs = maxMs = 0f;
        gcBytes = 0;
    }

    private void OnDestroy()
    {
        if (csv == null)
            return;

        csv.Dispose();
        gcAlloc.Dispose();
        if (allFrames.Count == 0)
            return;

        var sorted = new List<float>(allFrames);
        sorted.Sort();
        float sum = 0f;
        foreach (float f in sorted) sum += f;
        long memEnd = Profiler.GetTotalAllocatedMemoryLong();
        var sb = new StringBuilder();
        sb.AppendLine($"frames {sorted.Count}, play time {sum / 1000f:0}s");
        sb.AppendLine($"frame ms  avg {sum / sorted.Count:0.0}  p50 {Pct(sorted, 0.5f):0.0}  p95 {Pct(sorted, 0.95f):0.0}  p99 {Pct(sorted, 0.99f):0.0}  max {sorted[sorted.Count - 1]:0.0}");
        sb.AppendLine($"hitches over {HitchMs}ms: {hitches}");
        sb.AppendLine($"memory (total allocated) MB  start {memStart / 1048576f:0}  peak {System.Math.Max(memPeak, memEnd) / 1048576f:0}  end {memEnd / 1048576f:0}");
        sb.AppendLine("worst seconds (max frame | time,scene,phase,day,zombies,frames,avg,max,>33,>50,gcKB,monoMB,totalMB):");
        foreach (KeyValuePair<float, string> w in worstSeconds) sb.AppendLine($"  {w.Key,6:0.0} ms | {w.Value}");
        File.WriteAllText(summaryPath, sb.ToString());
    }

    private static float Pct(List<float> sorted, float p)
    {
        return sorted[Mathf.Clamp((int)(sorted.Count * p), 0, sorted.Count - 1)];
    }

#if UNITY_EDITOR
    private const string MenuPath = "Tools/Cute Carnage/Perf Log While Playing";

    [UnityEditor.MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
    }

    [UnityEditor.MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        UnityEditor.Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
#endif
}
#endif
