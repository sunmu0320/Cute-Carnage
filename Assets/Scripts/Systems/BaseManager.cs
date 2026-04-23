using System.Collections.Generic;
using UnityEngine;

public class BaseManager : MonoBehaviour
{
    private FenceSlot[] fenceSlots;

    private void Awake()
    {
        fenceSlots = FindObjectsByType<FenceSlot>(FindObjectsSortMode.None);
    }

    public BaseRuntimeState CaptureState()
    {
        BaseRuntimeState state = new BaseRuntimeState();
        state.fenceSlots.Clear();

        FenceSlot[] slots = fenceSlots != null && fenceSlots.Length > 0
            ? fenceSlots
            : FindObjectsByType<FenceSlot>(FindObjectsSortMode.None);

        for (int i = 0; i < slots.Length; i++)
        {
            FenceSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            FenceSlotRuntimeState slotState = slot.CreateRuntimeState();
            if (string.IsNullOrWhiteSpace(slotState.id))
            {
                continue;
            }

            state.fenceSlots.Add(slotState);
        }

        return state;
    }

    public void ApplyState(BaseRuntimeState state)
    {
        if (state == null || state.fenceSlots == null || state.fenceSlots.Count == 0)
        {
            return;
        }

        FenceSlot[] slots = FindObjectsByType<FenceSlot>(FindObjectsSortMode.None);
        Dictionary<string, FenceSlotRuntimeState> stateById = new Dictionary<string, FenceSlotRuntimeState>();
        for (int i = 0; i < state.fenceSlots.Count; i++)
        {
            FenceSlotRuntimeState slotState = state.fenceSlots[i];
            if (slotState == null || string.IsNullOrWhiteSpace(slotState.id) || stateById.ContainsKey(slotState.id))
            {
                continue;
            }

            stateById.Add(slotState.id, slotState);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            FenceSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            string id = slot.PersistentSlotId;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!stateById.TryGetValue(id, out FenceSlotRuntimeState slotState))
            {
                continue;
            }

            slot.ApplyRuntimeState(slotState);
        }
    }
}
