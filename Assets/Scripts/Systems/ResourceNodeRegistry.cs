using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks depleted ResourceNodes as a delta (PersistentId -> respawnDay); untouched nodes are never
/// stored. Owned by GameManager: nodes report depletion here, and GameManager calls RegrowDue() once per Day
/// start and Capture/RestoreState() for the DayCheckpoint.</summary>
public class ResourceNodeRegistry
{
    public const int MinRespawnDays = 3;
    public const int MaxRespawnDays = 5;

    private readonly Dictionary<string, int> depleted = new Dictionary<string, int>();
    private Dictionary<string, ResourceNode> nodesById;
    private Dictionary<string, Bounds> boundsById;

    public int DepletedCount => depleted.Count;

    /// <summary>Builds the id lookup once. Must run while nodes are still active (GameManager.Start) so their
    /// collider bounds can be captured for the regrow blocking check.</summary>
    public void BuildLookup()
    {
        nodesById = new Dictionary<string, ResourceNode>();
        boundsById = new Dictionary<string, Bounds>();

        ResourceNode[] nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < nodes.Length; i++)
        {
            ResourceNode node = nodes[i];
            string id = node.PersistentNodeId;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (nodesById.ContainsKey(id))
            {
                Debug.LogError($"[ResourceNodeRegistry] Duplicate PersistentId '{id}' on '{node.name}'. Run Tools/Cute Carnage/Assign Resource Node PersistentIds.", node);
                continue;
            }

            nodesById[id] = node;
            boundsById[id] = CalculateSolidBounds(node);
        }
    }

    public void MarkDepleted(ResourceNode node, int currentDay)
    {
        string id = node.PersistentNodeId;
        int respawnDay = currentDay + Random.Range(MinRespawnDays, MaxRespawnDays + 1);
        depleted[id] = respawnDay;
        node.gameObject.SetActive(false);
    }

    /// <summary>Regrows every node whose respawnDay has arrived, unless something (player/structure) now
    /// occupies its spot - those stay depleted and are retried next Day.</summary>
    public void RegrowDue(int currentDay)
    {
        List<string> ready = null;
        foreach (KeyValuePair<string, int> entry in depleted)
        {
            if (entry.Value > currentDay)
            {
                continue;
            }

            if (IsBlocked(entry.Key))
            {
                continue;
            }

            ready ??= new List<string>();
            ready.Add(entry.Key);
        }

        if (ready == null)
        {
            return;
        }

        for (int i = 0; i < ready.Count; i++)
        {
            depleted.Remove(ready[i]);
            SetNodeActive(ready[i], true);
        }

        Debug.Log($"[ResourceNodeRegistry] Day {currentDay}: regrew {ready.Count} node(s), {depleted.Count} still depleted.");
    }

    public Dictionary<string, int> CaptureState()
    {
        return new Dictionary<string, int>(depleted);
    }

    public void RestoreState(Dictionary<string, int> snapshot)
    {
        foreach (string id in depleted.Keys)
        {
            if (!snapshot.ContainsKey(id))
            {
                SetNodeActive(id, true);
            }
        }

        depleted.Clear();
        foreach (KeyValuePair<string, int> entry in snapshot)
        {
            depleted[entry.Key] = entry.Value;
            SetNodeActive(entry.Key, false);
        }
    }

    private void SetNodeActive(string id, bool active)
    {
        if (nodesById != null && nodesById.TryGetValue(id, out ResourceNode node) && node != null)
        {
            node.gameObject.SetActive(active);
        }
    }

    private bool IsBlocked(string id)
    {
        if (boundsById == null || !boundsById.TryGetValue(id, out Bounds bounds))
        {
            return false;
        }

        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit.GetComponentInParent<PlayerMovement>() != null
                || hit.GetComponentInParent<FenceSegment>() != null
                || hit.GetComponentInParent<ArrowTower>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds CalculateSolidBounds(ResourceNode node)
    {
        Collider[] colliders = node.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(node.transform.position, Vector3.one * 0.5f);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger || !colliders[i].enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return bounds;
    }
}
