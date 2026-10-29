using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Top-center Day/Night dial. Read-only view: DayTimeManager owns day time, SimpleZombieSpawner owns
/// night time, GameManager owns the phase. Event-driven only (no Update): day refreshes once per second, night
/// once per second of night time plus wave starts and late kills. Night progress is time-based (elapsed / stream
/// end = last wave + stream tail) with one marker per wave time. Cloud drift lives in DayNightClouds, the runner's glide in
/// DayNightRunner.</summary>
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
    [Tooltip("Inactive template; one copy per wave at its wave time.")]
    [SerializeField] private RectTransform waveDividerTemplate;
    [Tooltip("Marker tint for waves that bring a Tank or Boss.")]
    [SerializeField] private Color waveMarkerMajorColor = new Color(1f, 0.35f, 0.3f, 1f);
    [Tooltip("Night progress icon riding the bar (hidden by day).")]
    [SerializeField] private DayNightRunner nightRunner;

    private DayTimeManager dayTimeManager;
    private SimpleZombieSpawner spawner;
    private bool started;
    private bool subscribed;
    private Color waveMarkerColor = Color.white;
    private bool waveMarkerColorCached;
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
        LayoutWaveMarkers(null);
        SetActive(nightRunner, false);
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
        // Night start only: the spawner has already loaded tonight's waves (GameManager resets it before OnPhaseChanged).
        LayoutWaveMarkers(spawner);
        SetActive(nightRunner, true);
        RefreshNight();
        if (nightRunner != null && spawner != null)
            nightRunner.SetTarget(spawner.GaugeProgress, true);
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

        float progress = spawner.GaugeProgress;
        PlaceOnOrbit(moonIcon, progress);
        SetFill(progress);
        if (nightRunner != null)
            nightRunner.SetTarget(progress, false);

        string detail = "";
        if (spawner.WavesStarted < spawner.WaveCount)
        {
            int seconds = Mathf.CeilToInt(spawner.SecondsToNextWave);
            detail = $"WAVE {spawner.WavesStarted + 1} · {seconds / 60}:{seconds % 60:00}";
        }
        else if (spawner.WaveCount > 0)
        {
            detail = $"ZOMBIES LEFT {Zombie.AliveZombieCount}";
        }

        SetText(detailText, ref lastDetail, detail);
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

    /// <summary>Places one marker per wave at waveTime / StreamEndTime (the stream tail after the last wave is the
    /// space before the bar's end); tints Tank/Boss waves. Called on phase change only, never per frame. Null hides all markers.</summary>
    private void LayoutWaveMarkers(SimpleZombieSpawner source)
    {
        if (waveDividerTemplate == null)
            return;

        int count = source != null && source.NightDuration > 0f ? source.WaveCount : 0;
        while (dividers.Count < count)
            dividers.Add(Instantiate(waveDividerTemplate, waveDividerTemplate.parent));

        for (int i = 0; i < dividers.Count; i++)
        {
            bool show = i < count;
            dividers[i].gameObject.SetActive(show);
            if (!show)
                continue;

            float x = Mathf.Clamp01(source.WaveTimeAt(i) / source.StreamEndTime);
            dividers[i].anchorMin = new Vector2(x, dividers[i].anchorMin.y);
            dividers[i].anchorMax = new Vector2(x, dividers[i].anchorMax.y);
            dividers[i].anchoredPosition = new Vector2(0f, dividers[i].anchoredPosition.y);

            Graphic graphic = dividers[i].GetComponent<Graphic>();
            if (graphic != null)
            {
                if (!waveMarkerColorCached)
                {
                    waveMarkerColor = graphic.color;
                    waveMarkerColorCached = true;
                }

                graphic.color = source.IsMajorWave(i) ? waveMarkerMajorColor : waveMarkerColor;
            }
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
