using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("Player HP (Top Left)")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Player Hunger (Top Left, below HP)")]
    [Tooltip("Optional. If assigned, HUD shows current/max hunger on hungerText.")]
    [SerializeField] private HungerSystem hungerSystem;

    [Tooltip("Optional. TMP line directly under HP; format: Hunger 80 / 100.")]
    [SerializeField] private TextMeshProUGUI hungerText;

    [Header("Gameplay Sources (Optional)")]
    [Tooltip("Optional. If assigned, HUD will read current/max HP from this component.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Optional. If assigned, HUD will read Food/Scrap/Wood totals from this component.")]
    [SerializeField] private ResourceManager resourceManager;

    [Header("Base HP (Bottom Center)")]
    [Tooltip("Optional. If assigned, HUD will read current/max HP from this BaseCore.")]
    [SerializeField] private BaseCore baseCore;
    [Tooltip("Optional. Filled Image used for the fixed Screen Space Base HP bar.")]
    [SerializeField] private Image baseHpBarFillImage;
    [Tooltip("Optional. Root hidden while no valid BaseCore exists.")]
    [SerializeField] private GameObject baseHpBarRoot;

    [Header("Resources (Bottom Left) - No Fuel")]
    [SerializeField] private TextMeshProUGUI foodText;
    [SerializeField] private TextMeshProUGUI scrapText;
    [SerializeField] private TextMeshProUGUI woodText;

    [Header("Status (Top Center)")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI phaseText;

    [Header("Day Timer Bar (Top Center)")]
    [Tooltip("Optional. If assigned, HUD will read remaining time/progress from this DayTimeManager.")]
    [SerializeField] private DayTimeManager dayTimeManager;

    [Tooltip("Optional. The UI Image used as the filled portion of the bar. Must be set to Image.Type = Filled.")]
    [SerializeField] private Image dayTimerBarFillImage;

    [Tooltip("Optional. If assigned, HUD will render remaining time as MM:SS on/within the bar.")]
    [SerializeField] private TextMeshProUGUI dayTimerText;

    private PlayerHealth subscribedPlayerHealth;
    private bool hpCacheValid;
    private int lastHpCurrent;
    private int lastHpMax;
    private bool hungerCacheValid;
    private int lastHungerCurrent;
    private int lastHungerMax;
    private bool resourceCacheValid;
    private int lastFood;
    private int lastScrap;
    private int lastWood;
    private bool dayCacheValid;
    private int lastDay;
    private bool dayTimerSecondsCacheValid;
    private int lastDayTimerSeconds;
    private bool dayTimerFillCacheValid;
    private float lastDayTimerFill;
    private BaseCore lastBaseCore;
    private bool baseHpFillCacheValid;
    private float lastBaseHpFill;
    private float nextSourceResolveTime;

    private const float SourceRetryInterval = 1f;
    private const float DayTimerFillEpsilon = 0.0001f;
    private const float BaseHpFillEpsilon = 0.0001f;

    private void Awake()
    {
        ApplyPlaceholderText();

        if (Application.isPlaying)
        {
            RefreshFromSources(); // Replace placeholders when sources are assigned.
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        InvalidateDisplayCaches();
        ResolveMissingSources();
        BindPlayerHealth();
        RefreshCurrentValues();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (subscribedPlayerHealth != null)
            subscribedPlayerHealth.onHealthChanged.RemoveListener(UpdateHP);
        subscribedPlayerHealth = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep the UI foundation visible even before Play Mode.
        // This is safe because everything is null-checked.
        ApplyPlaceholderText();
    }
#endif

    private void Update()
    {
        // Simple and safe: if sources are assigned, keep HUD in sync.
        // If sources are missing, it will just keep the fallback placeholders.
        if (!Application.isPlaying)
            return;

        if (HasMissingSource() && Time.unscaledTime >= nextSourceResolveTime)
        {
            ResolveMissingSources();
            BindPlayerHealth();
        }

        RefreshCurrentValues();
    }

    public void RefreshHudFromPlayerStats()
    {
        ResolveMissingSources();
        BindPlayerHealth();
        RefreshCurrentValues();
    }

    public void UpdateHP(int current, int max)
    {
        if (hpText == null)
            return;

        if (hpCacheValid && lastHpCurrent == current && lastHpMax == max)
            return;

        SetTextOrIgnore(hpText, $"HP {current} / {max}");
        lastHpCurrent = current;
        lastHpMax = max;
        hpCacheValid = true;
    }

    private void ApplyPlaceholderText()
    {
        SetTextOrIgnore(hpText, "HP 100 / 100");
        SetTextOrIgnore(hungerText, GetFallbackHungerText());
        SetTextOrIgnore(foodText, "Food : 0");
        SetTextOrIgnore(scrapText, "Scrap : 0");
        SetTextOrIgnore(woodText, "Wood : 0");
        SetTextOrIgnore(dayText, GetFallbackDayText());
    }

    private void RefreshFromSources()
    {
        ResolveMissingSources();
        BindPlayerHealth();
        RefreshCurrentValues();
    }

    private void RefreshCurrentValues()
    {
        if (playerHealth != null)
            UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);

        RefreshBaseHp();

        // Resources
        if (resourceManager != null && (foodText != null || scrapText != null || woodText != null))
        {
            int food = resourceManager.GetAmount(ResourceType.Food);
            int scrap = resourceManager.GetAmount(ResourceType.Scrap);
            int wood = resourceManager.GetAmount(ResourceType.Wood);

            if (!resourceCacheValid || lastFood != food)
                SetTextOrIgnore(foodText, $"Food : {food}");
            if (!resourceCacheValid || lastScrap != scrap)
                SetTextOrIgnore(scrapText, $"Scrap : {scrap}");
            if (!resourceCacheValid || lastWood != wood)
                SetTextOrIgnore(woodText, $"Wood : {wood}");

            lastFood = food;
            lastScrap = scrap;
            lastWood = wood;
            resourceCacheValid = true;
        }
        else
        {
            if (!resourceCacheValid || lastFood != 0)
                SetTextOrIgnore(foodText, "Food : 0");
            if (!resourceCacheValid || lastScrap != 0)
                SetTextOrIgnore(scrapText, "Scrap : 0");
            if (!resourceCacheValid || lastWood != 0)
                SetTextOrIgnore(woodText, "Wood : 0");

            lastFood = 0;
            lastScrap = 0;
            lastWood = 0;
            resourceCacheValid = true;
        }

        // Hunger (optional). HungerSystem owns values; HUD only displays rounded integers.
        if (hungerSystem != null && hungerText != null)
        {
            int cur = Mathf.RoundToInt(hungerSystem.CurrentHunger);
            int max = Mathf.RoundToInt(hungerSystem.MaxHunger);
            if (!hungerCacheValid || lastHungerCurrent != cur || lastHungerMax != max)
                SetTextOrIgnore(hungerText, $"Hunger {cur} / {max}");

            lastHungerCurrent = cur;
            lastHungerMax = max;
            hungerCacheValid = true;
        }
        else
        {
            if (!hungerCacheValid || lastHungerCurrent != 100 || lastHungerMax != 100)
                SetTextOrIgnore(hungerText, GetFallbackHungerText());

            lastHungerCurrent = 100;
            lastHungerMax = 100;
            hungerCacheValid = true;
        }

        // Day (placeholder fallback; day timer bar is optional and driven by DayTimeManager).
        if (GameManager.Instance != null)
        {
            int currentDay = GameManager.Instance.CurrentDay;
            if (!dayCacheValid || lastDay != currentDay)
                SetTextOrIgnore(dayText, $"DAY {currentDay}");

            lastDay = currentDay;
            dayCacheValid = true;
        }
        else
        {
            if (!dayCacheValid || lastDay != 1)
                SetTextOrIgnore(dayText, GetFallbackDayText());

            lastDay = 1;
            dayCacheValid = true;
        }

        if (dayTimeManager != null)
        {
            float remainingSeconds = dayTimeManager.RemainingTimeSeconds;
            // NormalizedTime is day-progress (0 -> 1). We want remaining fraction (1 -> 0).
            float remainingNormalized = 1f - dayTimeManager.NormalizedTime;
            float fillAmount = Mathf.Clamp01(remainingNormalized);

            if (dayTimerBarFillImage != null
                && (!dayTimerFillCacheValid || Mathf.Abs(lastDayTimerFill - fillAmount) > DayTimerFillEpsilon))
            {
                dayTimerBarFillImage.fillAmount = fillAmount;
                lastDayTimerFill = fillAmount;
                dayTimerFillCacheValid = true;
            }

            int displaySeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            if (!dayTimerSecondsCacheValid || lastDayTimerSeconds != displaySeconds)
            {
                SetTextOrIgnore(dayTimerText, FormatRemainingTime(displaySeconds));
                lastDayTimerSeconds = displaySeconds;
                dayTimerSecondsCacheValid = true;
            }
        }
    }

    private void ResolveMissingSources()
    {
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (hungerSystem == null)
            hungerSystem = FindFirstObjectByType<HungerSystem>();
        if (resourceManager == null)
            resourceManager = ResourceManager.FindInActiveLoadedScene();
        if (dayTimeManager == null)
            dayTimeManager = FindFirstObjectByType<DayTimeManager>();
        if (!IsValidActiveSceneBaseCore(baseCore))
            SetBaseCore(FindBaseCoreInActiveScene());

        nextSourceResolveTime = Time.unscaledTime + SourceRetryInterval;
    }

    private bool HasMissingSource()
    {
        return playerHealth == null
            || hungerSystem == null
            || resourceManager == null
            || dayTimeManager == null
            || !IsValidActiveSceneBaseCore(baseCore);
    }

    private void BindPlayerHealth()
    {
        if (subscribedPlayerHealth == playerHealth)
            return;

        if (subscribedPlayerHealth != null)
            subscribedPlayerHealth.onHealthChanged.RemoveListener(UpdateHP);

        subscribedPlayerHealth = playerHealth;
        if (subscribedPlayerHealth != null)
            subscribedPlayerHealth.onHealthChanged.AddListener(UpdateHP);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateBaseHpCache();
        ResolveMissingSources();
        BindPlayerHealth();
        RefreshCurrentValues();
    }

    private void InvalidateDisplayCaches()
    {
        hpCacheValid = false;
        hungerCacheValid = false;
        resourceCacheValid = false;
        dayCacheValid = false;
        dayTimerSecondsCacheValid = false;
        dayTimerFillCacheValid = false;
        InvalidateBaseHpCache();
    }

    private void RefreshBaseHp()
    {
        bool hasValidBaseCore = IsValidActiveSceneBaseCore(baseCore);
        if (lastBaseCore != baseCore)
        {
            lastBaseCore = baseCore;
            baseHpFillCacheValid = false;
        }

        if (baseHpBarRoot != null && baseHpBarRoot.activeSelf != hasValidBaseCore)
            baseHpBarRoot.SetActive(hasValidBaseCore);

        if (!hasValidBaseCore)
            return;

        float fill = Mathf.Clamp01(baseCore.CurrentHp / Mathf.Max(1f, baseCore.MaxHp));
        if (baseHpBarFillImage != null
            && (!baseHpFillCacheValid || Mathf.Abs(lastBaseHpFill - fill) > BaseHpFillEpsilon))
        {
            baseHpBarFillImage.fillAmount = fill;
        }

        lastBaseHpFill = fill;
        baseHpFillCacheValid = true;
    }

    private void SetBaseCore(BaseCore resolvedBaseCore)
    {
        if (baseCore == resolvedBaseCore)
            return;

        baseCore = resolvedBaseCore;
        InvalidateBaseHpCache();
    }

    private void InvalidateBaseHpCache()
    {
        lastBaseCore = null;
        baseHpFillCacheValid = false;
    }

    private static BaseCore FindBaseCoreInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        BaseCore[] candidates = FindObjectsByType<BaseCore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsBaseCoreInScene(candidates[i], activeScene))
                return candidates[i];
        }

        return null;
    }

    private static bool IsValidActiveSceneBaseCore(BaseCore candidate)
    {
        return candidate != null
            && candidate.isActiveAndEnabled
            && candidate.gameObject.activeInHierarchy
            && IsBaseCoreInScene(candidate, SceneManager.GetActiveScene());
    }

    private static bool IsBaseCoreInScene(BaseCore candidate, Scene scene)
    {
        return candidate != null
            && scene.IsValid()
            && scene.isLoaded
            && candidate.gameObject.scene == scene;
    }

    private static string GetFallbackDayText() => "DAY 1";
    private static string GetFallbackHungerText() => "Hunger 100 / 100";
    private static string GetFallbackPhaseText() => "SCAVENGE";

    private static string FormatRemainingTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private static void SetTextOrIgnore(TextMeshProUGUI target, string value)
    {
        if (target == null)
            return;

        target.text = value;
    }

}

