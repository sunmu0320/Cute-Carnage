using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentSlotIdValidator
{
    private const string MenuPath = "Tools/Cute Carnage/Validate Persistent Slot IDs";

    private sealed class SlotInfo
    {
        public GameObject GameObject;
        public string Id;
        public string HierarchyPath;
        public string SlotType;
    }

    [MenuItem(MenuPath)]
    private static void ValidateLoadedScenes()
    {
        int towerSlotCount = 0;
        int fenceSlotCount = 0;
        int missingIdCount = 0;
        int duplicateGroupCount = 0;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            List<SlotInfo> slots = CollectSlots(
                scene,
                ref towerSlotCount,
                ref fenceSlotCount);

            Dictionary<string, List<SlotInfo>> slotsById =
                new Dictionary<string, List<SlotInfo>>(StringComparer.Ordinal);

            for (int i = 0; i < slots.Count; i++)
            {
                SlotInfo slot = slots[i];
                if (string.IsNullOrWhiteSpace(slot.Id))
                {
                    missingIdCount++;
                    Debug.LogError(
                        $"[Persistent Slot ID Validator] Scene '{GetSceneName(scene)}': missing or whitespace-only ID. " +
                        $"Slot type: {slot.SlotType}. Path: '{slot.HierarchyPath}'.",
                        slot.GameObject);
                    continue;
                }

                if (!slotsById.TryGetValue(slot.Id, out List<SlotInfo> matchingSlots))
                {
                    matchingSlots = new List<SlotInfo>();
                    slotsById.Add(slot.Id, matchingSlots);
                }

                matchingSlots.Add(slot);
            }

            foreach (KeyValuePair<string, List<SlotInfo>> pair in slotsById)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                duplicateGroupCount++;
                string affectedSlots = BuildAffectedSlotsText(pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    SlotInfo slot = pair.Value[i];
                    Debug.LogError(
                        $"[Persistent Slot ID Validator] Scene '{GetSceneName(scene)}': duplicate ID '{pair.Key}'. " +
                        $"Affected slots: {affectedSlots}",
                        slot.GameObject);
                }
            }
        }

        string counts =
            $"TowerSlots checked: {towerSlotCount}; FenceSlots checked: {fenceSlotCount}; " +
            $"Missing IDs: {missingIdCount}; Duplicate ID groups: {duplicateGroupCount}.";

        if (missingIdCount == 0 && duplicateGroupCount == 0)
        {
            Debug.Log($"[Persistent Slot ID Validator] Validation passed. {counts}");
        }
        else
        {
            Debug.LogWarning($"[Persistent Slot ID Validator] Validation finished with problems. {counts}");
        }
    }

    private static List<SlotInfo> CollectSlots(
        Scene scene,
        ref int towerSlotCount,
        ref int fenceSlotCount)
    {
        List<SlotInfo> slots = new List<SlotInfo>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            TowerSlot[] towerSlots = roots[rootIndex].GetComponentsInChildren<TowerSlot>(true);
            for (int i = 0; i < towerSlots.Length; i++)
            {
                TowerSlot slot = towerSlots[i];
                towerSlotCount++;
                slots.Add(CreateSlotInfo(slot.gameObject, slot.PersistentSlotId, nameof(TowerSlot)));
            }

            FenceSlot[] fenceSlots = roots[rootIndex].GetComponentsInChildren<FenceSlot>(true);
            for (int i = 0; i < fenceSlots.Length; i++)
            {
                FenceSlot slot = fenceSlots[i];
                fenceSlotCount++;
                slots.Add(CreateSlotInfo(slot.gameObject, slot.PersistentSlotId, nameof(FenceSlot)));
            }
        }

        return slots;
    }

    private static SlotInfo CreateSlotInfo(GameObject gameObject, string id, string slotType)
    {
        return new SlotInfo
        {
            GameObject = gameObject,
            Id = id,
            HierarchyPath = GetHierarchyPath(gameObject.transform),
            SlotType = slotType
        };
    }

    private static string BuildAffectedSlotsText(List<SlotInfo> slots)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < slots.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            builder.Append(slots[i].SlotType);
            builder.Append(" at '");
            builder.Append(slots[i].HierarchyPath);
            builder.Append('\'');
        }

        return builder.ToString();
    }

    private static string GetHierarchyPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path;
    }

    private static string GetSceneName(Scene scene)
    {
        return string.IsNullOrEmpty(scene.name) ? "<Untitled>" : scene.name;
    }
}
