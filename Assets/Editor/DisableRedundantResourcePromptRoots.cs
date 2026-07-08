using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DisableRedundantResourcePromptRoots
{
    private static readonly HashSet<string> TargetPrefabNames = new HashSet<string>
    {
        "Barrel1_WUI",
        "Barrel2_WUI",
        "MetalPile1_WUI",
        "MetalPile2_WUI",
        "ToolPile1_WUI",
        "ToolPile2_WUI",
        "ToolPile3_WUI",
        "NoLeafTree_WUI",
        "Tree1_WUI",
        "Tree2_WUI",
        "Tree3_WUI",
        "Tree4_WUI",
        "Tree5_WUI",
    };

    private const string PromptRootName = "WorldPromptRoot";

    [MenuItem("Tools/UI Cleanup/Disable Redundant Resource Prompt Roots")]
    private static void DisableRedundantPromptRoots()
    {
        foreach (string prefabPath in FindTargetPrefabPaths())
        {
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                Transform promptRootTransform = FindChildByName(prefabRoot.transform, PromptRootName);

                if (promptRootTransform == null)
                {
                    Debug.LogWarning($"[ResourcePromptCleanup] Missing '{PromptRootName}' in prefab: {prefabPath}");
                    continue;
                }

                GameObject promptRoot = promptRootTransform.gameObject;

                if (promptRoot.activeSelf)
                {
                    promptRoot.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log($"[ResourcePromptCleanup] Disabled '{promptRoot.name}' in prefab: {prefabPath}");
                }
                else
                {
                    Debug.Log($"[ResourcePromptCleanup] '{promptRoot.name}' already inactive in prefab: {prefabPath}");
                }
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
    }

    [MenuItem("Tools/UI Cleanup/Report Resource Prompt Roots")]
    private static void ReportResourcePromptRoots()
    {
        foreach (string prefabPath in FindTargetPrefabPaths())
        {
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                Transform promptRootTransform = FindChildByName(prefabRoot.transform, PromptRootName);

                if (promptRootTransform == null)
                {
                    Debug.LogWarning($"[ResourcePromptCleanup] Missing '{PromptRootName}' in prefab: {prefabPath}");
                    continue;
                }

                GameObject promptRoot = promptRootTransform.gameObject;
                bool hasCanvasInChildren = promptRoot.GetComponentInChildren<Canvas>(true) != null;

                Debug.Log(
                    $"[ResourcePromptCleanup] Prefab: {prefabPath} | Object: {promptRoot.name} | Active: {promptRoot.activeSelf} | HasCanvasInChildren: {hasCanvasInChildren}");
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
    }

    private static IEnumerable<string> FindTargetPrefabPaths()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

            if (TargetPrefabNames.Contains(prefabName))
            {
                yield return prefabPath;
            }
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == childName);
    }
}
