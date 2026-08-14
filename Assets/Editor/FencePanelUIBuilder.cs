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
/// InstallButton is out of scope and expected to be gone (removed by design);
/// its old cost text (installCostText, formerly "InstallCost"/"UpgradeCost")
/// and the hand-made "RepairAmount"/"RepairCost" placeholders are deleted
/// here and replaced by the CostRoot structure below.
/// </summary>
public static class FencePanelUIBuilder
{
    private const string MenuPath = "Tools/UI Builders/Build Fence Panel";
    private const string PrefabPath = "Assets/Prefabs/UI/UIRoot.prefab";
    private const float BorderThickness = 2f;

    private static readonly Color BackgroundColor = new Color(0x1a / 255f, 0x1a / 255f, 0x1a / 255f, 1f);
    private static readonly Color AccentColor = new Color(0xff / 255f, 0x8c / 255f, 0x1a / 255f, 1f);
    private static readonly Color WoodColor = new Color(0x8B / 255f, 0x5A / 255f, 0x2B / 255f, 1f);
    private static readonly Color ScrapColor = new Color(0x88 / 255f, 0x88 / 255f, 0x88 / 255f, 1f);

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
            Transform repairButton = GetRef<Button>(so, "repairButton")?.transform;
            Transform upgradeButton = GetRef<Button>(so, "upgradeButton")?.transform;
            Transform closeButton = GetRef<Button>(so, "closeButton")?.transform;
            Transform background = panelRoot.Find("Background");

            if (titleText == null || repairButton == null || upgradeButton == null || closeButton == null || background == null)
            {
                Debug.LogError("FencePanelUIBuilder: one or more expected references (titleText/repairButton/upgradeButton/closeButton/Background) were missing. Aborting without saving to avoid a partial edit. Note: installButton is expected to be null now that it has been removed, and is intentionally not required.");
                return;
            }

            TextMeshProUGUI titleTmp = titleText.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = titleTmp.font;
            Material fontMaterial = titleTmp.fontSharedMaterial;

            StyleBackground(background);
            EnsureAccentBorder(panelRoot, background);
            EnsureSubtitleText(panelRoot, titleText, font, fontMaterial);
            EnsureTierBadge(panelRoot, titleText, font, fontMaterial);
            EnsureKeyLabel(closeButton, "ESC", font, fontMaterial);

            RemoveObsoletePlaceholders(so, repairButton);
            so.ApplyModifiedProperties();

            BuildButtonCostLayout(repairButton, font, fontMaterial);
            BuildButtonCostLayout(upgradeButton, font, fontMaterial);

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

    private static void DestroyChildIfPresent(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void FixNonStretchedRect(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
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

    // Deletes the hand-made cost/amount placeholders that predate CostRoot:
    // RepairButton's "RepairAmount"/"RepairCost" texts, and the object
    // installCostText points to (originally "InstallCost", later relocated
    // into UpgradeButton and renamed "UpgradeCost"). Confirmed disposable -
    // no functional wiring, position references only.
    private static void RemoveObsoletePlaceholders(SerializedObject so, Transform repairButton)
    {
        DestroyChildIfPresent(repairButton, "RepairAmount");
        DestroyChildIfPresent(repairButton, "RepairCost");

        SerializedProperty installCostProp = so.FindProperty("installCostText");
        if (installCostProp != null && installCostProp.objectReferenceValue != null)
        {
            GameObject obsolete = ((Component)installCostProp.objectReferenceValue).gameObject;
            Object.DestroyImmediate(obsolete);
            installCostProp.objectReferenceValue = null;
        }
    }

    // Wraps a button's existing "Text (TMP)" label (and "KeyLabel", if the
    // button has one) into an ActionRow, adds a Wood/Scrap CostRoot below
    // it, and stacks the two with a VerticalLayoutGroup on the button itself.
    private static void BuildButtonCostLayout(Transform button, TMP_FontAsset font, Material fontMaterial)
    {
        Transform actionRow = EnsureActionRow(button);
        Transform costRoot = EnsureCostRoot(button, font, fontMaterial);

        actionRow.SetSiblingIndex(0);
        costRoot.SetSiblingIndex(1);

        VerticalLayoutGroup vlg = button.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = button.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
    }

    // Existing "Text (TMP)" is normally full-stretch (fills the whole
    // button); that's incompatible with a layout group, so it's pinned to a
    // fixed size here. "KeyLabel" is only reparented if the button has one -
    // UpgradeButton currently doesn't (Install's KeyLabel was never ported
    // over when InstallButton was removed).
    private static Transform EnsureActionRow(Transform button)
    {
        GameObject rowGo = EnsureChild(button, "ActionRow", typeof(HorizontalLayoutGroup));
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(100f, 20f);

        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Transform textLabel = button.Find("Text (TMP)") ?? rowGo.transform.Find("Text (TMP)");
        if (textLabel != null)
        {
            FixNonStretchedRect(textLabel.GetComponent<RectTransform>(), new Vector2(74f, 20f));
            textLabel.SetParent(rowGo.transform, false);
        }

        Transform keyLabel = button.Find("KeyLabel") ?? rowGo.transform.Find("KeyLabel");
        if (keyLabel != null)
        {
            keyLabel.SetParent(rowGo.transform, false);
        }

        return rowGo.transform;
    }

    private static Transform EnsureCostRoot(Transform button, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject costRootGo = EnsureChild(button, "CostRoot", typeof(HorizontalLayoutGroup));
        RectTransform costRootRect = costRootGo.GetComponent<RectTransform>();
        costRootRect.sizeDelta = new Vector2(90f, 16f);

        HorizontalLayoutGroup costRootLayout = costRootGo.GetComponent<HorizontalLayoutGroup>();
        costRootLayout.spacing = 8f;
        costRootLayout.childAlignment = TextAnchor.MiddleCenter;
        costRootLayout.childForceExpandWidth = false;
        costRootLayout.childForceExpandHeight = false;
        costRootLayout.childControlWidth = false;
        costRootLayout.childControlHeight = false;

        EnsureCostEntry(costRootGo.transform, "WoodCost", "WoodIcon", "WoodCountText", WoodColor, font, fontMaterial);
        EnsureCostEntry(costRootGo.transform, "ScrapCost", "ScrapIcon", "ScrapCountText", ScrapColor, font, fontMaterial);

        return costRootGo.transform;
    }

    private static void EnsureCostEntry(Transform parent, string rowName, string iconName, string textName, Color iconColor, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject rowGo = EnsureChild(parent, rowName, typeof(HorizontalLayoutGroup));
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(34f, 14f);

        HorizontalLayoutGroup rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 2f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;

        GameObject iconGo = EnsureChild(rowGo.transform, iconName, typeof(Image));
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(12f, 12f);
        Image icon = iconGo.GetComponent<Image>();
        icon.color = iconColor;
        icon.raycastTarget = false;

        GameObject textGo = EnsureChild(rowGo.transform, textName, typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(18f, 14f);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.text = "0"; // set at runtime in Phase 2
    }
}
