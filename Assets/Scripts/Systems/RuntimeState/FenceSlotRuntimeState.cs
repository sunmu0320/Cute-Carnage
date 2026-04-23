using System;

[Serializable]
public class FenceSlotRuntimeState
{
    public string id;
    public bool hasFence;

    public FenceSlotRuntimeState()
    {
        id = string.Empty;
        hasFence = false;
    }
}
