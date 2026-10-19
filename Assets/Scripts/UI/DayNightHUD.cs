using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Top-center Day/Night dial. Read-only view: DayTimeManager owns day time, SimpleZombieSpawner owns
/// night progress, GameManager owns the phase. Event-driven only (no Update): day refreshes once per second,
/// night on each kill / wave change. Cloud drift lives in DayNightClouds.</summary>
public class DayNightHUD : MonoBehaviour
{
    [Header("Sky (inside the circular mask)")]
    [SerializeField] private Image skyDay;
    [Tooltip("Drawn over skyDay; alpha rises from sunsetStartProgress to the end of the day.")]
    [SerializeField] private Image skySunset;
    [SerializeField] private Image skyNight;
    [SerializeField, Range(0f, 1f)] private float sunsetStartProgress = 0.8f;

    [Header("Sun / Moon path (dial local space, origin = dial center)")]
    [SerializeField] private RectTransform sunIcon;
    [SerializeField] private RectTransform moonIcon;
    [Tooltip("Horizon point the half-ellipse is centered on; keep it low so both ends sit behind the village.")]
    [SerializeField] private Vector2 orbitCenter = new Vector2(0f, -45f);
    [SerializeField] private Vector2 orbitRadius = new Vector2(46f, 85f);

    [Header("Clouds (tint only; motion is DayNightClouds)")]
    [SerializeField] private Image[] clouds;
    [SerializeField] private Color cloudDayColor = Color.white;
    [SerializeField] private Color cloudNightColor = new Color(0.55f, 0.6f, 0.8f, 0.45f);

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI phaseLabel;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Gauge")]
    [Tooltip("Sliced fill stretched from the left; its anchorMax.x is the fill amount.")]
    [SerializeField] private RectTransform barFill;
    [SerializeField] private Image barFillImage;
    [SerializeField] private Sprite dayFillSprite;
    [SerializeField] private Sprite nightFillSprite;
    [Tooltip("Inactive template; N-1 copies are spread over its parent at night.")]
    [SerializeField] private RectTransform waveDividerTemplate;

    private DayTimeManager dayTimeManager;
    private SimpleZombieSpawner spawner;
    private bool started;
    private bool subscribed;
    private int dividerCount = -1;
    private string lastLabel;
    private string lastDetail;
    private readonly System.Collections.Generic.List<RectTransform> dividers = new System.Collections.Generic.List<RectTransform>();

    private void Start()
    {
        // Resolved once; both live in the single persistent Day scene (spawner sits under the inactive nightRoot by day).
        dayTimeManager = FindFirstObjectByType<DayTimeManager>(FindObjectsInactive.Include);
        spawner = FindFirstObjectByType<SimpleZombieSpawner>(FindObjectsInactive.Include);
        started = true;
        Subscribe();
    }

    private void OnEnable()
    {
        if (started)
            Subscribe();
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        if (dayTimeManager != null)
            dayTimeManager.OnSecondChanged -= HandleSecondChanged;
        if (spawner != null)
            spawner.OnNightProgressChanged -= HandleNightProgressChanged;
        subscribed = false;
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        if (dayTimeManager != null)
            dayTimeManager.OnSecondChanged += HandleSecondChanged;
        if (spawner != null)
            spawner.OnNightProgressChanged += HandleNightProgressChanged;
        subscribed = true;

        // Re-enable / scene entry: show the live state, never a forced 0%.
        if (GameManager.Instance != null)
            HandlePhaseChanged(GameManager.Instance.CurrentPhase);
    }

    private void HandlePhaseChanged(GameManager.GamePhase phase)
    {
        // GameOver/Victory: keep showing the frozen day/night state underneath the panel.
        if (phase == GameManager.GamePhase.Day)
            ShowDay();
        else if (phase == GameManager.GamePhase.Night)
            ShowNight();
    }

    private void HandleSecondChanged(int _)
    {
        if (IsPhase(GameManager.GamePhase.Day))
            RefreshDay();
    }

    private void HandleNightProgressChanged()
    {
        if (IsPhase(GameManager.GamePhase.Night))
            RefreshNight();
    }

    private static bool IsPhase(GameManager.GamePhase phase)
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentPhase == phase;
    }

    private void ShowDay()
    {
        SetActive(moonIcon, false);
        SetActive(sunIcon, true);
        SetActive(skyNight, false);
        SetActive(skySunset, true);
        SetCloudColor(cloudDayColor);
        SetFillSprite(dayFillSprite);
        SetDividerCount(0);
        SetText(phaseLabel, ref lastLabel, $"DAY {GameManager.Instance.CurrentDay}");
        RefreshDay();
    }

    private void ShowNight()
    {
        SetActive(sunIcon, false);
        SetActive(moonIcon, true);
        SetActive(skySunset, false);
        SetActive(skyNight, true);
        SetCloudColor(cloudNightColor);
        SetFillSprite(nightFillSprite);
        SetText(phaseLabel, ref lastLabel, $"NIGHT {GameManager.Instance.CurrentDay}");
        RefreshNight();
    }

    private void RefreshDay()
    {
        if (dayTimeManager == null)
            return;

        float progress = Mathf.Clamp01(dayTimeManager.NormalizedTime);
        PlaceOnOrbit(sunIcon, progress);
        SetFill(1f - progress);
        if (skySunset != null)
        {
            Color c = skySunset.color;
            c.a = Mathf.InverseLerp(sunsetStartProgress, 1f, progress);
            skySunset.color = c;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(dayTimeManager.RemainingTimeSeconds));
        SetText(detailText, ref lastDetail, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    private void RefreshNight()
    {
        if (spawner == null)
            return;

        float progress = spawner.NightProgress;
        PlaceOnOrbit(moonIcon, progress);
        SetFill(progress);
        SetDividerCount(Mathf.Max(0, spawner.WaveCount - 1));
        SetText(detailText, ref lastDetail, spawner.WaveCount > 0 ? $"WAVE {spawner.CurrentWaveNumber} / {spawner.WaveCount}" : "");
    }

    /// <summary>p = 0 east (right), 0.5 top, 1 west (left). Moves the icon only; nothing rotates.</summary>
    private void PlaceOnOrbit(RectTransform icon, float p)
    {
        if (icon == null)
            return;

        float angle = Mathf.PI * p;
        icon.anchoredPosition = orbitCenter + new Vector2(Mathf.Cos(angle) * orbitRadius.x, Mathf.Sin(angle) * orbitRadius.y);
    }

    private void SetFill(float amount)
    {
        if (barFill == null)
            return;

        Vector2 max = barFill.anchorMax;
        max.x = Mathf.Clamp01(amount);
        barFill.anchorMax = max;
    }

    private void SetFillSprite(Sprite sprite)
    {
        if (barFillImage == null || sprite == null || barFillImage.sprite == sprite)
            return;

        barFillImage.sprite = sprite;
        // Scale the sliced caps to the bar height so both sprites keep round ends.
        float height = ((RectTransform)barFillImage.transform).rect.height;
        if (height > 0f)
            barFillImage.pixelsPerUnitMultiplier = sprite.rect.height / height;
    }

    private void SetCloudColor(Color color)
    {
        if (clouds == null)
            return;

        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] != null)
                clouds[i].color = color;
        }
    }

    /// <summary>Only rebuilds when the wave count changes (night start), never per frame.</summary>
    private void SetDividerCount(int count)
    {
        if (count == dividerCount || waveDividerTemplate == null)
            return;

        dividerCount = count;
        while (dividers.Count < count)
            dividers.Add(Instantiate(waveDividerTemplate, waveDividerTemplate.parent));

        int waves = count + 1;
        for (int i = 0; i < dividers.Count; i++)
        {
            bool show = i < count;
            dividers[i].gameObject.SetActive(show);
            if (!show)
                continue;

            float x = (i + 1f) / waves;
            dividers[i].anchorMin = new Vector2(x, dividers[i].anchorMin.y);
            dividers[i].anchorMax = new Vector2(x, dividers[i].anchorMax.y);
            dividers[i].anchoredPosition = new Vector2(0f, dividers[i].anchoredPosition.y);
        }
    }

    private static void SetActive(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active)
            target.gameObject.SetActive(active);
    }

    private static void SetText(TextMeshProUGUI target, ref string last, string value)
    {
        if (target == null || last == value)
            return;

        last = value;
        target.text = value;
    }
}
