#if UNITY_EDITOR
using System.IO;
using FpsDemo.UI;
using TMPro;
using UIFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds <see cref="DeathmatchHudPanelController"/> prefab and refreshes UISettings screen list.</summary>
public static class DeathmatchHudPanelPrefabBuilder
{
    private const string MenuPath = "FpsDemo/UI/Build DeathmatchHudPanelController Prefab And Wire UISettings";
    private const string EnsureWidgetsMenuPath =
        "FpsDemo/UI/Ensure Gameplay Widgets On DeathmatchHudPanelController Prefab";
    private const string PrefabPath = UiSettingsScreenList.ScreensDir + "/DeathmatchHudPanelController.prefab";
    private const int KillFeedLineCount = 5;

    [InitializeOnLoadMethod]
    private static void AutoBuildIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                BuildAndWire(showDialog: false);
                return;
            }

            EnsureGameplayWidgetsOnExistingPrefab(showDialog: false);
        };
    }

    [MenuItem(MenuPath, priority = 22)]
    public static void BuildAndWireMenu()
    {
        BuildAndWire(showDialog: true);
    }

    [MenuItem(EnsureWidgetsMenuPath, priority = 23)]
    public static void EnsureGameplayWidgetsMenu()
    {
        EnsureGameplayWidgetsOnExistingPrefab(showDialog: true);
    }

    public static void BuildAndWire(bool showDialog)
    {
        if (!Directory.Exists(UiSettingsScreenList.ScreensDir))
            Directory.CreateDirectory(UiSettingsScreenList.ScreensDir);

        var rootGo = new GameObject(DeathmatchHudPanelController.ScreenId, typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var canvasGroup = rootGo.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var panel = rootGo.AddComponent<DeathmatchHudPanelController>();

        var topBar = CreateChild(rootRt, "TopBar");
        StretchFull(topBar);
        topBar.anchorMin = new Vector2(0f, 0.88f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.offsetMin = Vector2.zero;
        topBar.offsetMax = Vector2.zero;

        var timer = CreateTmp(topBar, "MatchTimer", "05:00", 36f, TextAlignmentOptions.Center);
        var timerRt = timer.rectTransform;
        timerRt.anchorMin = new Vector2(0.35f, 0.15f);
        timerRt.anchorMax = new Vector2(0.65f, 0.95f);
        timerRt.offsetMin = Vector2.zero;
        timerRt.offsetMax = Vector2.zero;

        var board = CreateTmp(topBar, "Leaderboard", "Target 20 Kills", 16f, TextAlignmentOptions.TopLeft);
        var boardRt = board.rectTransform;
        boardRt.anchorMin = new Vector2(0.02f, 0.05f);
        boardRt.anchorMax = new Vector2(0.32f, 0.95f);
        boardRt.offsetMin = Vector2.zero;
        boardRt.offsetMax = Vector2.zero;
        board.enableWordWrapping = true;

        var feedRoot = CreateChild(rootRt, "KillFeed");
        feedRoot.anchorMin = new Vector2(0.62f, 0.55f);
        feedRoot.anchorMax = new Vector2(0.98f, 0.88f);
        feedRoot.offsetMin = Vector2.zero;
        feedRoot.offsetMax = Vector2.zero;

        var feedLines = new TMP_Text[KillFeedLineCount];
        for (int i = 0; i < KillFeedLineCount; i++)
        {
            float yMax = 1f - i * 0.2f;
            float yMin = yMax - 0.18f;
            var line = CreateTmp(feedRoot, "KillLine_" + i, "", 16f, TextAlignmentOptions.TopRight);
            var lineRt = line.rectTransform;
            lineRt.anchorMin = new Vector2(0f, yMin);
            lineRt.anchorMax = new Vector2(1f, yMax);
            lineRt.offsetMin = Vector2.zero;
            lineRt.offsetMax = Vector2.zero;
            line.gameObject.SetActive(false);
            feedLines[i] = line;
        }

        var healthRoot = CreateChild(rootRt, "HealthCluster");
        healthRoot.anchorMin = new Vector2(0.02f, 0.04f);
        healthRoot.anchorMax = new Vector2(0.28f, 0.14f);
        healthRoot.offsetMin = Vector2.zero;
        healthRoot.offsetMax = Vector2.zero;

        var sliderGo = new GameObject("HealthSlider", typeof(RectTransform));
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.SetParent(healthRoot, false);
        sliderRt.anchorMin = new Vector2(0f, 0.45f);
        sliderRt.anchorMax = new Vector2(1f, 1f);
        sliderRt.offsetMin = Vector2.zero;
        sliderRt.offsetMax = Vector2.zero;

        var bg = sliderGo.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);

        var fillArea = CreateChild(sliderRt, "Fill Area");
        StretchFull(fillArea);
        fillArea.offsetMin = new Vector2(4f, 4f);
        fillArea.offsetMax = new Vector2(-4f, -4f);

        var fill = CreateChild(fillArea, "Fill");
        StretchFull(fill);
        var fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.75f, 0.35f, 1f);

        var slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.targetGraphic = fillImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;

        var healthNum = CreateTmp(healthRoot, "HealthNumber", "100 / 100", 18f, TextAlignmentOptions.MidlineLeft);
        var healthNumRt = healthNum.rectTransform;
        healthNumRt.anchorMin = new Vector2(0f, 0f);
        healthNumRt.anchorMax = new Vector2(1f, 0.45f);
        healthNumRt.offsetMin = Vector2.zero;
        healthNumRt.offsetMax = Vector2.zero;

        var so = new SerializedObject(panel);
        so.FindProperty("_matchTimerText").objectReferenceValue = timer;
        so.FindProperty("_leaderboardText").objectReferenceValue = board;
        var feedProp = so.FindProperty("_killFeedLines");
        feedProp.arraySize = KillFeedLineCount;
        for (int i = 0; i < KillFeedLineCount; i++)
            feedProp.GetArrayElementAtIndex(i).objectReferenceValue = feedLines[i];
        so.FindProperty("_healthSlider").objectReferenceValue = slider;
        so.FindProperty("_healthNumber").objectReferenceValue = healthNum;

        var props = so.FindProperty("properties");
        if (props != null)
        {
            var priority = props.FindPropertyRelative("priority");
            if (priority != null)
                priority.enumValueIndex = (int)PanelPriority.None;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureGameplayWidgets(rootRt);

        PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        UiSettingsScreenList.WireAllPresentScreens();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DeathmatchHudPanelPrefabBuilder] Saved " + PrefabPath);
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "DeathmatchHudPanelController",
                "Built DeathmatchHudPanelController.prefab and refreshed UISettings screens.\n" +
                "Restart Play if UIFrame was already created without this screen.",
                "OK");
        }
    }

    /// <summary>
    /// 在已有 HUD 预制体上补弹药 / 准星 / 受伤红闪 / 连杀（不重建顶栏等，保留你微调过的布局）。
    /// </summary>
    public static void EnsureGameplayWidgetsOnExistingPrefab(bool showDialog)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            BuildAndWire(showDialog);
            return;
        }

        var rootGo = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var rootRt = rootGo.GetComponent<RectTransform>();
            bool changed = EnsureGameplayWidgets(rootRt);
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[DeathmatchHudPanelPrefabBuilder] Added gameplay widgets to " + PrefabPath);
            }
            else
            {
                Debug.Log("[DeathmatchHudPanelPrefabBuilder] Gameplay widgets already present on " + PrefabPath);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "DeathmatchHudPanelController",
                    changed
                        ? "Added Ammo / Crosshair / HurtOverlay / KillStreak to the HUD prefab.\n" +
                          "You can nudge positions in the prefab; scene GamePlayCanvas duplicates can stay off."
                        : "Ammo / Crosshair / HurtOverlay / KillStreak already exist on the HUD prefab.",
                    "OK");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(rootGo);
        }
    }

    /// <returns>true if any widget was created.</returns>
    public static bool EnsureGameplayWidgets(RectTransform rootRt)
    {
        if (rootRt == null)
            return false;

        bool changed = false;

        if (rootRt.Find("HurtOverlay") == null)
        {
            CreateHurtOverlay(rootRt);
            changed = true;
        }

        if (rootRt.Find("Crosshair") == null)
        {
            CreateCrosshair(rootRt);
            changed = true;
        }

        if (rootRt.Find("Ammo") == null)
        {
            CreateAmmo(rootRt);
            changed = true;
        }

        if (rootRt.Find("KillStreakPlaceholder") == null)
        {
            CreateKillStreak(rootRt);
            changed = true;
        }

        // Hurt 画在最底层，其它 HUD 盖在上面。
        var hurt = rootRt.Find("HurtOverlay");
        if (hurt != null)
            hurt.SetAsFirstSibling();

        return changed;
    }

    private static void CreateHurtOverlay(RectTransform rootRt)
    {
        var rt = CreateChild(rootRt, "HurtOverlay");
        StretchFull(rt);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        var feedback = rt.gameObject.AddComponent<FpsPlayerHurtOverlayFeedback>();
        var so = new SerializedObject(feedback);
        so.FindProperty("_overlayImage").objectReferenceValue = image;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateCrosshair(RectTransform rootRt)
    {
        var rt = CreateChild(rootRt, "Crosshair");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(8f, 8f);
        rt.anchoredPosition = Vector2.zero;

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        image.color = new Color(1f, 1f, 1f, 0.85f);
        image.raycastTarget = false;

        var feedback = rt.gameObject.AddComponent<FpsCrosshairHitFeedback>();
        var so = new SerializedObject(feedback);
        so.FindProperty("_crosshairImage").objectReferenceValue = image;
        so.FindProperty("_hitColor").colorValue = new Color(1f, 0f, 0f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateAmmo(RectTransform rootRt)
    {
        var ammoTmp = CreateTmp(rootRt, "Ammo", "30 / 30", 28f, TextAlignmentOptions.Center);
        var rt = ammoTmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f);
        rt.sizeDelta = new Vector2(240f, 68f);

        var hub = rt.gameObject.AddComponent<AmmoHub>();
        var so = new SerializedObject(hub);
        so.FindProperty("_ammoText").objectReferenceValue = ammoTmp;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateKillStreak(RectTransform rootRt)
    {
        var tmp = CreateTmp(rootRt, "KillStreakPlaceholder", "", 32f, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.92f, 0.23f, 1f);
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(480f, 64f);
        tmp.enabled = false;

        var placeholder = rt.gameObject.AddComponent<KillStreakHudPlaceholder>();
        var so = new SerializedObject(placeholder);
        so.FindProperty("_streakText").objectReferenceValue = tmp;
        so.FindProperty("_format").stringValue = "Killx{0}";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
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
}
#endif
