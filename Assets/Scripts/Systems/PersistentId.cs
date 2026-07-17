using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentId : MonoBehaviour
{
    [SerializeField]
    private string persistentId;

    public string Id => persistentId;

    private void Reset()
    {
        TryAssignIdInEditor();
    }

    private void OnValidate()
    {
        TryAssignIdInEditor();
    }

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            Debug.LogWarning($"[PersistentId] '{name}' has an empty ID. Assign one in the editor.", this);
        }
    }

    private void TryAssignIdInEditor()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(persistentId))
        {
            return;
        }

        persistentId = Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
