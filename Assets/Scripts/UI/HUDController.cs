using System;
using System.IO;
using UnityEngine;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("Player HP (Top Left)")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Gameplay Sources (Optional)")]
    [Tooltip("Optional. If assigned, HUD will read current/max HP from this component.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Optional. If assigned, HUD will read Food/Scrap/Wood totals from this component.")]
    [SerializeField] private ResourceManager resourceManager;

    [Header("Resources (Bottom Left) - No Fuel")]
    [SerializeField] private TextMeshProUGUI foodText;
    [SerializeField] private TextMeshProUGUI scrapText;
    [SerializeField] private TextMeshProUGUI woodText;

    [Header("Status (Top Center)")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI phaseText;

    private void Awake()
    {
        ApplyPlaceholderText();

        if (Application.isPlaying)
        {
            RefreshFromSources(); // Replace placeholders when sources are assigned.
            LogHudDebug();
        }
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        // In play mode, we want the UI to update as soon as it becomes enabled.
        // (OnValidate already covers editor-only placeholder visuals.)
        if (Application.isPlaying)
        {
            if (playerHealth != null)
            {
                playerHealth.onHealthChanged.AddListener(UpdateHP);
                UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            RefreshFromSources();
        }
    }
#else
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (playerHealth != null)
            {
                playerHealth.onHealthChanged.AddListener(UpdateHP);
                UpdateHP(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            RefreshFromSources();
        }
    }
#endif

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (playerHealth != null)
            playerHealth.onHealthChanged.RemoveListener(UpdateHP);
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

        RefreshFromSources();
    }

    public void UpdateHP(int current, int max)
    {
        if (hpText == null)
            return;

        SetTextOrIgnore(hpText, $"HP {current} / {max}");
    }

    private void ApplyPlaceholderText()
    {
        SetTextOrIgnore(hpText, "HP 100 / 100");
        SetTextOrIgnore(foodText, "Food : 0");
        SetTextOrIgnore(scrapText, "Scrap : 0");
        SetTextOrIgnore(woodText, "Wood : 0");
        SetTextOrIgnore(dayText, GetFallbackDayText());
        SetTextOrIgnore(phaseText, GetFallbackPhaseText());
    }

    private void RefreshFromSources()
    {
        // Resources
        if (resourceManager != null && (foodText != null || scrapText != null || woodText != null))
        {
            if (foodText != null)
                SetTextOrIgnore(foodText, $"Food : {resourceManager.GetAmount(ResourceType.Food)}");
            if (scrapText != null)
                SetTextOrIgnore(scrapText, $"Scrap : {resourceManager.GetAmount(ResourceType.Scrap)}");
            if (woodText != null)
                SetTextOrIgnore(woodText, $"Wood : {resourceManager.GetAmount(ResourceType.Wood)}");
        }
        else
        {
            SetTextOrIgnore(foodText, "Food : 0");
            SetTextOrIgnore(scrapText, "Scrap : 0");
            SetTextOrIgnore(woodText, "Wood : 0");
        }

        // Day/Phase (placeholder fallback; wire real time system later).
        SetTextOrIgnore(dayText, GetFallbackDayText());
        SetTextOrIgnore(phaseText, GetFallbackPhaseText());
        // TODO: Later: connect to your time/day-phase system and update dayText/phaseText safely.
    }

    private static string GetFallbackDayText() => "DAY 1";
    private static string GetFallbackPhaseText() => "SCAVENGE";

    private static void SetTextOrIgnore(TextMeshProUGUI target, string value)
    {
        if (target == null)
            return;

        target.text = value;
    }

    private void LogHudDebug()
    {
        Canvas canvasInParents = GetComponentInParent<Canvas>(includeInactive: true);
        string logPath = GetLogFilePath();

        bool hpNull = hpText == null;
        bool foodNull = foodText == null;
        bool scrapNull = scrapText == null;
        bool woodNull = woodText == null;
        bool dayNull = dayText == null;
        bool phaseNull = phaseText == null;

        #region agent log
        AppendNdjson(logPath,
            hypothesisId: "A",
            runId: "pre-fix",
            location: "HUDController.cs:LogHudDebug:renderMode",
            message: "HUD Canvas renderMode (WorldSpace vs Overlay/Camera)",
            dataJson: canvasInParents == null
                ? "{\"canvasFound\":false}"
                : $"{{\"canvasFound\":true,\"renderMode\":\"{canvasInParents.renderMode}\",\"renderModeInt\":{(int)canvasInParents.renderMode}}}");
        #endregion

        #region agent log
        AppendNdjson(logPath,
            hypothesisId: "B",
            runId: "pre-fix",
            location: "HUDController.cs:LogHudDebug:worldCamera",
            message: "HUD Canvas camera reference (if applicable)",
            dataJson: canvasInParents == null
                ? "{\"canvasFound\":false}"
                : $"{{\"canvasFound\":true,\"worldCameraAssigned\":{(canvasInParents.worldCamera != null ? "true" : "false")},\"worldCameraName\":\"{(canvasInParents.worldCamera != null ? canvasInParents.worldCamera.name : "")}\"}}");
        #endregion

        #region agent log
        AppendNdjson(logPath,
            hypothesisId: "C",
            runId: "pre-fix",
            location: "HUDController.cs:LogHudDebug:activeState",
            message: "Active state of HUDRoot and canvas",
            dataJson: canvasInParents == null
                ? $"{{\"canvasFound\":false,\"hudRootActiveInHierarchy\":{(gameObject.activeInHierarchy ? "true" : "false")}}}"
                : $"{{\"canvasFound\":true,\"hudRootActiveInHierarchy\":{(gameObject.activeInHierarchy ? "true" : "false")},\"canvasActiveInHierarchy\":{(canvasInParents.gameObject.activeInHierarchy ? "true" : "false")},\"canvasEnabled\":{(canvasInParents.enabled ? "true" : "false")}}}");
        #endregion

        #region agent log
        AppendNdjson(logPath,
            hypothesisId: "D",
            runId: "pre-fix",
            location: "HUDController.cs:LogHudDebug:tmpRefs",
            message: "Serialized TMP text references null status",
            dataJson: $"{{\"hpTextNull\":{(hpNull ? "true" : "false")},\"foodTextNull\":{(foodNull ? "true" : "false")},\"scrapTextNull\":{(scrapNull ? "true" : "false")},\"woodTextNull\":{(woodNull ? "true" : "false")},\"dayTextNull\":{(dayNull ? "true" : "false")},\"phaseTextNull\":{(phaseNull ? "true" : "false")}}}");
        #endregion
    }

    private static string GetLogFilePath()
    {
        try
        {
            string assetsPath = Application.dataPath; // <project>/Assets
            string projectRoot = Directory.GetParent(assetsPath)?.FullName ?? assetsPath;
            return Path.Combine(projectRoot, "debug-decb40.log");
        }
        catch
        {
            // Fallback: write to relative path.
            return Path.Combine(Directory.GetCurrentDirectory(), "debug-decb40.log");
        }
    }

    private static void AppendNdjson(string logPath, string hypothesisId, string runId, string location, string message, string dataJson)
    {
        try
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string id = $"log_{timestamp}_{Guid.NewGuid():N}";

            // sessionId required by the debug workflow.
            string sessionId = "decb40";

            string safeLocation = EscapeJson(location);
            string safeMessage = EscapeJson(message);

            // dataJson is expected to already be valid JSON (built from simple primitives).
            string line =
                $"{{\"sessionId\":\"{sessionId}\",\"id\":\"{EscapeJson(id)}\",\"timestamp\":{timestamp},\"location\":\"{safeLocation}\",\"message\":\"{safeMessage}\",\"data\":{dataJson},\"runId\":\"{EscapeJson(runId)}\",\"hypothesisId\":\"{EscapeJson(hypothesisId)}\"}}";

            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never break gameplay; swallow exceptions.
        }
    }

    private static string EscapeJson(string value)
    {
        if (value == null)
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}

