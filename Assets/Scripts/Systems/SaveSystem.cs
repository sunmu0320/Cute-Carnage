using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Single-slot autosave. GameManager writes the DayCheckpoint here at the start of every Day;
/// MainMenu's Continue sets LoadOnNextStart so GameManager.Start() restores it instead of starting fresh.
/// Fences/Towers/ResourceNodes are keyed by PersistentId - changing those IDs orphans saved entries.</summary>
public static class SaveSystem
{
    public const int Version = 1;

    /// <summary>Set by MainMenu before loading the game scene; consumed once by GameManager.Start().</summary>
    public static bool LoadOnNextStart;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool HasSave => TryRead(out _);

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    public static void Write(SaveData data)
    {
        data.version = Version;
        // Write-then-replace so a crash mid-write never leaves a truncated save.
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.Move(tmp, FilePath);
    }

    public static bool TryRead(out SaveData data)
    {
        data = null;
        if (!File.Exists(FilePath)) return false;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Unreadable save ignored: {e.Message}");
            return false;
        }

        return data != null && data.version == Version && data.day >= 1;
    }
}

[Serializable]
public class SaveData
{
    public int version;
    public int day;
    public float baseCoreHp;
    public int wood, scrap, food;
    public float playerHp, playerHunger;
    public List<FenceSave> fences = new List<FenceSave>();
    public List<TowerSave> towers = new List<TowerSave>();
    public List<NodeSave> depletedNodes = new List<NodeSave>();
}

[Serializable] public class FenceSave { public string id; public bool hasFence; public int tier; public float hp; }
[Serializable] public class TowerSave { public string id; public bool hasTower; public float hp; }
[Serializable] public class NodeSave { public string id; public int respawnDay; }
