using System;

[Serializable]
public class FenceSlotRuntimeState
{
    /// <summary>Stable slot id (matches slot PersistentId).</summary>
    public string id;

    public bool hasFence;

    /// <summary>HP of the placed fence when hasFence is true; otherwise unused.</summary>
    public float currentHp;

    /// <summary>True when the fence is broken (HP at 0); persisted across scene loads.</summary>
    public bool isDestroyed;

    public FenceSlotRuntimeState()
    {
        id = string.Empty;
        hasFence = false;
        currentHp = 0f;
        isDestroyed = false;
    }
}
