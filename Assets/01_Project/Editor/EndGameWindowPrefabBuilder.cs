#if UNITY_EDITOR
using System.IO;
using FpsDemo.UI;
using TMPro;
using UIFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds <see cref="EndGameWindowController"/> prefab and refreshes UISettings screen list.</summary>
public static class EndGameWindowPrefabBuilder
{
    private const string MenuPath = "FpsDemo/UI/Build EndGameWindowController Prefab And Wire UISettings";
    private const string PrefabPath = UiSettingsScreenList.ScreensDir + "/EndGameWindowController.prefab";

    [InitializeOnLoadMethod]
    private static void AutoBuildIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;
            BuildAndWire(showDialog: false);
        };
    }

    [MenuItem(MenuPath, priority = 21)]
    public static void BuildAndWireMenu()
    {
        BuildAndWire(showDialog: true);
    }

    public static void BuildAndWire(bool showDialog)
    {
        if (!Directory.Exists(UiSettingsScreenList.ScreensDir))
            Directory.CreateDirectory(UiSettingsScreenList.ScreensDir);

        var rootGo = new GameObject(EndGameWindowController.ScreenId, typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var dim = rootGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        var window = rootGo.AddComponent<EndGameWindowController>();

        var boxGo = new GameObject("Box", typeof(RectTransform));
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.SetParent(rootRt, false);
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(520f, 280f);
        boxGo.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

        var summary = CreateTmp(boxRt, "Summary", "Match over", 22f, TextAlignmentOptions.Center);
        var summaryRt = summary.rectTransform;
        summaryRt.anchorMin = new Vector2(0.08f, 0.38f);
        summaryRt.anchorMax = new Vector2(0.92f, 0.88f);
        summaryRt.offsetMin = Vector2.zero;
        summaryRt.offsetMax = Vector2.zero;
        summary.color = new Color(0.92f, 0.93f, 0.96f, 1f);

        var againBtn = CreateButton(boxRt, "BtnAgain", "Again");
        var againRt = againBtn.GetComponent<RectTransform>();
        againRt.anchorMin = againRt.anchorMax = new Vector2(0.5f, 0.18f);
        againRt.sizeDelta = new Vector2(200f, 48f);
        againRt.anchoredPosition = Vector2.zero;

        var so = new SerializedObject(window);
        so.FindProperty("_summaryText").objectReferenceValue = summary;
        so.FindProperty("_againButton").objectReferenceValue = againBtn;

        var props = so.FindProperty("properties");
        if (props != null)
        {
            props.FindPropertyRelative("hideOnForegroundLost").boolValue = false;
            props.FindPropertyRelative("windowQueuePriority").enumValueIndex = (int)WindowPriority.ForceForeground;
            props.FindPropertyRelative("isPopup").boolValue = true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        UiSettingsScreenList.WireAllPresentScreens();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[EndGameWindowPrefabBuilder] Saved " + PrefabPath);
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "EndGameWindowController",
                "Built EndGameWindowController.prefab and refreshed UISettings screens.\n" +
                "Restart Play if UIFrame was already created without this screen.",
                "OK");
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
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }
}
#endif
