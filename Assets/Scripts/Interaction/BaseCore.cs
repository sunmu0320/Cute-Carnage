using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaseCore : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHp = 200f;

    [Header("World HP Bar")]
    [SerializeField] private GameObject worldHpBarPrefab;
    [SerializeField] private Transform worldHpAnchor;
    [SerializeField] private Vector3 worldHpLocalOffset = new Vector3(0f, 2.5f, 1.5f);
    [SerializeField] private float worldHpScaleMultiplier = 1.8f;
    [SerializeField] private int worldHpCanvasSortingOrder = 10;
    [SerializeField] private bool showWorldHpText = true;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool hideWorldHpWhenFull = false;

    private float currentHp;
    private bool hasLoggedDestroyed;
    private GameObject worldHpBarInstance;
    private Image worldHpFillImage;
    private TextMeshProUGUI worldHpText;
    private WorldGatherBar worldGatherBar;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public bool IsDestroyed => currentHp <= 0f;

    private void Awake()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = maxHp;
        hasLoggedDestroyed = false;

        TrySpawnWorldHpBar();
        RefreshWorldHpBar();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHp = Mathf.Max(1f, maxHp);
        if (worldHpBarInstance != null)
        {
            RefreshWorldHpBar();
        }
    }
#endif

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDestroyed)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);
        RefreshWorldHpBar();

        if (currentHp > 0f || hasLoggedDestroyed)
        {
            return;
        }

        hasLoggedDestroyed = true;
        Debug.Log("BaseCore destroyed - Game Over", this);
    }

    private void TrySpawnWorldHpBar()
    {
        if (worldHpBarInstance != null)
        {
            return;
        }

        GameObject sourcePrefab = worldHpBarPrefab;
        if (sourcePrefab == null)
        {
            sourcePrefab = Resources.Load<GameObject>("UI/BaseCoreWorldHpBar");
        }

        if (sourcePrefab == null)
        {
            sourcePrefab = CreateFallbackWorldHpBar();
        }

        Transform parent = worldHpAnchor != null ? worldHpAnchor : transform;
        worldHpBarInstance = Instantiate(sourcePrefab, parent, false);
        worldHpBarInstance.name = $"{sourcePrefab.name}_BaseCoreHp";
        worldHpBarInstance.transform.localPosition = worldHpLocalOffset;
        worldHpBarInstance.transform.localRotation = Quaternion.identity;
        worldHpBarInstance.transform.localScale *= Mathf.Max(0.1f, worldHpScaleMultiplier);

        Canvas canvas = worldHpBarInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = worldHpCanvasSortingOrder;
        }

        worldGatherBar = worldHpBarInstance.GetComponent<WorldGatherBar>();
        if (worldGatherBar != null)
        {
            worldGatherBar.enabled = faceCamera;
        }

        CacheWorldHpComponents();
    }

    private GameObject CreateFallbackWorldHpBar()
    {
        GameObject root = new GameObject("BaseCoreWorldHpBar_Fallback", typeof(RectTransform), typeof(Canvas));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(220f, 30f);
        rootRect.localScale = new Vector3(0.03f, 0.03f, 0.03f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject bg = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(220f, 30f);
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);

        GameObject fill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(210f, 18f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.color = new Color(0.2f, 0.9f, 0.2f, 0.95f);

        GameObject text = new GameObject("BaseHpText", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(root.transform, false);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(260f, 32f);
        textRect.anchoredPosition = new Vector2(0f, 22f);
        TextMeshProUGUI tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.text = "Base HP 200 / 200";
        tmp.fontSize = 22f;
        tmp.fontWeight = FontWeight.Bold;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        return root;
    }

    private void CacheWorldHpComponents()
    {
        worldHpFillImage = null;
        worldHpText = null;

        if (worldHpBarInstance == null)
        {
            return;
        }

        Image[] allImages = worldHpBarInstance.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < allImages.Length; i++)
        {
            Image image = allImages[i];
            if (image != null && image.type == Image.Type.Filled)
            {
                worldHpFillImage = image;
                break;
            }
        }

        worldHpText = worldHpBarInstance.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void RefreshWorldHpBar()
    {
        if (worldHpBarInstance == null)
        {
            return;
        }

        float safeMaxHp = Mathf.Max(1f, maxHp);
        float fill = Mathf.Clamp01(currentHp / safeMaxHp);

        if (worldHpFillImage != null)
        {
            worldHpFillImage.fillAmount = fill;
        }

        if (worldHpText != null)
        {
            worldHpText.gameObject.SetActive(showWorldHpText);
            if (showWorldHpText)
            {
                worldHpText.text = $"Base HP {Mathf.RoundToInt(currentHp)} / {Mathf.RoundToInt(safeMaxHp)}";
            }
        }

        bool visible = !hideWorldHpWhenFull || currentHp < safeMaxHp;
        worldHpBarInstance.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (!faceCamera || worldHpBarInstance == null || (worldGatherBar != null && worldGatherBar.enabled))
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        worldHpBarInstance.transform.rotation = cam.transform.rotation;
    }
}
