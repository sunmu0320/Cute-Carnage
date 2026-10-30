using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-edge markers pointing at off-screen world points (wave alarm). Each Show returns a handle;
/// once its point is on screen the marker sits over it for the first minVisibleSeconds, then hides while on
/// screen, and disappears for good on Hide or when its time runs out. Position is re-pinned in LateUpdate while anything is shown — a deliberate
/// rule-6 exception (display only, the camera moves every frame); the component disables itself when empty.</summary>
public class EdgeIndicatorUI : MonoBehaviour
{
    public static EdgeIndicatorUI Instance { get; private set; }

    [Tooltip("Inactive template (Image, optionally with a child label); one copy per shown marker.")]
    [SerializeField] private RectTransform template;
    [SerializeField] private float edgeMargin = 40f;
    [Tooltip("For this long after Show the marker stays up even when its point is on screen (drawn over it).")]
    [SerializeField, Min(0f)] private float minVisibleSeconds = 5f;
    [Tooltip("Top-center box (width, height) kept clear for the Day/Night dial; markers there drop below it.")]
    [SerializeField] private Vector2 topCenterKeepOut = new Vector2(340f, 300f);

    private class Entry
    {
        public RectTransform marker;
        public Vector3 point;
        public float hideAt;
        public float keepOnScreenUntil;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    private readonly Stack<RectTransform> spare = new Stack<RectTransform>();
    private readonly List<int> expired = new List<int>();
    private RectTransform area;
    private Camera cam;
    private int nextHandle = 1;

    private void Awake()
    {
        Instance = this;
        area = (RectTransform)transform;
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
            keepOnScreenUntil = Time.time + minVisibleSeconds,
        };
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

    /// <summary>Off screen: pinned to the screen edge toward the point. On screen: drawn over the point for the
    /// first minVisibleSeconds, hidden after that.</summary>
    private void Pin(Entry e)
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        Vector3 vp = cam.WorldToViewportPoint(e.point);
        bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        bool visible = !onScreen || Time.time < e.keepOnScreenUntil;
        if (e.marker.gameObject.activeSelf != visible)
            e.marker.gameObject.SetActive(visible);
        if (!visible)
            return;

        Vector2 size = area.rect.size;
        Vector2 offset = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        if (onScreen)
        {
            e.marker.anchoredPosition = offset;
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
