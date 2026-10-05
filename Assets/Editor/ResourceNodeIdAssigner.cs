using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gives every ResourceNode in the loaded scenes its own scene-instance PersistentId and fixes
/// duplicates (e.g. from Ctrl+D). Existing unique IDs are never changed, so re-running is safe.</summary>
public static class ResourceNodeIdAssigner
{
    private const string MenuPath = "Tools/Cute Carnage/Assign Resource Node PersistentIds";

    [MenuItem(MenuPath)]
    private static void AssignIds()
    {
        int total = 0;
        int added = 0;
        int regenerated = 0;
        int prefabLevel = 0;
        HashSet<string> seen = new HashSet<string>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ResourceNode node in root.GetComponentsInChildren<ResourceNode>(true))
                {
                    total++;
                    PersistentId persistentId = node.GetComponent<PersistentId>();
                    if (persistentId == null)
                    {
                        persistentId = Undo.AddComponent<PersistentId>(node.gameObject);
                        added++;
                        sceneChanged = true;
                    }
                    else if (PrefabUtility.GetCorrespondingObjectFromSource(persistentId) != null)
                    {
                        // Lives on the prefab asset: every instance would share the same ID.
                        prefabLevel++;
                        Debug.LogError($"[ResourceNodeIdAssigner] '{node.name}' gets PersistentId from its prefab asset. Remove it from the prefab and re-run.", node);
                    }

                    if (string.IsNullOrWhiteSpace(persistentId.Id) || !seen.Add(persistentId.Id))
                    {
                        SerializedObject so = new SerializedObject(persistentId);
                        so.FindProperty("persistentId").stringValue = System.Guid.NewGuid().ToString("N");
                        so.ApplyModifiedProperties();
                        seen.Add(persistentId.Id);
                        regenerated++;
                        sceneChanged = true;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Debug.Log($"[ResourceNodeIdAssigner] ResourceNodes={total}, PersistentId added={added}, empty/duplicate IDs regenerated={regenerated}, prefab-level IDs={prefabLevel}. Save the scene to keep changes.");
    }
}
