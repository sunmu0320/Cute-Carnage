using System.Collections.Generic;
using UnityEngine;

public class BaseManager : MonoBehaviour
{
    public BaseRuntimeState CaptureState()
    {
        BaseRuntimeState state = new BaseRuntimeState();
        state.fenceSlots.Clear();
        state.towerSlots.Clear();

        FenceSlot[] fenceSceneSlots = FindObjectsByType<FenceSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < fenceSceneSlots.Length; i++)
        {
            FenceSlot slot = fenceSceneSlots[i];
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

        TowerSlot[] towerSceneSlots = FindObjectsByType<TowerSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < towerSceneSlots.Length; i++)
        {
            TowerSlot slot = towerSceneSlots[i];
            if (slot == null)
            {
                continue;
            }

            TowerSlotRuntimeState slotState = slot.CreateRuntimeState();
            if (string.IsNullOrWhiteSpace(slotState.id))
            {
                continue;
            }

            state.towerSlots.Add(slotState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[BaseManager] Capture tower slot id='{slotState.id}' hasTower={slotState.hasTower} towerId='{slotState.towerId}' level={slotState.level} " +
                $"currentHp={slotState.currentHp:0.##} isDestroyed={slotState.isDestroyed}",
                this);
#endif
        }

        return state;
    }

    public void ApplyState(BaseRuntimeState state)
    {
        if (state == null)
        {
            return;
        }

        bool hasFenceData = state.fenceSlots != null && state.fenceSlots.Count > 0;
        bool hasTowerData = state.towerSlots != null && state.towerSlots.Count > 0;
        if (!hasFenceData && !hasTowerData)
        {
            return;
        }

        if (hasFenceData)
        {
            ApplyFenceSlots(state);
        }

        if (hasTowerData)
        {
            ApplyTowerSlots(state);
        }
    }

    private void ApplyFenceSlots(BaseRuntimeState state)
    {
        FenceSlot[] slots = FindObjectsByType<FenceSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

    private void ApplyTowerSlots(BaseRuntimeState state)
    {
        int incomingCount = state.towerSlots.Count;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BaseManager] Apply tower slots: incomingTowerSlotStates={incomingCount}", this);
#endif
        Dictionary<string, TowerSlotRuntimeState> stateById = new Dictionary<string, TowerSlotRuntimeState>();
        for (int i = 0; i < state.towerSlots.Count; i++)
        {
            TowerSlotRuntimeState slotState = state.towerSlots[i];
            if (slotState == null || string.IsNullOrWhiteSpace(slotState.id) || stateById.ContainsKey(slotState.id))
            {
                continue;
            }

            stateById.Add(slotState.id, slotState);
        }

        TowerSlot[] slots = FindObjectsByType<TowerSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int matchedCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            TowerSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            string id = slot.PersistentId;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!stateById.TryGetValue(id, out TowerSlotRuntimeState slotState))
            {
                continue;
            }

            matchedCount++;
            bool alreadyHadTower = slot.HasTower;
            slot.ApplyRuntimeState(slotState);
            bool skippedDuplicate = alreadyHadTower && slotState.hasTower;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[BaseManager] Apply tower incomingCount={incomingCount} matchedSoFar={matchedCount} slot id='{id}' " +
                $"incomingHasTower={slotState.hasTower} restoredTowerId='{slotState.towerId}' incomingHp={slotState.currentHp:0.##} " +
                $"incomingDestroyed={slotState.isDestroyed} skippedDuplicate={skippedDuplicate}",
                this);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[BaseManager] Apply tower summary incomingTowerSlots={incomingCount} matchedTowerSlots={matchedCount}",
            this);
#endif
    }
}
