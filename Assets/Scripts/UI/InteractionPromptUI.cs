using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionPromptUI : MonoBehaviour
{
    private const string GatherPromptText = "Press E to Gather";
    private const string GatherPromptDisplayText = "[E]";

    [Header("World Prompt References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform promptRoot;
    [SerializeField] private TextMeshProUGUI actionText;

    [Header("Optional Cost Sections (icon + count containers)")]
    [SerializeField] private GameObject woodCostSection;
    [SerializeField] private TextMeshProUGUI woodCostText;
    [SerializeField] private GameObject scrapCostSection;
    [SerializeField] private TextMeshProUGUI scrapCostText;

    [Header("Optional Colors")]
    [SerializeField] private Color canAffordColor = Color.white;
    [SerializeField] private Color cannotAffordColor = Color.red;
    [Header("Prompt Layout")]
    [SerializeField, Tooltip("When action text is long, push cost sections down to avoid overlap.")]
    private Vector2 longTextCostOffset = new Vector2(0f, -22f);
    [SerializeField, Tooltip("Character count threshold before applying long-text offset.")]
    private int longTextThreshold = 26;
    [SerializeField, Tooltip("Text height ratio (vs single-line) to treat as wrapped and shift cost rows down.")]
    private float wrappedHeightRatioThreshold = 1.15f;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs screen-space prompt positioning details.")]
    private bool logScreenSpacePositioning;

    private Transform currentAnchor;
    private IInteractable currentTarget;
    private bool isVisible;
    private RectTransform woodCostRect;
    private RectTransform scrapCostRect;
    private RectTransform actionTextRect;
    private RectTransform worldCanvasRect;
    private Vector2 woodCostBasePos;
    private Vector2 scrapCostBasePos;
    private float singleLineActionTextHeight = -1f;

    private void Awake()
    {
        AutoAssignReferencesIfMissing();
        CacheCostSectionLayout();
        WarnIfMultiplePromptSystems();
        // Prompt should start hidden every time.
        Hide();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferencesIfMissing();
    }
#endif

    private void LateUpdate()
    {
        if (!isVisible || currentAnchor == null)
            return;

        UpdateScreenSpacePosition();
    }

    public void Show(IInteractable target, InteractablePromptData data)
    {
        if (target == null)
        {
            Hide();
            return;
        }

        if (!isVisible)
        {
            Debug.Log("[InteractionPromptUI] UI shown.");
        }

        if (currentTarget != target)
        {
            Debug.Log("[InteractionPromptUI] UI target changed.");
        }

        currentTarget = target;
        currentAnchor = target.GetUIAnchor();
        isVisible = true;

        gameObject.SetActive(true);
        SetCanvasVisible(true);
        ApplyData(data);
        UpdateScreenSpacePosition();
    }

    public void Hide()
    {
        if (isVisible)
        {
            Debug.Log("[InteractionPromptUI] UI hidden.");
        }

        isVisible = false;
        currentTarget = null;
        currentAnchor = null;

        if (actionText != null)
            actionText.text = string.Empty;

        if (woodCostText != null)
            woodCostText.text = string.Empty;

        if (scrapCostText != null)
            scrapCostText.text = string.Empty;

        if (woodCostSection != null)
            woodCostSection.SetActive(false);

        if (scrapCostSection != null)
            scrapCostSection.SetActive(false);

        SetCanvasVisible(false);
        gameObject.SetActive(false);
    }

    private void ApplyData(InteractablePromptData data)
    {
        if (actionText != null)
            actionText.text = GetDisplayActionText(data.actionText);

        bool showWood = data.woodCost > 0;
        if (woodCostSection != null)
            woodCostSection.SetActive(showWood);
        if (woodCostText != null)
            woodCostText.text = $"x{Mathf.Max(0, data.woodCost)}";

        bool showScrap = data.scrapCost > 0;
        if (scrapCostSection != null)
            scrapCostSection.SetActive(showScrap);
        if (scrapCostText != null)
            scrapCostText.text = $"x{Mathf.Max(0, data.scrapCost)}";

        Color dataColor = data.canAfford ? canAffordColor : cannotAffordColor;
        if (actionText != null)
            actionText.color = dataColor;
        if (woodCostText != null)
            woodCostText.color = dataColor;
        if (scrapCostText != null)
            scrapCostText.color = dataColor;

        ApplyCostLayoutOffset(data.actionText, showWood || showScrap);
        Debug.Log("[InteractionPromptUI] UI data updated.");
    }

    private void CacheCostSectionLayout()
    {
        if (worldCanvas != null)
        {
            worldCanvasRect = worldCanvas.GetComponent<RectTransform>();
        }

        if (woodCostSection != null)
        {
            woodCostRect = woodCostSection.GetComponent<RectTransform>();
            if (woodCostRect != null)
                woodCostBasePos = woodCostRect.anchoredPosition;
        }

        if (scrapCostSection != null)
        {
            scrapCostRect = scrapCostSection.GetComponent<RectTransform>();
            if (scrapCostRect != null)
                scrapCostBasePos = scrapCostRect.anchoredPosition;
        }

        if (actionText != null)
        {
            actionTextRect = actionText.rectTransform;
            if (actionTextRect != null)
            {
                singleLineActionTextHeight = Mathf.Max(1f, actionTextRect.rect.height);
            }
        }
    }

    private void UpdateScreenSpacePosition()
    {
        if (worldCanvas == null || worldCanvasRect == null || promptRoot == null || currentAnchor == null)
            return;

        Camera targetCamera = GetTargetCamera();
        if (targetCamera == null)
        {
            SetCanvasVisible(false);
            return;
        }

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(currentAnchor.position);
        if (screenPoint.z <= 0f)
        {
            SetCanvasVisible(false);
            return;
        }

        Camera eventCamera = worldCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldCanvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            SetCanvasVisible(false);
            return;
        }

        SetCanvasVisible(true);
        promptRoot.anchoredPosition = localPoint;

        if (logScreenSpacePositioning)
        {
            string targetName = (currentTarget as Component) != null ? ((Component)currentTarget).name : currentTarget.GetType().Name;
            string anchorName = currentAnchor != null ? currentAnchor.name : "<null>";
            string movedRectName = promptRoot != null ? promptRoot.name : "<null>";
            Debug.Log(
                $"[InteractionPromptUI] Positioned prompt. Target='{targetName}', Anchor='{anchorName}', " +
                $"AnchorWorld={currentAnchor.position}, ScreenPoint={screenPoint}, MovedRect='{movedRectName}', " +
                $"FinalAnchoredPosition={promptRoot.anchoredPosition}.",
                this);
        }
    }

    private void ApplyCostLayoutOffset(string promptText, bool hasVisibleCost)
    {
        bool hasPrompt = !string.IsNullOrWhiteSpace(promptText);
        bool shouldOffset = hasVisibleCost && hasPrompt;
        float offsetMagnitude = Mathf.Abs(longTextCostOffset.y);

        if (shouldOffset)
        {
            bool isWrapped = IsPromptLikelyWrapped(promptText, out float preferredHeight, out float baselineHeight);
            if (isWrapped)
            {
                // Push cost rows below wrapped text with a small visual gap.
                float wrappedExtra = Mathf.Max(0f, preferredHeight - baselineHeight) + 8f;
                offsetMagnitude = Mathf.Max(offsetMagnitude, wrappedExtra);
            }
            else
            {
                // Even for short prompts with visible costs, force a minimum safe separation.
                offsetMagnitude = Mathf.Max(offsetMagnitude, 28f);
            }
        }

        Vector2 effectiveOffset = shouldOffset
            ? new Vector2(longTextCostOffset.x, -offsetMagnitude)
            : Vector2.zero;

        if (woodCostRect != null)
            woodCostRect.anchoredPosition = woodCostBasePos + effectiveOffset;

        if (scrapCostRect != null)
            scrapCostRect.anchoredPosition = scrapCostBasePos + effectiveOffset;
    }

    private bool IsPromptLikelyWrapped(string promptText)
    {
        return IsPromptLikelyWrapped(promptText, out _, out _);
    }

    private bool IsPromptLikelyWrapped(string promptText, out float preferredHeight, out float baselineHeight)
    {
        preferredHeight = 0f;
        baselineHeight = 0f;

        if (actionText == null || actionTextRect == null || string.IsNullOrWhiteSpace(promptText))
        {
            return false;
        }

        actionText.ForceMeshUpdate();
        preferredHeight = actionText.GetPreferredValues(promptText, actionTextRect.rect.width, 0f).y;
        baselineHeight = singleLineActionTextHeight > 0f ? singleLineActionTextHeight : Mathf.Max(1f, actionText.fontSize + 4f);
        float threshold = baselineHeight * Mathf.Max(1f, wrappedHeightRatioThreshold);
        return preferredHeight > threshold;
    }

    private void AutoAssignReferencesIfMissing()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>(includeInactive: true);

        if (actionText == null)
            actionText = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

        if (promptRoot == null && actionText != null)
        {
            RectTransform actionRect = actionText.rectTransform;
            if (actionRect != null
                && actionRect.parent is RectTransform parentRect
                && parentRect != worldCanvasRect)
                promptRoot = parentRect;
            else
                promptRoot = actionRect;
        }

        ConfigureCanvasForScreenSpace();
    }

    private void WarnIfMultiplePromptSystems()
    {
        InteractionPromptUI[] promptUis = FindObjectsOfType<InteractionPromptUI>(includeInactive: true);
        if (promptUis.Length > 1)
        {
            Debug.LogWarning("[InteractionPromptUI] Multiple InteractionPromptUI objects detected. Only one prompt system should be active.");
        }
    }

    private void ConfigureCanvasForScreenSpace()
    {
        if (worldCanvas == null)
            return;

        worldCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        worldCanvas.worldCamera = null;

        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
            canvasRect.localScale = Vector3.one;

        if (promptRoot != null)
            promptRoot.localScale = Vector3.one;

        if (actionText != null)
            actionText.rectTransform.localScale = Vector3.one;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (worldCanvas != null)
            worldCanvas.enabled = visible;
    }

    private Camera GetTargetCamera()
    {
        if (worldCanvas != null && worldCanvas.renderMode == RenderMode.ScreenSpaceCamera && worldCanvas.worldCamera != null)
            return worldCanvas.worldCamera;

        return Camera.main;
    }

    private static string GetDisplayActionText(string actionTextValue)
    {
        if (string.IsNullOrWhiteSpace(actionTextValue))
            return string.Empty;

        return actionTextValue == GatherPromptText ? GatherPromptDisplayText : actionTextValue;
    }
}
