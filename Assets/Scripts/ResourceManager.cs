using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    private void Awake()
    {
        InitializeIfNeeded();
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InitializeIfNeeded();

        resources[type] += amount;
        Debug.Log($"Added {amount} {type}. New total: {resources[type]}");
    }

    public void RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InitializeIfNeeded();

        int current = resources[type];
        int removed = Math.Min(current, amount);

        if (removed <= 0)
        {
            return;
        }

        resources[type] = Math.Max(0, current - amount);
        Debug.Log($"Removed {removed} {type}. New total: {resources[type]}");
    }

    public bool HasEnough(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        InitializeIfNeeded();
        return resources[type] >= amount;
    }

    public int GetAmount(ResourceType type)
    {
        InitializeIfNeeded();
        return resources[type];
    }

    private void InitializeIfNeeded()
    {
        if (resources.Count > 0)
        {
            return;
        }

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }
}
