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
        gameObject.SetActive(true);
        SetGraphicsVisible(true);
        UpdateScreenSpacePosition(forceHideWhenInvalid: false);
    }

    public void HideInstant()
    {
        SetProgress(0f);
        SetGraphicsVisible(false);
        gameObject.SetActive(false);
    }

    public void SetProgress(float normalized)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(normalized);
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
        barRoot.localScale = Vector3.one;
        SetGraphicsVisible(true);
    }

    void SetGraphicsVisible(bool visible)
    {
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
