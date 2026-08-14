using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot structural builder for the Fence/Tower StructureActionPanel.
/// Run via Tools/UI Builders/Build Fence Panel. Idempotent: re-running
/// updates existing generated children in place instead of duplicating them.
/// Only touches the real StructureActionPanel instance (gameObject named
/// "StructureActionPanel"), never the dead NightRepairPanel copy.
/// </summary>
public static class FencePanelUIBuilder
{
    private const string MenuPath = "Tools/UI Builders/Build Fence Panel";
    private const string PrefabPath = "Assets/Prefabs/UI/UIRoot.prefab";
    private const float BorderThickness = 2f;

    private static readonly Color BackgroundColor = new Color(0x1a / 255f, 0x1a / 255f, 0x1a / 255f, 1f);
    private static readonly Color AccentColor = new Color(0xff / 255f, 0x8c / 255f, 0x1a / 255f, 1f);

    [MenuItem(MenuPath)]
    private static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StructureActionPanelUI panelUI = root.GetComponentsInChildren<StructureActionPanelUI>(true)
                .FirstOrDefault(p => p.gameObject.name == "StructureActionPanel");

            if (panelUI == null)
            {
                Debug.LogError("FencePanelUIBuilder: could not find the active StructureActionPanelUI on a GameObject named 'StructureActionPanel'. Aborting without saving.");
                return;
            }

            SerializedObject so = new SerializedObject(panelUI);
            Transform panelRoot = panelUI.transform;
            Transform titleText = GetRef<TextMeshProUGUI>(so, "titleText")?.transform;
            Transform installButton = GetRef<Button>(so, "installButton")?.transform;
            Transform repairButton = GetRef<Button>(so, "repairButton")?.transform;
            Transform closeButton = GetRef<Button>(so, "closeButton")?.transform;
            Transform background = panelRoot.Find("Background");

            if (titleText == null || installButton == null || repairButton == null || closeButton == null || background == null)
            {
                Debug.LogError("FencePanelUIBuilder: one or more expected references (titleText/installButton/repairButton/closeButton/Background) were missing. Aborting without saving to avoid a partial edit.");
                return;
            }

            TextMeshProUGUI titleTmp = titleText.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = titleTmp.font;
            Material fontMaterial = titleTmp.fontSharedMaterial;

            StyleBackground(background);
            EnsureAccentBorder(panelRoot, background);
            EnsureSubtitleText(panelRoot, titleText, font, fontMaterial);
            EnsureTierBadge(panelRoot, titleText, font, fontMaterial);
            EnsureKeyLabel(installButton, "E", font, fontMaterial);
            EnsureKeyLabel(repairButton, "R", font, fontMaterial);
            EnsureKeyLabel(closeButton, "ESC", font, fontMaterial);
            EnsureRepairAmountText(repairButton, font, fontMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FencePanelUIBuilder: StructureActionPanel structure updated.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetRef<T>(SerializedObject so, string propertyName) where T : Object
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        return prop != null ? prop.objectReferenceValue as T : null;
    }

    private static GameObject EnsureChild(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StyleBackground(Transform background)
    {
        Image bgImage = background.GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = BackgroundColor;
        }
    }

    // Flat-color border trick: an accent-colored rect sized a couple of
    // pixels larger than Background, placed directly behind it, so the
    // extra margin reads as a border ring. Avoids needing a sliced sprite.
    private static void EnsureAccentBorder(Transform panelRoot, Transform background)
    {
        RectTransform bgRect = background.GetComponent<RectTransform>();
        GameObject borderGo = EnsureChild(panelRoot, "BackgroundBorder", typeof(Image));
        borderGo.transform.SetSiblingIndex(background.GetSiblingIndex());

        RectTransform borderRect = borderGo.GetComponent<RectTransform>();
        borderRect.anchorMin = bgRect.anchorMin;
        borderRect.anchorMax = bgRect.anchorMax;
        borderRect.pivot = bgRect.pivot;
        borderRect.anchoredPosition = bgRect.anchoredPosition;
        borderRect.sizeDelta = bgRect.sizeDelta + new Vector2(BorderThickness, BorderThickness) * 2f;

        Image borderImage = borderGo.GetComponent<Image>();
        borderImage.color = AccentColor;
        borderImage.raycastTarget = false;
    }

    private static void EnsureSubtitleText(Transform panelRoot, Transform titleText, TMP_FontAsset font, Material fontMaterial)
    {
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        GameObject go = EnsureChild(panelRoot, "SubtitleText", typeof(TextMeshProUGUI));

        RectTransform subRect = go.GetComponent<RectTransform>();
        subRect.anchorMin = titleRect.anchorMin;
        subRect.anchorMax = titleRect.anchorMax;
        subRect.pivot = titleRect.pivot;
        subRect.sizeDelta = new Vector2(titleRect.sizeDelta.x + 40f, 14f);
        subRect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, -16f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = AccentColor;
        tmp.text = string.Empty; // set at runtime in Phase 2
    }

    private static void EnsureTierBadge(Transform panelRoot, Transform titleText, TMP_FontAsset font, Material fontMaterial)
    {
        RectTransform titleRect = titleText.GetComponent<RectTransform>();

        GameObject badgeGo = EnsureChild(panelRoot, "TierBadge", typeof(Image));
        RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
        badgeRect.anchorMin = titleRect.anchorMin;
        badgeRect.anchorMax = titleRect.anchorMax;
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.sizeDelta = new Vector2(28f, 16f);
        badgeRect.anchoredPosition = titleRect.anchoredPosition + new Vector2((titleRect.sizeDelta.x / 2f) + 8f, 0f);

        Image badgeImage = badgeGo.GetComponent<Image>();
        badgeImage.color = AccentColor;
        badgeImage.raycastTarget = false;

        GameObject fillGo = EnsureChild(badgeGo.transform, "TierBadgeFill", typeof(Image));
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
        fillRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);

        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = BackgroundColor;
        fillImage.raycastTarget = false;

        GameObject textGo = EnsureChild(fillGo.transform, "TierBadgeText", typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = string.Empty; // set at runtime in Phase 2, e.g. "T{fence.CurrentTierNumber}"
    }

    // Small keycap-style label docked to the left edge of a button:
    // an accent-colored rect with a dark, bold, centered key glyph.
    private static void EnsureKeyLabel(Transform button, string key, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject labelGo = EnsureChild(button, "KeyLabel", typeof(Image));
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(20f, 14f);
        labelRect.anchoredPosition = new Vector2(4f, 0f);

        Image labelImage = labelGo.GetComponent<Image>();
        labelImage.color = AccentColor;
        labelImage.raycastTarget = false;

        GameObject textGo = EnsureChild(labelGo.transform, "KeyLabelText", typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 10f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = BackgroundColor;
        tmp.text = key;
    }

    private static void EnsureRepairAmountText(Transform repairButton, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject go = EnsureChild(repairButton, "RepairAmountText", typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(26f, 0f); // leaves room for KeyLabel on the left
        rect.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        tmp.color = Color.white;
        tmp.text = string.Empty; // set at runtime in Phase 2, e.g. "+20 HP"
    }
}
