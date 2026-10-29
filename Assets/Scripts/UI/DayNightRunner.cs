using UnityEngine;

/// <summary>Night-gauge runner icon. DayNightHUD sets a target x (0-1 along the bar) about once per second; this
/// glides there over that second and bobs like a running step. Decorative animation only (same rule-6 exception
/// as DayNightClouds): it reads no game state, and stops with timeScale 0.</summary>
public class DayNightRunner : MonoBehaviour
{
    [SerializeField] private float bobHeight = 2f;
    [SerializeField] private float stepsPerSecond = 4f;

    private RectTransform rect;
    private float x;
    private float target;
    private float speed;
    private float baseY;
    private float phase;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseY = rect.anchoredPosition.y;
    }

    /// <summary>snap: jump straight there (night start); otherwise arrive in one second.</summary>
    public void SetTarget(float x01, bool snap)
    {
        if (rect == null)
            Awake();

        target = Mathf.Clamp01(x01);
        if (snap)
            x = target;
        speed = Mathf.Abs(target - x);
        Apply(0f);
    }

    private void Update()
    {
        if (x == target)
            return;

        x = Mathf.MoveTowards(x, target, speed * Time.deltaTime);
        phase += Time.deltaTime * stepsPerSecond * Mathf.PI * 2f;
        Apply(Mathf.Abs(Mathf.Sin(phase)) * bobHeight);
    }

    private void Apply(float bob)
    {
        rect.anchorMin = new Vector2(x, rect.anchorMin.y);
        rect.anchorMax = new Vector2(x, rect.anchorMax.y);
        rect.anchoredPosition = new Vector2(0f, baseY + bob);
    }
}
