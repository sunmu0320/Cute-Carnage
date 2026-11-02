using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Direction markers for world points (wave alarm). Each Show returns a handle; off screen the marker
/// is pinned to the screen edge, on screen it sits on a ring around the player toward the point, and it
/// disappears on Hide or when its time runs out. Position is re-pinned in LateUpdate while anything is shown — a deliberate
/// rule-6 exception (display only, the camera moves every frame); the component disables itself when empty.</summary>
public class EdgeIndicatorUI : MonoBehaviour
{
    public static EdgeIndicatorUI Instance { get; private set; }

    [Tooltip("Inactive template (Image, optionally with a child label); one copy per shown marker.")]
    [SerializeField] private RectTransform template;
    [SerializeField] private float edgeMargin = 40f;
    [Tooltip("On screen: marker sits this far (canvas units) from the player toward the point, or over the point if closer.")]
    [SerializeField, Min(0f)] private float onScreenRingRadius = 160f;
    [Tooltip("Top-center box (width, height) kept clear for the Day/Night dial; markers there drop below it.")]
    [SerializeField] private Vector2 topCenterKeepOut = new Vector2(340f, 300f);

    private class Entry
    {
        public RectTransform marker;
        public Vector3 point;
        public float hideAt;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private readonly Stack<RectTransform> spare = new Stack<RectTransform>();
    private readonly List<int> expired = new List<int>();
    private RectTransform area;
    private Camera cam;
    private Transform player;
    private int nextHandle = 1;

    private void Awake()
    {
        Instance = this;
        area = (RectTransform)transform;
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.transform : null;
        enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>seconds <= 0 keeps it until Hide.</summary>
    public int Show(Vector3 worldPoint, Sprite icon, Color color, float seconds)
    {
        RectTransform marker = spare.Count > 0 ? spare.Pop() : Instantiate(template, area);
        Image image = marker.GetComponent<Image>();
        if (image != null)
        {
            if (icon != null)
                image.sprite = icon;
            image.color = color;
        }

        int handle = nextHandle++;
        entries[handle] = new Entry
        {
            marker = marker,
            point = worldPoint,
            hideAt = seconds > 0f ? Time.time + seconds : float.MaxValue,
        };
        marker.gameObject.SetActive(true);
        enabled = true;
        Pin(entries[handle]);
        return handle;
    }

    /// <summary>Moves the marker's world point and/or pushes its timeout (seconds <= 0 leaves the timeout).</summary>
    public void Refresh(int handle, Vector3 worldPoint, float seconds)
    {
        if (!entries.TryGetValue(handle, out Entry e))
            return;

        e.point = worldPoint;
        if (seconds > 0f)
            e.hideAt = Time.time + seconds;
    }

    public bool IsShown(int handle) => entries.ContainsKey(handle);

    public void Hide(int handle)
    {
        if (!entries.TryGetValue(handle, out Entry e))
            return;

        e.marker.gameObject.SetActive(false);
        spare.Push(e.marker);
        entries.Remove(handle);
        if (entries.Count == 0)
            enabled = false;
    }

    public void HideAll()
    {
        expired.Clear();
        expired.AddRange(entries.Keys);
        foreach (int h in expired)
            Hide(h);
    }

    private void LateUpdate()
    {
        expired.Clear();
        foreach (KeyValuePair<int, Entry> kv in entries)
        {
            if (Time.time >= kv.Value.hideAt)
                expired.Add(kv.Key);
            else
                Pin(kv.Value);
        }

        foreach (int h in expired)
            Hide(h);
    }

    /// <summary>Off screen: pinned to the screen edge toward the point. On screen: on a ring around the player
    /// toward the point (over the point when it is inside the ring).</summary>
    private void Pin(Entry e)
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        Vector3 vp = cam.WorldToViewportPoint(e.point);
        bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        Vector2 size = area.rect.size;
        Vector2 offset = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        if (onScreen)
        {
            Vector2 origin = Vector2.zero;
            if (player != null)
            {
                Vector3 pvp = cam.WorldToViewportPoint(player.position);
                origin = new Vector2((pvp.x - 0.5f) * size.x, (pvp.y - 0.5f) * size.y);
            }

            Vector2 toPoint = offset - origin;
            e.marker.anchoredPosition = toPoint.sqrMagnitude > onScreenRingRadius * onScreenRingRadius
                ? origin + toPoint.normalized * onScreenRingRadius
                : offset;
            return;
        }

        if (vp.z < 0f)
            offset = -offset; // behind the camera: viewport is mirrored

        Vector2 half = size * 0.5f - Vector2.one * edgeMargin;
        float scale = Mathf.Min(
            Mathf.Abs(offset.x) > 0.001f ? half.x / Mathf.Abs(offset.x) : float.MaxValue,
            Mathf.Abs(offset.y) > 0.001f ? half.y / Mathf.Abs(offset.y) : float.MaxValue);
        Vector2 pos = offset * scale;
        if (Mathf.Abs(pos.x) < topCenterKeepOut.x * 0.5f && pos.y > size.y * 0.5f - topCenterKeepOut.y)
            pos.y = size.y * 0.5f - topCenterKeepOut.y;
        e.marker.anchoredPosition = pos;
    }
}
