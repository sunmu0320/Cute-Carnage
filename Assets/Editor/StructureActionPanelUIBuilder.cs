using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot structural builder for the shared Fence/Tower StructureActionPanel.
/// Renamed from FencePanelUIBuilder when Tower-specific generation (StatsSection,
/// EvolveButton) was added - the builder was never Fence-only, it has always
/// targeted the single shared StructureActionPanelUI, and the old name was
/// actively misleading once Tower got its own generated sections.
/// Run via Tools/UI Builders/Build Structure Action Panel. Idempotent:
/// re-running updates existing generated children in place instead of
/// duplicating them. Only touches the real StructureActionPanel instance
/// (gameObject named "StructureActionPanel"), never the dead NightRepairPanel
/// copy.
/// InstallButton is out of scope and expected to be gone (removed by design);
/// its old cost text (installCostText, formerly "InstallCost"/"UpgradeCost")
/// and the hand-made "RepairAmount"/"RepairCost" placeholders are deleted
/// here and replaced by the CostRoot structure below. Also wires the new
/// StructureActionPanelUI SerializeFields (subtitleText, tierBadgeText,
/// upgradeButtonText, repairAmountText, upgrade/repair Wood/ScrapCountText,
/// statsSection, damage/attackSpeed/rangeValueText, evolveButton,
/// evolveButtonText) to the objects it creates, via WireGeneratedFields.
/// </summary>
public static class StructureActionPanelUIBuilder
{
    private const string MenuPath = "Tools/UI Builders/Build Structure Action Panel";
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
                Debug.LogError("StructureActionPanelUIBuilder: could not find the active StructureActionPanelUI on a GameObject named 'StructureActionPanel'. Aborting without saving.");
                return;
            }

            SerializedObject so = new SerializedObject(panelUI);
            Transform panelRoot = panelUI.transform;
            Transform titleText = GetRef<TextMeshProUGUI>(so, "titleText")?.transform;
            Transform repairButton = GetRef<Button>(so, "repairButton")?.transform;
            Transform upgradeButton = GetRef<Button>(so, "upgradeButton")?.transform;
            Transform closeButton = GetRef<Button>(so, "closeButton")?.transform;
            Transform background = panelRoot.Find("Background");
            Transform hpSection = GetRef<GameObject>(so, "hpSection")?.transform;
            Transform dayActionsRoot = GetRef<GameObject>(so, "dayActionsRoot")?.transform;

            if (titleText == null || repairButton == null || upgradeButton == null || closeButton == null
                || background == null || hpSection == null || dayActionsRoot == null)
            {
                Debug.LogError("StructureActionPanelUIBuilder: one or more expected references (titleText/repairButton/upgradeButton/closeButton/Background/hpSection/dayActionsRoot) were missing. Aborting without saving to avoid a partial edit. Note: installButton is expected to be null now that it has been removed, and is intentionally not required.");
                return;
            }

            TextMeshProUGUI titleTmp = titleText.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = titleTmp.font;
            Material fontMaterial = titleTmp.fontSharedMaterial;

            // Readability only, no functional change. Named "HpValueText"
            // rather than "HpText" because a sibling static "HP" label
            // already owns that name (see HpSection children).
            TextMeshProUGUI hpValueText = GetRef<TextMeshProUGUI>(so, "hpText");
            if (hpValueText != null)
            {
                hpValueText.gameObject.name = "HpValueText";
            }

            // Hand-made wireframe reference text, never read by code. Superseded
            // by SubtitleText, which occupies the same spot with real content.
            DestroyChildIfPresent(panelRoot, "Description");

            StyleBackground(background);
            EnsureAccentBorder(panelRoot, background);
            EnsureSubtitleText(panelRoot, titleText, font, fontMaterial);
            EnsureTierBadge(panelRoot, titleText, font, fontMaterial);
            EnsureKeyLabel(closeButton, "ESC", font, fontMaterial);

            RemoveObsoletePlaceholders(so, repairButton);
            so.ApplyModifiedProperties();

            Transform repairActionRow = BuildButtonCostLayout(repairButton, font, fontMaterial);
            EnsureRepairAmountText(repairActionRow, font, fontMaterial);

            Transform upgradeActionRow = BuildButtonCostLayout(upgradeButton, font, fontMaterial);

            Transform statsSection = EnsureStatsSection(panelRoot, dayActionsRoot, font, fontMaterial);
            Transform evolveButton = EnsureEvolveButton(dayActionsRoot, upgradeButton, font, fontMaterial);

            WireGeneratedFields(so, panelRoot, repairButton, repairActionRow, upgradeButton, upgradeActionRow, statsSection, evolveButton);
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("StructureActionPanelUIBuilder: StructureActionPanel structure updated.");
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

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
    }

    // Wires the SerializeFields StructureActionPanelUI.cs added for Fence
    // Phase 1 (subtitleText, tierBadgeText, upgradeButtonText,
    // repairAmountText, upgrade/repair Wood/ScrapCountText) and Tower
    // Phase 1-3 (statsSection, damage/attackSpeed/rangeValueText,
    // evolveButton, evolveButtonText) to the objects this builder just
    // created. Safe to call every run - re-wiring an already-correct
    // reference is a no-op.
    private static void WireGeneratedFields(
        SerializedObject so,
        Transform panelRoot,
        Transform repairButton,
        Transform repairActionRow,
        Transform upgradeButton,
        Transform upgradeActionRow,
        Transform statsSection,
        Transform evolveButton)
    {
        SetRef(so, "subtitleText", panelRoot.Find("SubtitleText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "tierBadgeText", panelRoot.Find("TierBadge/TierBadgeFill/TierBadgeText")?.GetComponent<TextMeshProUGUI>());

        SetRef(so, "repairAmountText", repairActionRow.Find("RepairAmountText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "repairWoodCountText", repairButton.Find("CostRoot/WoodCost/WoodCountText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "repairScrapCountText", repairButton.Find("CostRoot/ScrapCost/ScrapCountText")?.GetComponent<TextMeshProUGUI>());

        SetRef(so, "upgradeButtonText", upgradeActionRow.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "upgradeWoodCountText", upgradeButton.Find("CostRoot/WoodCost/WoodCountText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "upgradeScrapCountText", upgradeButton.Find("CostRoot/ScrapCost/ScrapCountText")?.GetComponent<TextMeshProUGUI>());

        SetRef(so, "statsSection", statsSection.gameObject);
        SetRef(so, "damageValueText", statsSection.Find("DamageRow/ValueText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "attackSpeedValueText", statsSection.Find("AttackSpeedRow/ValueText")?.GetComponent<TextMeshProUGUI>());
        SetRef(so, "rangeValueText", statsSection.Find("RangeRow/ValueText")?.GetComponent<TextMeshProUGUI>());

        SetRef(so, "evolveButton", evolveButton.GetComponent<Button>());
        SetRef(so, "evolveButtonText", evolveButton.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>());
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
        tmp.text = string.Empty; // set at runtime by StructureActionPanelUI.RefreshFence()
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
        tmp.text = string.Empty; // set at runtime by StructureActionPanelUI.RefreshFence()
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
    // Returns the ActionRow so callers can append button-specific extras
    // (e.g. RepairAmountText) after KeyLabel + Text(TMP).
    private static Transform BuildButtonCostLayout(Transform button, TMP_FontAsset font, Material fontMaterial)
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

        return actionRow;
    }

    // Existing "Text (TMP)" is normally full-stretch (fills the whole
    // button); that's incompatible with a layout group, so it's pinned to a
    // fixed size here. "KeyLabel" is only reparented if the button has one -
    // UpgradeButton currently doesn't (Install's KeyLabel was never ported
    // over when InstallButton was removed). Sibling order is forced to
    // [KeyLabel, Text (TMP)] every run so the keycap always reads first.
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

        Transform keyLabel = button.Find("KeyLabel") ?? rowGo.transform.Find("KeyLabel");
        if (keyLabel != null)
        {
            keyLabel.SetParent(rowGo.transform, false);
            keyLabel.SetSiblingIndex(0);
        }

        Transform textLabel = button.Find("Text (TMP)") ?? rowGo.transform.Find("Text (TMP)");
        if (textLabel != null)
        {
            FixNonStretchedRect(textLabel.GetComponent<RectTransform>(), new Vector2(74f, 20f));
            textLabel.SetParent(rowGo.transform, false);
            textLabel.SetSiblingIndex(keyLabel != null ? 1 : 0);
        }

        return rowGo.transform;
    }

    // "+0 HP"-style repair amount, placed after KeyLabel + "Repair" inside
    // ActionRow. Initial text is a placeholder; StructureActionPanelUI.RefreshFence()
    // overwrites it at runtime with the installed fence's actual repair amount.
    private static void EnsureRepairAmountText(Transform actionRow, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject go = EnsureChild(actionRow, "RepairAmountText", typeof(TextMeshProUGUI));
        FixNonStretchedRect(go.GetComponent<RectTransform>(), new Vector2(50f, 20f));
        go.transform.SetSiblingIndex(actionRow.childCount - 1);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.text = "+0 HP";
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
        tmp.text = "0"; // set at runtime by StructureActionPanelUI.RefreshFence()
    }

    // Tower-only stats block (Damage/Atk Speed/Range), placed directly above
    // DayActionsRoot. Fence hides this whole section (RefreshFence sets
    // statsSection inactive) since a Fence has no attack stats.
    private static Transform EnsureStatsSection(Transform panelRoot, Transform dayActionsRoot, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject sectionGo = EnsureChild(panelRoot, "StatsSection", typeof(VerticalLayoutGroup));
        // Always re-anchored directly above DayActionsRoot rather than
        // relative to HpSection - keeps this idempotent regardless of
        // exactly where HpSection sits in the sibling order.
        sectionGo.transform.SetSiblingIndex(dayActionsRoot.GetSiblingIndex());

        RectTransform sectionRect = sectionGo.GetComponent<RectTransform>();
        FixNonStretchedRect(sectionRect, new Vector2(150f, 58f));
        sectionRect.anchoredPosition = new Vector2(0f, 14f);

        VerticalLayoutGroup vlg = sectionGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 3f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        EnsureStatRow(sectionGo.transform, "DamageRow", "DAMAGE", "0", font, fontMaterial);
        EnsureStatRow(sectionGo.transform, "AttackSpeedRow", "ATK SPEED", "0/s", font, fontMaterial);
        EnsureStatRow(sectionGo.transform, "RangeRow", "RANGE", "0m", font, fontMaterial);

        return sectionGo.transform;
    }

    // "LABEL  value"-style row: accent-colored label, white value. Value text
    // is a placeholder; StructureActionPanelUI.Refresh() overwrites it at
    // runtime with the installed tower's actual stat (or "-" when empty).
    private static void EnsureStatRow(Transform parent, string rowName, string labelText, string initialValue, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject rowGo = EnsureChild(parent, rowName, typeof(HorizontalLayoutGroup));
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(140f, 16f);

        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        GameObject labelGo = EnsureChild(rowGo.transform, "Label", typeof(TextMeshProUGUI));
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(70f, 16f);

        TextMeshProUGUI labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
        labelTmp.font = font;
        labelTmp.fontSharedMaterial = fontMaterial;
        labelTmp.fontSize = 11f;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = AccentColor;
        labelTmp.text = labelText;

        GameObject valueGo = EnsureChild(rowGo.transform, "ValueText", typeof(TextMeshProUGUI));
        RectTransform valueRect = valueGo.GetComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(60f, 16f);

        TextMeshProUGUI valueTmp = valueGo.GetComponent<TextMeshProUGUI>();
        valueTmp.font = font;
        valueTmp.fontSharedMaterial = fontMaterial;
        valueTmp.fontSize = 11f;
        valueTmp.alignment = TextAlignmentOptions.MidlineLeft;
        valueTmp.color = Color.white;
        valueTmp.text = initialValue; // set at runtime by StructureActionPanelUI.Refresh()
    }

    // Tower-only evolve button, sibling to UpgradeButton/RepairActionRoot
    // inside DayActionsRoot's HorizontalLayoutGroup. Styled to match
    // UpgradeButton (same sprite/size) rather than reusing
    // BuildButtonCostLayout, since Evolve has no Wood/Scrap cost to show -
    // it's gated on tower tier, not resources.
    private static Transform EnsureEvolveButton(Transform dayActionsRoot, Transform upgradeButton, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject evolveGo = EnsureChild(dayActionsRoot, "EvolveButton", typeof(Image), typeof(Button));

        RectTransform evolveRect = evolveGo.GetComponent<RectTransform>();
        RectTransform upgradeRect = upgradeButton.GetComponent<RectTransform>();
        evolveRect.anchorMin = upgradeRect.anchorMin;
        evolveRect.anchorMax = upgradeRect.anchorMax;
        evolveRect.pivot = upgradeRect.pivot;
        evolveRect.sizeDelta = upgradeRect.sizeDelta;
        evolveRect.anchoredPosition = Vector2.zero; // DayActionsRoot's HorizontalLayoutGroup repositions this

        Image upgradeImage = upgradeButton.GetComponent<Image>();
        Image evolveImage = evolveGo.GetComponent<Image>();
        evolveImage.sprite = upgradeImage.sprite;
        evolveImage.type = upgradeImage.type;
        evolveImage.color = Color.white;

        Button evolveButtonComponent = evolveGo.GetComponent<Button>();
        evolveButtonComponent.targetGraphic = evolveImage;

        GameObject textGo = EnsureChild(evolveGo.transform, "Text (TMP)", typeof(TextMeshProUGUI));
        FixNonStretchedRect(textGo.GetComponent<RectTransform>(), new Vector2(110f, 20f));

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 12.3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = "EVOLVE"; // set at runtime by StructureActionPanelUI.Refresh()

        return evolveGo.transform;
    }
}
