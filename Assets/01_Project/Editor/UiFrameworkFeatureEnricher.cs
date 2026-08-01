#if UNITY_EDITOR
using System.IO;
using FpsDemo.UI;
using TMPro;
using UIFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Strips non-framework UI leftovers from screen prefabs and wires Fade / Settings / Hurt Prioritary panel.
/// Menu: FpsDemo → UI → Enrich UI Framework Features.
/// </summary>
public static class UiFrameworkFeatureEnricher
{
    private const string MenuPath = "FpsDemo/UI/Enrich UI Framework Features";
    private const string ScreensDir = UiSettingsScreenList.ScreensDir;

    [InitializeOnLoadMethod]
    private static void AutoEnrichIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ScreensDir + "/SettingsWindowController.prefab") != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(ScreensDir + "/HurtOverlayPanelController.prefab") != null)
                return;
            Enrich(showDialog: false);
        };
    }

    [MenuItem(MenuPath, priority = 30)]
    public static void EnrichMenu()
    {
        Enrich(showDialog: true);
    }

    public static void Enrich(bool showDialog)
    {
        if (!Directory.Exists(ScreensDir))
            Directory.CreateDirectory(ScreensDir);

        EnsureSettingsWindowPrefab();
        EnsureHurtOverlayPanelPrefab();
        StripLobbySettingsPlaceholder();
        StripEndGameSelfDimAndWireFade();
        WireFadeOnLobby();
        StripHurtFromDeathmatchHud();
        UiSettingsScreenList.WireAllPresentScreens();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[UiFrameworkFeatureEnricher] Screens enriched (Fade / Settings / Hurt Prioritary).");
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "UI Framework Features",
                "Done:\n" +
                "• Removed lobby SettingsPanel placeholder\n" +
                "• EndGame uses framework DarkenBG (no self dim Image)\n" +
                "• FadeAni on Lobby / Settings / EndGame\n" +
                "• SettingsWindowController + HurtOverlayPanelController (Prioritary)\n\n" +
                "Restart Play so UIFrame re-registers screens.",
                "OK");
        }
    }

    private static void EnsureSettingsWindowPrefab()
    {
        string path = ScreensDir + "/SettingsWindowController.prefab";
        var rootGo = new GameObject(SettingsWindowController.ScreenId, typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var window = rootGo.AddComponent<SettingsWindowController>();
        AttachFadePair(rootGo);

        var boxGo = new GameObject("Box", typeof(RectTransform));
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.SetParent(rootRt, false);
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(480f, 320f);
        boxGo.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

        var body = CreateTmp(boxRt, "Body", "Settings", 18f, TextAlignmentOptions.Center);
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = new Vector2(0.08f, 0.32f);
        bodyRt.anchorMax = new Vector2(0.92f, 0.9f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        var closeBtn = CreateButton(boxRt, "BtnClose", "Close");
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0.14f);
        closeRt.sizeDelta = new Vector2(180f, 44f);

        var so = new SerializedObject(window);
        so.FindProperty("_bodyText").objectReferenceValue = body;
        so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
        var props = so.FindProperty("properties");
        if (props != null)
        {
            // Popup → PriorityWindowLayer + DarkenBG; Lobby stays under (framework does not hide current when opening popup).
            props.FindPropertyRelative("hideOnForegroundLost").boolValue = false;
            props.FindPropertyRelative("windowQueuePriority").enumValueIndex = (int)WindowPriority.ForceForeground;
            props.FindPropertyRelative("isPopup").boolValue = true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(rootGo, path);
        Object.DestroyImmediate(rootGo);
    }

    private static void EnsureHurtOverlayPanelPrefab()
    {
        string path = ScreensDir + "/HurtOverlayPanelController.prefab";
        var rootGo = new GameObject(HurtOverlayPanelController.ScreenId, typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var image = rootGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        var feedback = rootGo.AddComponent<FpsPlayerHurtOverlayFeedback>();
        var panel = rootGo.AddComponent<HurtOverlayPanelController>();

        var soPanel = new SerializedObject(panel);
        soPanel.FindProperty("_overlayImage").objectReferenceValue = image;
        soPanel.FindProperty("_feedback").objectReferenceValue = feedback;
        var props = soPanel.FindProperty("properties");
        if (props != null)
        {
            var priority = props.FindPropertyRelative("priority");
            if (priority != null)
                priority.enumValueIndex = (int)PanelPriority.Prioritary;
        }

        soPanel.ApplyModifiedPropertiesWithoutUndo();

        var soFb = new SerializedObject(feedback);
        soFb.FindProperty("_overlayImage").objectReferenceValue = image;
        soFb.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(rootGo, path);
        Object.DestroyImmediate(rootGo);
    }

    private static void StripLobbySettingsPlaceholder()
    {
        string path = ScreensDir + "/LobbyMenuWindowController.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var settings = root.transform.Find("SettingsPanel");
            if (settings != null)
                Object.DestroyImmediate(settings.gameObject);

            var window = root.GetComponent<LobbyMenuWindowController>();
            if (window != null)
            {
                var so = new SerializedObject(window);
                var legacy = so.FindProperty("_settingsPlaceholderPanel");
                if (legacy != null)
                    legacy.objectReferenceValue = null;
                var props = so.FindProperty("properties");
                if (props != null)
                    props.FindPropertyRelative("hideOnForegroundLost").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AttachFadePair(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void StripEndGameSelfDimAndWireFade()
    {
        string path = ScreensDir + "/EndGameWindowController.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var img = root.GetComponent<Image>();
            if (img != null)
                Object.DestroyImmediate(img);

            AttachFadePair(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireFadeOnLobby()
    {
        // Already handled in StripLobbySettingsPlaceholder.
    }

    private static void StripHurtFromDeathmatchHud()
    {
        string path = ScreensDir + "/DeathmatchHudPanelController.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var hurt = root.transform.Find("HurtOverlay");
            if (hurt != null)
                Object.DestroyImmediate(hurt.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AttachFadePair(GameObject root)
    {
        if (root.GetComponent<CanvasGroup>() == null)
            root.AddComponent<CanvasGroup>();

        var existing = root.GetComponents<FadeAni>();
        for (int i = existing.Length - 1; i >= 0; i--)
            Object.DestroyImmediate(existing[i]);

        var fadeIn = root.AddComponent<FadeAni>();
        var fadeOut = root.AddComponent<FadeAni>();
        var soIn = new SerializedObject(fadeIn);
        soIn.FindProperty("fadeDuration").floatValue = 0.2f;
        soIn.FindProperty("fadeOut").boolValue = false;
        soIn.ApplyModifiedPropertiesWithoutUndo();
        var soOut = new SerializedObject(fadeOut);
        soOut.FindProperty("fadeDuration").floatValue = 0.15f;
        soOut.FindProperty("fadeOut").boolValue = true;
        soOut.ApplyModifiedPropertiesWithoutUndo();

        foreach (var c in root.GetComponents<MonoBehaviour>())
        {
            var so = new SerializedObject(c);
            var animIn = so.FindProperty("animIn");
            var animOut = so.FindProperty("animOut");
            if (animIn == null || animOut == null)
                continue;
            animIn.objectReferenceValue = fadeIn;
            animOut.objectReferenceValue = fadeOut;
            so.ApplyModifiedPropertiesWithoutUndo();
            break;
        }
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    private static TMP_Text CreateTmp(RectTransform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(RectTransform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        go.AddComponent<Image>().color = new Color(0.15f, 0.42f, 0.82f, 1f);
        var btn = go.AddComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        StretchFull(labelRt);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }
}
#endif
