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
            Transform backgroundBorder = panelRoot.Find("BackgroundBorder");
            Transform subtitleText = EnsureSubtitleText(so, panelRoot, titleText, font, fontMaterial);
            Transform tierBadge = EnsureTierBadge(so, panelRoot, font, fontMaterial);
            EnsureKeyLabel(closeButton, "ESC", font, fontMaterial);

            RemoveObsoletePlaceholders(so, repairButton);
            so.ApplyModifiedProperties();

            Transform repairActionRow = BuildButtonCostLayout(repairButton, font, fontMaterial);
            EnsureRepairAmountText(repairActionRow, font, fontMaterial);

            Transform upgradeActionRow = BuildButtonCostLayout(upgradeButton, font, fontMaterial);

            Transform statsSection = EnsureStatsSection(so, panelRoot, font, fontMaterial);
            Transform evolveButton = EnsureEvolveButton(so, panelRoot, upgradeButton, font, fontMaterial);

            // Structural pass: fold everything except Background/
            // BackgroundBorder/CloseButton into one ContentRoot
            // (VerticalLayoutGroup) so sections stack by their real height
            // instead of colliding at hand-picked anchoredPositions - see
            // class doc comment for why those three stay outside it.
            Transform contentRoot = EnsureContentRoot(panelRoot, background);

            Transform iconRoot = panelRoot.Find("IconRoot") ?? contentRoot.Find("HeaderRow/IconRoot");
            if (iconRoot != null)
            {
                // Confirmed with design: IconRoot's Canvas/CanvasScaler/
                // GraphicRaycaster were a stray oversized RectTransform, not
                // an intentional 3D-preview overlay. Strip them so it's a
                // plain child Image inside HeaderRow.
                StripIconRootOverlayComponents(iconRoot);
            }
            else
            {
                Debug.LogWarning("StructureActionPanelUIBuilder: IconRoot not found - skipping header icon slot.");
            }

            Transform headerRow = EnsureHeaderRow(contentRoot, iconRoot, titleText, subtitleText, tierBadge);

            hpSection.SetParent(contentRoot, false);
            statsSection.SetParent(contentRoot, false);
            evolveButton.SetParent(contentRoot, false);
            EnsureFullWidthLayoutElement(evolveButton, upgradeButton.GetComponent<RectTransform>().sizeDelta.x);
            dayActionsRoot.SetParent(contentRoot, false);

            // Bug 3: HpSection/StatsSection/DayActionsRoot still carry their
            // old flat-layout sizeDelta.y (100, 100 again for
            // DayActionsRoot) from before this restructure. ContentRoot's
            // childControlHeight=false means it just stacks children at
            // whatever height they already report, so those stale values
            // reserve far more vertical space than the actual content needs.
            // A ContentSizeFitter makes each section self-report its real
            // height instead.
            EnsureVerticalContentSizeFitter(hpSection);
            EnsureVerticalContentSizeFitter(statsSection);
            EnsureVerticalContentSizeFitter(dayActionsRoot);

            headerRow.SetSiblingIndex(0);
            hpSection.SetSiblingIndex(1);
            statsSection.SetSiblingIndex(2);
            evolveButton.SetSiblingIndex(3);
            dayActionsRoot.SetSiblingIndex(4);

            closeButton.SetAsLastSibling(); // always on top, outside ContentRoot's flow

            WireGeneratedFields(so, subtitleText, tierBadge, repairButton, repairActionRow, upgradeButton, upgradeActionRow, statsSection, evolveButton);
            WireBackgroundSyncFields(so, contentRoot, background, backgroundBorder);
            so.ApplyModifiedProperties();

            // PrefabUtility.LoadPrefabContents runs outside a live scene's
            // update loop, so LayoutGroups don't get the usual per-frame
            // deferred rebuild - without forcing one, whatever partial/stale
            // layout state happened to exist gets baked into the saved
            // prefab as-is (this is what produced the sizeDelta.x=0 on
            // HeaderRow/EvolveButton fixed above). Forcing it here once,
            // synchronously, makes sure what gets saved is converged.
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot.GetComponent<RectTransform>());

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
    // reference is a no-op. Takes subtitleText/tierBadge directly (rather
    // than Find-ing them under panelRoot) since both now live nested under
    // ContentRoot/HeaderRow, past what a simple Find by name would reach.
    private static void WireGeneratedFields(
        SerializedObject so,
        Transform subtitleText,
        Transform tierBadge,
        Transform repairButton,
        Transform repairActionRow,
        Transform upgradeButton,
        Transform upgradeActionRow,
        Transform statsSection,
        Transform evolveButton)
    {
        SetRef(so, "subtitleText", subtitleText.GetComponent<TextMeshProUGUI>());
        SetRef(so, "tierBadgeText", tierBadge.Find("TierBadgeFill/TierBadgeText")?.GetComponent<TextMeshProUGUI>());

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

    // Wires the Background-sync SerializeFields (contentRoot, background,
    // backgroundBorder) StructureActionPanelUI.SyncBackgroundSize() reads at
    // runtime. Separate from WireGeneratedFields since it's called with
    // objects that already existed pre-restructure (Background/
    // BackgroundBorder), not just newly-generated ones.
    private static void WireBackgroundSyncFields(SerializedObject so, Transform contentRoot, Transform background, Transform backgroundBorder)
    {
        SetRef(so, "contentRoot", contentRoot.GetComponent<RectTransform>());
        SetRef(so, "background", background.GetComponent<RectTransform>());
        SetRef(so, "backgroundBorder", backgroundBorder != null ? backgroundBorder.GetComponent<RectTransform>() : null);
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

    // Reparent-safe: locates an already-created SubtitleText via the
    // subtitleText SerializeField (an object reference, so it resolves
    // regardless of where HeaderRow/TitleStack has since moved it) instead
    // of Find-ing it by a fixed parent. Only falls back to creating fresh
    // under fallbackParent on a true first run. Position is no longer
    // computed relative to titleText - once inside TitleStack
    // (VerticalLayoutGroup), that's governed by the stack instead.
    private static Transform EnsureSubtitleText(SerializedObject so, Transform fallbackParent, Transform titleText, TMP_FontAsset font, Material fontMaterial)
    {
        TextMeshProUGUI existing = GetRef<TextMeshProUGUI>(so, "subtitleText");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("SubtitleText", typeof(TextMeshProUGUI));
            go.transform.SetParent(fallbackParent, false);
        }

        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        RectTransform subRect = go.GetComponent<RectTransform>();
        subRect.sizeDelta = new Vector2(titleRect.sizeDelta.x + 40f, 14f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = AccentColor;
        tmp.text = string.Empty; // set at runtime by StructureActionPanelUI.RefreshFence()

        return go.transform;
    }

    // Reparent-safe like EnsureSubtitleText above: derives the existing
    // TierBadge root by walking up from the tierBadgeText SerializeField
    // (TierBadgeText -> TierBadgeFill -> TierBadge) rather than Find-ing it
    // by a fixed parent, so it resolves regardless of HeaderRow having
    // already moved it. Position is no longer computed relative to
    // titleText - once inside HeaderRow, the HorizontalLayoutGroup places it.
    private static Transform EnsureTierBadge(SerializedObject so, Transform fallbackParent, TMP_FontAsset font, Material fontMaterial)
    {
        TextMeshProUGUI existingText = GetRef<TextMeshProUGUI>(so, "tierBadgeText");
        Transform existingRoot = existingText != null ? existingText.transform.parent?.parent : null;

        GameObject badgeGo;
        if (existingRoot != null && existingRoot.name == "TierBadge")
        {
            badgeGo = existingRoot.gameObject;
        }
        else
        {
            badgeGo = new GameObject("TierBadge", typeof(Image));
            badgeGo.transform.SetParent(fallbackParent, false);
        }

        RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(28f, 16f);

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

        return badgeGo.transform;
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

    // Tower-only stats block (Damage/Atk Speed/Range). Reparent-safe like
    // EnsureSubtitleText: locates an already-created StatsSection via the
    // statsSection SerializeField instead of Find-ing it under a fixed
    // parent, so re-running after Build() has moved it into ContentRoot
    // doesn't spawn a duplicate. Fence hides this whole section
    // (RefreshFence sets statsSection inactive) since a Fence has no attack
    // stats. No longer self-positions relative to DayActionsRoot - once
    // inside ContentRoot, stacking order is Build()'s job.
    private static Transform EnsureStatsSection(SerializedObject so, Transform fallbackParent, TMP_FontAsset font, Material fontMaterial)
    {
        GameObject existing = GetRef<GameObject>(so, "statsSection");
        Transform sectionTransform;
        if (existing != null)
        {
            sectionTransform = existing.transform;
            if (sectionTransform.GetComponent<VerticalLayoutGroup>() == null)
            {
                sectionTransform.gameObject.AddComponent<VerticalLayoutGroup>();
            }
        }
        else
        {
            GameObject sectionGo = new GameObject("StatsSection", typeof(VerticalLayoutGroup));
            sectionGo.transform.SetParent(fallbackParent, false);
            sectionTransform = sectionGo.transform;
        }

        RectTransform sectionRect = sectionTransform.GetComponent<RectTransform>();
        FixNonStretchedRect(sectionRect, new Vector2(150f, 58f));

        VerticalLayoutGroup vlg = sectionTransform.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 3f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        EnsureStatRow(sectionTransform, "DamageRow", "DAMAGE", "0", font, fontMaterial);
        EnsureStatRow(sectionTransform, "AttackSpeedRow", "ATK SPEED", "0/s", font, fontMaterial);
        EnsureStatRow(sectionTransform, "RangeRow", "RANGE", "0m", font, fontMaterial);

        return sectionTransform;
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

    // Tower-only evolve button, placed as its own ContentRoot row above
    // DayActionsRoot (see EnsureFullWidthLayoutElement / Build()). Styled to
    // match UpgradeButton (same sprite/size) rather than reusing
    // BuildButtonCostLayout, since Evolve has no Wood/Scrap cost to show -
    // it's gated on tower tier, not resources. Reparent-safe like
    // EnsureStatsSection: locates an already-created button via the
    // evolveButton SerializeField instead of Find-ing it under a fixed
    // parent, so re-running after Build() has moved it doesn't duplicate it.
    private static Transform EnsureEvolveButton(SerializedObject so, Transform fallbackParent, Transform upgradeButton, TMP_FontAsset font, Material fontMaterial)
    {
        Button existingButton = GetRef<Button>(so, "evolveButton");
        Transform evolveTransform;
        if (existingButton != null)
        {
            evolveTransform = existingButton.transform;
        }
        else
        {
            GameObject evolveGo = new GameObject("EvolveButton", typeof(Image), typeof(Button));
            evolveGo.transform.SetParent(fallbackParent, false);
            evolveTransform = evolveGo.transform;
        }

        // Only borrow UpgradeButton's *size* here, not its anchorMin/Max/
        // pivot - those were tuned for UpgradeButton's old DayActionsRoot
        // context. Copying them onto EvolveButton left it with a corner
        // anchor ContentRoot's layout couldn't reliably size from (measured
        // sizeDelta.x collapsing to 0 on the last run). FixNonStretchedRect
        // gives it the same neutral center anchor as every other
        // ContentRoot-level child.
        RectTransform evolveRect = evolveTransform.GetComponent<RectTransform>();
        RectTransform upgradeRect = upgradeButton.GetComponent<RectTransform>();
        FixNonStretchedRect(evolveRect, upgradeRect.sizeDelta);

        Image upgradeImage = upgradeButton.GetComponent<Image>();
        Image evolveImage = evolveTransform.GetComponent<Image>();
        evolveImage.sprite = upgradeImage.sprite;
        evolveImage.type = upgradeImage.type;
        evolveImage.color = Color.white;

        Button evolveButtonComponent = evolveTransform.GetComponent<Button>();
        evolveButtonComponent.targetGraphic = evolveImage;

        GameObject textGo = EnsureChild(evolveTransform, "Text (TMP)", typeof(TextMeshProUGUI));
        FixNonStretchedRect(textGo.GetComponent<RectTransform>(), new Vector2(110f, 20f));

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = fontMaterial;
        tmp.fontSize = 12.3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = "EVOLVE"; // set at runtime by StructureActionPanelUI.Refresh()

        return evolveTransform;
    }

    // Single flex container for all StructureActionPanel body content.
    // Anchored to exactly cover Background's box so ContentRoot's own
    // padding reads as the panel's inner margin. Everything except
    // Background/BackgroundBorder/CloseButton lives inside this, so
    // sections stack by their real height instead of colliding at
    // hand-picked anchoredPositions (the StatsSection/HpSection overlap
    // bug this exists to fix).
    // ContentSizeFitter (Vertical Fit = Preferred Size) makes ContentRoot's
    // own height track its stacked content instead of staying pinned to
    // Background's original 200 forever. StructureActionPanelUI.
    // SyncBackgroundSize() reads the result back at runtime and resizes
    // Background/BackgroundBorder to match - see that method for why this
    // can't just be done here once at build time (Fence vs Tower show a
    // different number of sections, so the "right" height is only known at
    // runtime once a slot is selected).
    private static Transform EnsureContentRoot(Transform panelRoot, Transform background)
    {
        GameObject contentGo = EnsureChild(panelRoot, "ContentRoot", typeof(VerticalLayoutGroup));

        RectTransform bgRect = background.GetComponent<RectTransform>();
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = bgRect.anchorMin;
        contentRect.anchorMax = bgRect.anchorMax;
        contentRect.pivot = bgRect.pivot;
        contentRect.anchoredPosition = bgRect.anchoredPosition;
        contentRect.sizeDelta = bgRect.sizeDelta;

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentGo.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return contentGo.transform;
    }

    // Icon (fixed 64x64) + Title/Subtitle stack + TierBadge, left to right.
    // Reparents pre-existing objects in place (SetParent(.., false)) rather
    // than recreating them, so titleText/subtitleText/tierBadgeText
    // SerializeField references stay valid across rebuilds.
    private static Transform EnsureHeaderRow(Transform contentRoot, Transform iconRoot, Transform titleText, Transform subtitleText, Transform tierBadge)
    {
        GameObject headerGo = EnsureChild(contentRoot, "HeaderRow", typeof(HorizontalLayoutGroup));
        RectTransform headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(300f, 64f);

        HorizontalLayoutGroup hlg = headerGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        if (iconRoot != null)
        {
            iconRoot.SetParent(headerGo.transform, false);
            iconRoot.SetSiblingIndex(0);
            FixNonStretchedRect(iconRoot.GetComponent<RectTransform>(), new Vector2(64f, 64f));
            // IconRoot itself has no Image/Graphic (that's its "Img" child),
            // so it has nothing to report a preferred size through -
            // without this, HeaderRow's HorizontalLayoutGroup has no
            // reliable size to lay it out with.
            EnsureFixedSizeLayoutElement(iconRoot, 64f, 64f);
        }

        GameObject stackGo = EnsureChild(headerGo.transform, "TitleStack", typeof(VerticalLayoutGroup));
        stackGo.transform.SetSiblingIndex(iconRoot != null ? 1 : 0);
        RectTransform stackRect = stackGo.GetComponent<RectTransform>();
        stackRect.sizeDelta = new Vector2(150f, 44f);

        VerticalLayoutGroup stackVlg = stackGo.GetComponent<VerticalLayoutGroup>();
        stackVlg.spacing = 2f;
        stackVlg.childAlignment = TextAnchor.MiddleLeft;
        stackVlg.childForceExpandWidth = false;
        stackVlg.childForceExpandHeight = false;
        stackVlg.childControlWidth = false;
        stackVlg.childControlHeight = false;

        titleText.SetParent(stackGo.transform, false);
        titleText.SetSiblingIndex(0);
        subtitleText.SetParent(stackGo.transform, false);
        subtitleText.SetSiblingIndex(1);

        tierBadge.SetParent(headerGo.transform, false);
        tierBadge.SetSiblingIndex(headerGo.transform.childCount - 1);

        return headerGo.transform;
    }

    // Confirmed with design: IconRoot's own Canvas/CanvasScaler/
    // GraphicRaycaster were a stray oversized RectTransform (sizeDelta
    // 1090x613), not an intentional 3D-preview overlay. Strips them so it
    // renders as a plain child Image inside HeaderRow. Safe to call every
    // run - no-ops once already stripped.
    // Order matters: CanvasScaler and GraphicRaycaster both carry
    // [RequireComponent(typeof(Canvas))], so destroying Canvas first makes
    // Unity silently refuse that destroy (Canvas stays, only the other two
    // go) - confirmed this actually happened on the prior run, which is why
    // IconRoot kept rendering as a detached Screen Space - Overlay canvas.
    // Destroying the dependents before the dependency avoids that.
    private static void StripIconRootOverlayComponents(Transform iconRoot)
    {
        CanvasScaler scaler = iconRoot.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            Object.DestroyImmediate(scaler);
        }

        GraphicRaycaster raycaster = iconRoot.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Object.DestroyImmediate(raycaster);
        }

        Canvas canvas = iconRoot.GetComponent<Canvas>();
        if (canvas != null)
        {
            Object.DestroyImmediate(canvas);
        }
    }

    // EvolveButton sits directly in ContentRoot (childControlWidth=true
    // already stretches it), but a LayoutElement makes the full-width
    // intent explicit and gives the layout pass a concrete non-zero
    // preferredWidth to converge on instead of falling back to the
    // Image/Button's own (unreliable in this headless build context)
    // reported size - see EnsureEvolveButton's comment on the sizeDelta.x=0
    // this caused before.
    private static void EnsureFullWidthLayoutElement(Transform target, float preferredWidth)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minWidth = -1f;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = 1f;
    }

    // IconRoot has no Graphic/ILayoutElement of its own (its "Img" child
    // does), so without this HeaderRow's HorizontalLayoutGroup has nothing
    // reliable to size it by. Fixed, non-flexible slot - unlike
    // EnsureFullWidthLayoutElement, this should never stretch.
    private static void EnsureFixedSizeLayoutElement(Transform target, float width, float height)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    // Makes a ContentRoot section self-report its real height instead of
    // perpetuating a stale hand-picked sizeDelta.y from before this
    // restructure (ContentRoot's childControlHeight=false just stacks
    // children at whatever height they already have). Only effective on
    // sections that themselves have a LayoutGroup to compute a preferred
    // height from (StatsSection, DayActionsRoot) - HpSection's children are
    // still manually anchoredPosition-placed with no LayoutGroup of their
    // own, so this is currently a no-op there. Flagged as a known follow-up
    // rather than guessing a manual height for it.
    private static void EnsureVerticalContentSizeFitter(Transform target)
    {
        ContentSizeFitter fitter = target.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = target.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
