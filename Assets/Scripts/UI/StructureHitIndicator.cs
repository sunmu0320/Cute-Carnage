using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Red arcs on a ring around the player pointing at structures (fence, tower, base core) that are
/// being hit, on screen or off. One arc per structure; every hit holds it for holdSeconds, then it fades out.
/// Arcs are re-aimed in LateUpdate only while any is shown — display-only rule-6 exception (the camera moves
/// every frame); the component disables itself when nothing is shown.</summary>
public class StructureHitIndicator : MonoBehaviour
{
    [Tooltip("Inactive template: a centered Image whose sprite is an arc pointing up.")]
    [SerializeField] private RectTransform arcTemplate;
    [SerializeField, Min(0.1f)] private float holdSeconds = 2f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.5f;

    private class Entry
    {
        public RectTransform arc;
        public Image image;
        public float hideAt;
    }

    private readonly Dictionary<Component, Entry> entries = new Dictionary<Component, Entry>();
    private readonly Stack<Entry> spare = new Stack<Entry>();
    private readonly List<Component> expired = new List<Component>();
    private Camera cam;
    private Transform player;
    private RectTransform ring;
    private float baseAlpha = 1f;

    private void Awake()
    {
        ring = (RectTransform)transform;
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.transform : null;
        if (arcTemplate != null)
        {
            Image image = arcTemplate.GetComponent<Image>();
            if (image != null)
                baseAlpha = image.color.a;
        }

        // Subscribed for the component's whole life; enabled only gates LateUpdate (and Start never runs once
        // Awake disables it, so subscribe here).
        StructureDamageEvents.Damaged += HandleDamaged;
        enabled = false;
    }

    private void OnDestroy()
    {
        StructureDamageEvents.Damaged -= HandleDamaged;
    }

    private void HandleDamaged(Component structure)
    {
        if (structure == null || arcTemplate == null || !TryGetScreenDirection(structure.transform.position, out _))
            return;

        if (!entries.TryGetValue(structure, out Entry e))
        {
            e = spare.Count > 0 ? spare.Pop() : NewEntry();
            entries[structure] = e;
        }

        e.hideAt = Time.time + holdSeconds;
        if (!e.arc.gameObject.activeSelf)
            e.arc.gameObject.SetActive(true);
        Aim(e, structure);
        enabled = true;
    }

    private Entry NewEntry()
    {
        RectTransform arc = Instantiate(arcTemplate, arcTemplate.parent);
        return new Entry { arc = arc, image = arc.GetComponent<Image>() };
    }

    private void LateUpdate()
    {
        if (cam != null && player != null)
        {
            // Ring follows the player (the top-down camera keeps them below the screen center).
            Vector3 vp = cam.WorldToViewportPoint(player.position);
            Vector2 size = ((RectTransform)ring.parent).rect.size;
            ring.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        }

        expired.Clear();
        foreach (KeyValuePair<Component, Entry> kv in entries)
        {
            if (kv.Key == null || Time.time >= kv.Value.hideAt || !Aim(kv.Value, kv.Key))
                expired.Add(kv.Key);
        }

        foreach (Component key in expired)
        {
            Entry e = entries[key];
            e.arc.gameObject.SetActive(false);
            spare.Push(e);
            entries.Remove(key);
        }

        if (entries.Count == 0)
            enabled = false;
    }

    /// <summary>Rotates the arc toward the structure and fades it near the end; false when it has no direction.</summary>
    private bool Aim(Entry e, Component structure)
    {
        if (!TryGetScreenDirection(structure.transform.position, out Vector2 dir))
            return false;

        e.arc.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        if (e.image != null)
        {
            Color c = e.image.color;
            c.a = baseAlpha * Mathf.Clamp01((e.hideAt - Time.time) / fadeSeconds);
            e.image.color = c;
        }

        return true;
    }

    /// <summary>Screen-space direction from the player (screen center without one), on screen or off.</summary>
    private bool TryGetScreenDirection(Vector3 worldPoint, out Vector2 dir)
    {
        dir = Vector2.zero;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 vp = cam.WorldToViewportPoint(worldPoint);
        Vector2 origin = player != null ? (Vector2)cam.WorldToViewportPoint(player.position) : new Vector2(0.5f, 0.5f);
        dir = new Vector2((vp.x - origin.x) * cam.pixelWidth, (vp.y - origin.y) * cam.pixelHeight);
        if (vp.z < 0f)
            dir = -dir;
        return dir.sqrMagnitude > 0.0001f;
    }
}
