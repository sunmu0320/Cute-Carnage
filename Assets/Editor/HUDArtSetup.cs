using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Idempotent setup for the Vitals (HP/Hunger) + Resources HUD.
/// 1) Import settings + one cropped sprite per PNG in Assets/Art/UI/HUD.
/// 2) Builds/updates the hierarchy inside HUDCanvas.prefab, reusing the existing TMP texts
///    so HUDController references stay intact. Re-running finds objects by name, never duplicates.
/// </summary>
public static class HUDArtSetup
{
    private const string ArtDir = "Assets/Art/UI/HUD/";
    private const string HudPrefabPath = "Assets/Prefabs/UI/HUDCanvas.prefab";

    private const string HeartPng = "Coral Faceted Heart Icon-1.png";
    private const string DrumstickPng = "Golden chicken drumstick icon-3.png";
    private const string WoodPng = "Three-log wood bundle icon-2.png";
    private const string BoltPng = "Faceted silver hex bolt-4.png";
    private const string TinPng = "Cute coral food tin icon-6.png";
    private const string BarFramePng = "Minimal empty health bar frame-5.png";
    private const string HpFillPng = "Coral health bar fill-7.png";
    private const string HungerFillPng = "Golden hunger fill bar-8.png";
    private const string ResourcesPanelPng = "Three-Slot Resource HUD Panel.png";
    private const string ResourcesPanelPlainPng = "Minimal dark resource panel background-9.png"; // spare, unused

    // Sprite rects in texture pixels (origin bottom-left): alpha>8 bbox + 8px (bars/panels) / 12px (icons) padding,
    // measured from the source PNGs so soft edges and outlines are not clipped.
    private static readonly Dictionary<string, Rect> SpriteRects = new Dictionary<string, Rect>
    {
        { HeartPng, new Rect(129, 180, 997, 852) },
        { HpFillPng, new Rect(65, 284, 2042, 158) },
        { TinPng, new Rect(239, 150, 777, 926) },
        { BoltPng, new Rect(185, 164, 941, 893) },
        { DrumstickPng, new Rect(194, 178, 901, 884) },
        { HungerFillPng, new Rect(51, 295, 1977, 167) },
        { ResourcesPanelPlainPng, new Rect(59, 262, 2053, 202) },
        { BarFramePng, new Rect(86, 310, 2001, 104) },
        { ResourcesPanelPng, new Rect(59, 265, 2056, 204) },
        { WoodPng, new Rect(100, 172, 1098, 878) },
    };

    // Frame: 8px padding + 4px outline + ~10px corner radius.
    private static readonly Vector4 BarFrameBorder = new Vector4(22, 22, 22, 22);
    private const float BarFramePpuMultiplier = 3f;

    // Divider centres of the three-slot panel (x 749.5 / 1421.5 px) as fractions of its sprite rect;
    // edges are the inner opaque bounds (x 67 / 2107).
    private static readonly float[] SlotEdges = { 0.0039f, 0.3358f, 0.6627f, 0.9961f };

    private static readonly Color TextColor = new Color32(0xF3, 0xEE, 0xDC, 0xFF);

    [MenuItem("Tools/Cute Carnage/HUD/1 Apply HUD Sprite Import Settings")]
    public static void ApplyImportSettings()
    {
#pragma warning disable 0618 // spritesheet: com.unity.2d.sprite (ISpriteEditorDataProvider) is not installed
        foreach (KeyValuePair<string, Rect> entry in SpriteRects)
        {
            string path = ArtDir + entry.Key;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[HUDArtSetup] Missing texture: {path}");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            foreach (string platform in new[] { "Standalone", "Android", "iOS", "WebGL" })
            {
                TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings(platform);
                if (ps.overridden)
                {
                    ps.overridden = false;
                    importer.SetPlatformTextureSettings(ps);
                }
            }

            string spriteName = System.IO.Path.GetFileNameWithoutExtension(entry.Key);
            importer.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name = spriteName,
                    rect = entry.Value,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = entry.Key == BarFramePng ? BarFrameBorder : Vector4.zero,
                },
            };

            importer.SaveAndReimport();
        }
#pragma warning restore 0618
        Debug.Log("[HUDArtSetup] HUD sprite import settings applied.");
    }

    [MenuItem("Tools/Cute Carnage/HUD/2 Build Vitals + Resources HUD")]
    public static void BuildHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            var canvasScaler = root.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            Transform hudRoot = root.transform.Find("HUDRoot");
            var hud = hudRoot.GetComponent<HUDController>();
            var so = new SerializedObject(hud);

            // ---- Vitals (top-left) ----
            RectTransform vitals = FindOrRename(hudRoot, "VitalsGroup", "TopLeftHP");
            SetRect(vitals, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32), new Vector2(302, 82));

            Image hpFill = BuildVitalRow(vitals, "HealthRow", 0f, HeartPng, HpFillPng,
                (TextMeshProUGUI)so.FindProperty("hpText").objectReferenceValue);
            Image hungerFill = BuildVitalRow(vitals, "HungerRow", -46f, DrumstickPng, HungerFillPng,
                (TextMeshProUGUI)so.FindProperty("hungerText").objectReferenceValue);
            so.FindProperty("hpBarFillImage").objectReferenceValue = hpFill;
            so.FindProperty("hungerBarFillImage").objectReferenceValue = hungerFill;

            // ---- Resources (bottom-left): Wood -> Scrap -> Food ----
            RectTransform resources = FindOrRename(hudRoot, "ResourcesGroup", "BottomLeftResources");
            Sprite panelSprite = LoadSprite(ResourcesPanelPng);
            float panelHeight = 48f;
            float panelWidth = Mathf.Round(panelHeight * panelSprite.rect.width / panelSprite.rect.height);
            SetRect(resources, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32, 32), new Vector2(panelWidth, panelHeight));

            Image panel = GetOrAdd<Image>(Child(resources, "ResourcesBackground"));
            Stretch(panel.rectTransform);
            SetupImage(panel, panelSprite, Image.Type.Simple, false);
            panel.transform.SetSiblingIndex(0);

            RectTransform slots = Child(resources, "SlotsRoot");
            Stretch(slots);
            slots.SetSiblingIndex(1);
            BuildSlot(slots, "WoodSlot", 0, WoodPng, (TextMeshProUGUI)so.FindProperty("woodText").objectReferenceValue);
            BuildSlot(slots, "ScrapSlot", 1, BoltPng, (TextMeshProUGUI)so.FindProperty("scrapText").objectReferenceValue);
            BuildSlot(slots, "FoodSlot", 2, TinPng, (TextMeshProUGUI)so.FindProperty("foodText").objectReferenceValue);

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Debug.Log("[HUDArtSetup] HUDCanvas.prefab vitals/resources HUD built.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Image BuildVitalRow(RectTransform vitals, string rowName, float y, string iconPng, string fillPng, TextMeshProUGUI valueText)
    {
        RectTransform row = Child(vitals, rowName);
        SetRect(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(302, 36));

        Image icon = GetOrAdd<Image>(Child(row, rowName == "HealthRow" ? "HealthIcon" : "HungerIcon"));
        SetRect(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(32, 32));
        SetupImage(icon, LoadSprite(iconPng), Image.Type.Simple, true);

        RectTransform barRoot = Child(row, "BarRoot");
        SetRect(barRoot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(260, 30));

        Image frame = GetOrAdd<Image>(Child(barRoot, "BarBackground"));
        Stretch(frame.rectTransform);
        SetupImage(frame, LoadSprite(BarFramePng), Image.Type.Sliced, false);
        frame.fillCenter = true;
        frame.pixelsPerUnitMultiplier = BarFramePpuMultiplier;
        frame.transform.SetSiblingIndex(0);

        Image fill = GetOrAdd<Image>(Child(barRoot, "Fill"));
        Stretch(fill.rectTransform, 4f);
        SetupImage(fill, LoadSprite(fillPng), Image.Type.Filled, false);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.transform.SetSiblingIndex(1);

        // Reuse the existing TMP text (keeps HUDController's reference); sibling of Fill, not its child.
        valueText.gameObject.name = "ValueText";
        valueText.transform.SetParent(barRoot, false);
        Stretch(valueText.rectTransform);
        StyleText(valueText, 18f, TextAlignmentOptions.Center);
        valueText.transform.SetSiblingIndex(2);
        return fill;
    }

    private static void BuildSlot(RectTransform slots, string slotName, int index, string iconPng, TextMeshProUGUI countText)
    {
        RectTransform slot = Child(slots, slotName);
        slot.anchorMin = new Vector2(SlotEdges[index], 0);
        slot.anchorMax = new Vector2(SlotEdges[index + 1], 1);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.offsetMin = Vector2.zero;
        slot.offsetMax = Vector2.zero;
        slot.SetSiblingIndex(index);

        Image icon = GetOrAdd<Image>(Child(slot, "Icon"));
        SetRect(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(30, 30));
        SetupImage(icon, LoadSprite(iconPng), Image.Type.Simple, true);

        countText.gameObject.name = "CountText";
        countText.transform.SetParent(slot, false);
        RectTransform rt = countText.rectTransform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(56, 0);
        rt.offsetMax = new Vector2(-10, 0);
        StyleText(countText, 20f, TextAlignmentOptions.Left);
    }

    private static void StyleText(TextMeshProUGUI text, float size, TextAlignmentOptions alignment)
    {
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = alignment;
        text.color = TextColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private static void SetupImage(Image image, Sprite sprite, Image.Type type, bool preserveAspect)
    {
        image.sprite = sprite;
        image.type = type;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static Sprite LoadSprite(string png)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ArtDir + png))
        {
            if (asset is Sprite sprite)
                return sprite;
        }

        throw new System.InvalidOperationException($"[HUDArtSetup] No sprite in {png}. Run step 1 first.");
    }

    private static RectTransform FindOrRename(Transform parent, string name, string legacyName)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = parent.Find(legacyName);
            t.name = name;
        }

        return (RectTransform)t;
    }

    private static RectTransform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null)
            return (RectTransform)t;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static T GetOrAdd<T>(RectTransform rt) where T : Component
    {
        T c = rt.GetComponent<T>();
        return c != null ? c : rt.gameObject.AddComponent<T>();
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
