using UnityEngine;
using UnityEngine.UI;

public class WorldGatherBar : MonoBehaviour
{
    [SerializeField] Image fillImage;
    [SerializeField] RectTransform barRoot;

    RectTransform overlayCanvasRect;
    Transform followTarget;
    RectTransform rectTransform;
    Graphic[] cachedGraphics;
    bool graphicsVisibilityInitialized;
    bool graphicsVisible;
    bool progressInitialized;
    float lastProgress;

    const float ProgressEpsilon = 0.0001f;

    void Awake()
    {
        rectTransform = transform as RectTransform;
        if (barRoot == null)
            barRoot = rectTransform;
        cachedGraphics = GetComponentsInChildren<Graphic>(true);
        HideInstant();
    }

    public void Initialize(RectTransform canvasRect, Transform target)
    {
        overlayCanvasRect = canvasRect;
        followTarget = target;

        if (barRoot != null)
            barRoot.localScale = Vector3.one;

        UpdateScreenSpacePosition(forceHideWhenInvalid: false);
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetGraphicsVisible(true);
        UpdateScreenSpacePosition(forceHideWhenInvalid: false);
    }

    public void HideInstant()
    {
        if (!gameObject.activeSelf
            && graphicsVisibilityInitialized && !graphicsVisible
            && progressInitialized && Mathf.Abs(lastProgress) <= ProgressEpsilon)
        {
            return;
        }

        SetProgress(0f);
        SetGraphicsVisible(false);
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void SetProgress(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        if (progressInitialized && Mathf.Abs(lastProgress - clamped) <= ProgressEpsilon)
            return;

        if (fillImage != null)
            fillImage.fillAmount = clamped;

        lastProgress = clamped;
        progressInitialized = true;
    }

    void LateUpdate()
    {
        UpdateScreenSpacePosition(forceHideWhenInvalid: true);
    }

    void UpdateScreenSpacePosition(bool forceHideWhenInvalid)
    {
        if (!gameObject.activeInHierarchy || overlayCanvasRect == null || barRoot == null)
            return;

        Camera cam = Camera.main;
        if (cam == null || followTarget == null)
        {
            if (forceHideWhenInvalid)
                SetGraphicsVisible(false);
            return;
        }

        Vector3 screenPoint = cam.WorldToScreenPoint(followTarget.position);
        if (screenPoint.z <= 0f)
        {
            SetGraphicsVisible(false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayCanvasRect, screenPoint, null, out Vector2 localPoint))
        {
            SetGraphicsVisible(false);
            return;
        }

        barRoot.anchoredPosition = localPoint;
        SetGraphicsVisible(true);
    }

    void SetGraphicsVisible(bool visible)
    {
        if (graphicsVisibilityInitialized && graphicsVisible == visible)
            return;

        graphicsVisible = visible;
        graphicsVisibilityInitialized = true;

        if (cachedGraphics == null)
            return;

        for (int i = 0; i < cachedGraphics.Length; i++)
        {
            Graphic graphic = cachedGraphics[i];
            if (graphic != null)
                graphic.enabled = visible;
        }
    }
}
