using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BasePersistentIdValidator
{
    private static readonly string[] BaseCoreNameCandidates = { "BaseCore", "Base Core", "Core", "HomeBase", "Base" };

    public static void ValidateActiveScene(GameObject explicitBaseCore = null, Object logContext = null)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        List<GameObject> requiredObjects = CollectBaseObjectsInScene(activeScene, explicitBaseCore);
        if (requiredObjects.Count == 0)
        {
            Debug.LogWarning("[BasePersistentIdValidator] No base-related objects found for validation in active scene.", logContext);
            return;
        }

        HashSet<string> seenIds = new HashSet<string>();
        Dictionary<string, GameObject> firstById = new Dictionary<string, GameObject>();

        for (int i = 0; i < requiredObjects.Count; i++)
        {
            GameObject obj = requiredObjects[i];
            if (obj == null)
            {
                continue;
            }

            PersistentId persistentId = obj.GetComponent<PersistentId>();
            if (persistentId == null)
            {
                Debug.LogError(
                    $"[BasePersistentIdValidator] Missing PersistentId on base object '{GetHierarchyPath(obj.transform)}'.",
                    obj);
                continue;
            }

            if (string.IsNullOrWhiteSpace(persistentId.Id))
            {
                Debug.LogError(
                    $"[BasePersistentIdValidator] Empty PersistentId on base object '{GetHierarchyPath(obj.transform)}'.",
                    obj);
                continue;
            }

            if (!seenIds.Add(persistentId.Id))
            {
                GameObject first = firstById[persistentId.Id];
                Debug.LogError(
                    $"[BasePersistentIdValidator] Duplicate PersistentId '{persistentId.Id}' found on '{GetHierarchyPath(obj.transform)}' and '{GetHierarchyPath(first.transform)}'.",
                    obj);
                continue;
            }

            firstById[persistentId.Id] = obj;
        }

        Debug.Log($"[BasePersistentIdValidator] Validated {requiredObjects.Count} base objects in scene '{activeScene.name}'.", logContext);
    }

    private static List<GameObject> CollectBaseObjectsInScene(Scene scene, GameObject explicitBaseCore)
    {
        List<GameObject> result = new List<GameObject>();
        HashSet<GameObject> unique = new HashSet<GameObject>();

        AddIfInScene(explicitBaseCore, scene, result, unique);

        FenceSlot[] fenceSlots = Object.FindObjectsByType<FenceSlot>(FindObjectsSortMode.None);
        for (int i = 0; i < fenceSlots.Length; i++)
        {
            if (fenceSlots[i] != null)
            {
                AddIfInScene(fenceSlots[i].gameObject, scene, result, unique);
            }
        }

        TowerSlot[] towerSlots = Object.FindObjectsByType<TowerSlot>(FindObjectsSortMode.None);
        for (int i = 0; i < towerSlots.Length; i++)
        {
            if (towerSlots[i] != null)
            {
                AddIfInScene(towerSlots[i].gameObject, scene, result, unique);
            }
        }

        GameObject coreFromHomeBase = FindBaseCoreFromHomeBaseRoot(scene);
        AddIfInScene(coreFromHomeBase, scene, result, unique);

        GameObject namedCore = FindBaseCoreByName(scene);
        AddIfInScene(namedCore, scene, result, unique);

        return result;
    }

    private static GameObject FindBaseCoreFromHomeBaseRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != "HomeBase")
            {
                continue;
            }

            PersistentId rootPersistentId = root.GetComponent<PersistentId>();
            if (rootPersistentId != null)
            {
                return root;
            }

            PersistentId[] childPersistentIds = root.GetComponentsInChildren<PersistentId>(true);
            if (childPersistentIds.Length > 0 && childPersistentIds[0] != null)
            {
                return childPersistentIds[0].gameObject;
            }
        }

        return null;
    }

    private static GameObject FindBaseCoreByName(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindByNameRecursive(roots[i].transform);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindByNameRecursive(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < BaseCoreNameCandidates.Length; i++)
        {
            if (root.name == BaseCoreNameCandidates[i])
            {
                return root.gameObject;
            }
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindByNameRecursive(root.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void AddIfInScene(GameObject obj, Scene scene, List<GameObject> result, HashSet<GameObject> unique)
    {
        if (obj == null || obj.scene != scene || !unique.Add(obj))
        {
            return;
        }

        result.Add(obj);
    }

    private static string GetHierarchyPath(Transform current)
    {
        if (current == null)
        {
            return "<null>";
        }

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path;
    }
}
