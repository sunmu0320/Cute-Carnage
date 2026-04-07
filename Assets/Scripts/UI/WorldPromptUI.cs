using TMPro;
using UnityEngine;

public class WorldPromptUI : MonoBehaviour
{
    [Header("World Prompt References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private TextMeshProUGUI actionText;

    [Header("Optional Cost Sections (icon + count containers)")]
    [SerializeField] private GameObject woodCostSection;
    [SerializeField] private TextMeshProUGUI woodCostText;
    [SerializeField] private GameObject scrapCostSection;
    [SerializeField] private TextMeshProUGUI scrapCostText;

    [Header("Optional Colors")]
    [SerializeField] private Color canAffordColor = Color.white;
    [SerializeField] private Color cannotAffordColor = Color.red;

    private Transform currentAnchor;
    private IInteractable currentTarget;
    private bool isVisible;

    private void Awake()
    {
        AutoAssignReferencesIfMissing();
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

        transform.position = currentAnchor.position;
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
            Debug.Log("[WorldPromptUI] UI shown.");
        }

        if (currentTarget != target)
        {
            Debug.Log("[WorldPromptUI] UI target changed.");
        }

        currentTarget = target;
        currentAnchor = target.GetUIAnchor();
        isVisible = true;

        if (worldCanvas != null)
            worldCanvas.enabled = true;

        gameObject.SetActive(true);
        ApplyData(data);
    }

    public void Hide()
    {
        if (isVisible)
        {
            Debug.Log("[WorldPromptUI] UI hidden.");
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

        if (worldCanvas != null)
            worldCanvas.enabled = false;

        gameObject.SetActive(false);
    }

    private void ApplyData(InteractablePromptData data)
    {
        if (actionText != null)
            actionText.text = string.IsNullOrWhiteSpace(data.actionText) ? string.Empty : data.actionText;

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

        Debug.Log("[WorldPromptUI] UI data updated.");
    }

    private void AutoAssignReferencesIfMissing()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>(includeInactive: true);

        if (actionText == null)
            actionText = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
    }

    private void WarnIfMultiplePromptSystems()
    {
        WorldPromptUI[] promptUis = FindObjectsOfType<WorldPromptUI>(includeInactive: true);
        if (promptUis.Length > 1)
        {
            Debug.LogWarning("[WorldPromptUI] Multiple WorldPromptUI objects detected. Only one prompt system should be active.");
        }
    }
}
