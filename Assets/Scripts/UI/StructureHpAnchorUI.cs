using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StructureHpAnchorUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour hpSourceComponent;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform playerTransform;

    [Header("Visibility")]
    [SerializeField] private float visibleDistance = 12f;
    [SerializeField] private bool showWhenDamaged = true;
    [SerializeField] private bool showWhenPlayerNearby = true;
    [SerializeField] private bool hideWhenDestroyed = true;

    [Header("Screen Position")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 56f);
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private RectTransform rootRect;

    [Header("Bar")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private bool showHpText = true;
    [SerializeField] private string hpLabel = "HP";

    private IStructureHpSource hpSource;
    private CanvasGroup canvasGroup;
    private bool isVisible;

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        ResolveHpSource();
        if (hpSource == null)
        {
            SetVisible(false);
            return;
        }

        if (hideWhenDestroyed && hpSource.IsDestroyed)
        {
            SetVisible(false);
            return;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            SetVisible(false);
            return;
        }

        Transform worldTarget = targetTransform != null ? targetTransform : hpSource.HpAnchorTransform;
        if (worldTarget == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPos = worldTarget.position + worldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        bool isOnScreen =
            screenPos.z > 0f &&
            screenPos.x >= 0f &&
            screenPos.x <= Screen.width &&
            screenPos.y >= 0f &&
            screenPos.y <= Screen.height;

        float safeMaxHp = Mathf.Max(1f, hpSource.MaxHp);
        float fill = Mathf.Clamp01(hpSource.CurrentHp / safeMaxHp);
        bool isDamaged = hpSource.CurrentHp < safeMaxHp - 0.01f;
        bool isNearby = IsPlayerNearby(worldTarget.position);

        bool shouldShow = isOnScreen && ((showWhenDamaged && isDamaged) || (showWhenPlayerNearby && isNearby));
        SetVisible(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = fill;
        }

        if (hpText != null)
        {
            hpText.gameObject.SetActive(showHpText);
            if (showHpText)
            {
                hpText.text = $"{hpLabel} {Mathf.RoundToInt(hpSource.CurrentHp)} / {Mathf.RoundToInt(safeMaxHp)}";
            }
        }

        PlaceAtScreenPosition(screenPos, cam);
    }

    private void ResolveReferences()
    {
        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].type == Image.Type.Filled)
                {
                    fillImage = images[i];
                    break;
                }
            }
        }

        if (hpText == null)
        {
            hpText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ResolveHpSource();
    }

    private void ResolveHpSource()
    {
        hpSource = hpSourceComponent as IStructureHpSource;
    }

    private bool IsPlayerNearby(Vector3 worldPos)
    {
        if (playerTransform == null)
        {
            return false;
        }

        Vector3 a = playerTransform.position;
        Vector3 b = worldPos;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= Mathf.Max(0f, visibleDistance);
    }

    private void PlaceAtScreenPosition(Vector3 screenPos, Camera cam)
    {
        if (rootRect == null)
        {
            return;
        }

        Vector2 adjusted = new Vector2(screenPos.x, screenPos.y) + screenOffset;
        if (parentCanvas == null)
        {
            rootRect.position = adjusted;
            return;
        }

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            rootRect.position = adjusted;
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, adjusted, eventCamera, out Vector2 localPoint))
        {
            rootRect.anchoredPosition = localPoint;
        }
    }

    private void SetVisible(bool value)
    {
        if (isVisible == value)
        {
            return;
        }

        isVisible = value;
        canvasGroup.alpha = value ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
