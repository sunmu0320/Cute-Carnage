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

    public bool HasResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        InitializeIfNeeded();
        return resources[type] >= amount;
    }

    public bool TrySpendResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();

        if (!HasResource(type, amount))
        {
            return false;
        }

        resources[type] -= amount;
        Debug.Log($"Spent {amount} {type}. New total: {resources[type]}");
        return true;
    }

    public void RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (TrySpendResource(type, amount))
        {
            return;
        }

        int available = GetAmount(type);
        if (available <= 0)
        {
            return;
        }

        TrySpendResource(type, available);
    }

    public bool HasEnough(ResourceType type, int amount)
    {
        return HasResource(type, amount);
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
