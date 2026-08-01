#if UNITY_EDITOR
using System.IO;
using FpsDemo.UI;
using TMPro;
using UIFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds <c>LobbyMenu</c> screen prefab for UIFramework and wires <see cref="UISettings"/>.
/// </summary>
public static class LobbyMenuPrefabBuilder
{
    private const string MenuPath = "FpsDemo/UI/Build LobbyMenuWindowController Prefab And Wire UISettings";
    private const string PrefabDir = UiSettingsScreenList.ScreensDir;
    private const string PrefabPath = PrefabDir + "/LobbyMenuWindowController.prefab";
    private const float MainColumnWidth = 448f;

    private static readonly string[] LegacyPrefabPaths =
    {
        PrefabDir + "/LobbyMenu.prefab",
        PrefabDir + "/LobbyMenuController.prefab",
    };

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

    [MenuItem(MenuPath, priority = 20)]
    public static void BuildAndWireMenu()
    {
        BuildAndWire(showDialog: true);
    }

    public static void BuildAndWire(bool showDialog)
    {
        if (!Directory.Exists(PrefabDir))
            Directory.CreateDirectory(PrefabDir);

        foreach (var legacy in LegacyPrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacy) != null)
                AssetDatabase.DeleteAsset(legacy);
        }

        var rootGo = new GameObject(LobbyMenuWindowController.ScreenId, typeof(RectTransform));
        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var window = rootGo.AddComponent<LobbyMenuWindowController>();
        var lobbyRoot = BuildLobbyRoot(rootRt);
        var settingsPanel = BuildSettingsPanel(rootRt);

        var so = new SerializedObject(window);
        so.FindProperty("_createRoomButton").objectReferenceValue =
            FindBuilt<Button>(lobbyRoot, "BtnCreateRoom");
        so.FindProperty("_joinGameButton").objectReferenceValue =
            FindBuilt<Button>(lobbyRoot, "BtnJoinGame");
        so.FindProperty("_singlePlayerButton").objectReferenceValue =
            FindBuilt<Button>(lobbyRoot, "BtnSinglePlayer");
        so.FindProperty("_settingsButton").objectReferenceValue =
            FindBuilt<Button>(lobbyRoot, "BtnSettings");
        so.FindProperty("_joinAddressInput").objectReferenceValue =
            FindBuilt<TMP_InputField>(lobbyRoot, "AddressField");
        so.FindProperty("_joinPortInput").objectReferenceValue =
            FindBuilt<TMP_InputField>(lobbyRoot, "PortField");
        so.FindProperty("_hintText").objectReferenceValue =
            FindBuilt<TMP_Text>(lobbyRoot, "Hint");
        so.FindProperty("_settingsPlaceholderPanel").objectReferenceValue = settingsPanel;

        // Default window props: force foreground, hide when another window opens.
        var props = so.FindProperty("properties");
        if (props != null)
        {
            props.FindPropertyRelative("hideOnForegroundLost").boolValue = true;
            props.FindPropertyRelative("windowQueuePriority").enumValueIndex = (int)WindowPriority.ForceForeground;
            props.FindPropertyRelative("isPopup").boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        UiSettingsScreenList.WireAllPresentScreens();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[LobbyMenuPrefabBuilder] Saved " + PrefabPath + " and wired UISettings.");
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "LobbyMenuWindowController Prefab",
                "Built LobbyMenuWindowController.prefab and refreshed UISettings screens.\n" +
                "Lobby scene should have LobbyUiBootstrap with UISettings assigned.",
                "OK");
        }
    }

    private static T FindBuilt<T>(Transform lobbyRoot, string objectName) where T : Component
    {
        foreach (var tr in lobbyRoot.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name != objectName)
                continue;
            var c = tr.GetComponent<T>();
            if (c != null)
                return c;
        }

        return null;
    }

    private static RectTransform BuildLobbyRoot(RectTransform parent)
    {
        var rootGo = new GameObject(LobbyMenuWindowController.ContentRootName, typeof(RectTransform));
        var root = rootGo.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        StretchFull(root);
        var bg = rootGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.055f, 0.08f, 1f);
        bg.raycastTarget = false;

        var mainGo = new GameObject("MainColumn", typeof(RectTransform));
        var main = mainGo.GetComponent<RectTransform>();
        main.SetParent(root, false);
        main.anchorMin = main.anchorMax = new Vector2(0.5f, 0.5f);
        main.pivot = new Vector2(0.5f, 0.5f);
        main.sizeDelta = new Vector2(MainColumnWidth, 0f);
        main.anchoredPosition = Vector2.zero;

        var mainVlg = mainGo.AddComponent<VerticalLayoutGroup>();
        mainVlg.spacing = 16f;
        mainVlg.padding = new RectOffset(4, 4, 4, 4);
        mainVlg.childAlignment = TextAnchor.UpperCenter;
        mainVlg.childControlHeight = true;
        mainVlg.childControlWidth = true;
        mainVlg.childForceExpandWidth = true;
        mainVlg.childForceExpandHeight = false;

        var mainFitter = mainGo.AddComponent<ContentSizeFitter>();
        mainFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateTmpText(main, "Title", "Lobby", 56, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.93f, 0.94f, 0.98f, 1f);
        var leTitle = title.gameObject.AddComponent<LayoutElement>();
        leTitle.minHeight = 68f;
        leTitle.preferredHeight = 68f;

        AddMenuButton(main, "BtnCreateRoom", "Create room");
        AddMenuButton(main, "BtnJoinGame", "Join game");
        AddMenuButton(main, "BtnSinglePlayer", "Single player");
        AddMenuButton(main, "BtnSettings", "Settings");
        BuildJoinSection(main);

        var hint = CreateTmpText(main, "Hint", "Choose an action above.", 15f, TextAlignmentOptions.Center);
        hint.color = new Color(0.52f, 0.56f, 0.66f, 1f);
        hint.gameObject.AddComponent<LayoutElement>().minHeight = 32f;

        return root;
    }

    private static void BuildJoinSection(RectTransform main)
    {
        var secGo = new GameObject("JoinSection", typeof(RectTransform));
        var sec = secGo.GetComponent<RectTransform>();
        sec.SetParent(main, false);

        var secImg = secGo.AddComponent<Image>();
        secImg.color = new Color(0.1f, 0.115f, 0.15f, 0.98f);
        secImg.raycastTarget = false;

        var secV = secGo.AddComponent<VerticalLayoutGroup>();
        secV.padding = new RectOffset(18, 18, 14, 16);
        secV.spacing = 10f;
        secV.childAlignment = TextAnchor.UpperCenter;
        secV.childControlWidth = true;
        secV.childControlHeight = true;
        secV.childForceExpandWidth = true;
        secV.childForceExpandHeight = false;
        secGo.AddComponent<LayoutElement>().minWidth = MainColumnWidth - 8f;
        secGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sub = CreateTmpText(sec, "JoinSectionTitle", "CONNECTION", 12f, TextAlignmentOptions.Left);
        sub.fontStyle = FontStyles.Bold;
        sub.color = new Color(0.5f, 0.55f, 0.65f, 1f);
        sub.gameObject.AddComponent<LayoutElement>().minHeight = 20f;

        AddLabeledFieldRow(sec, "AddressRow", "Address", "AddressField", "127.0.0.1", "IPv4 or hostname", true);
        AddLabeledFieldRow(sec, "PortRow", "Port", "PortField", "7777", "port", false);
    }

    private static void AddLabeledFieldRow(
        RectTransform parent,
        string rowName,
        string labelText,
        string fieldObjectName,
        string initial,
        string placeholder,
        bool flexibleField)
    {
        var rowGo = new GameObject(rowName, typeof(RectTransform));
        var row = rowGo.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        var h = rowGo.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = flexibleField;

        var label = CreateTmpText(row, "RowLabel", labelText, 17f, TextAlignmentOptions.Left);
        label.color = new Color(0.78f, 0.8f, 0.88f, 1f);
        var leL = label.gameObject.AddComponent<LayoutElement>();
        leL.preferredWidth = 80f;
        leL.minWidth = 80f;
        leL.minHeight = 42f;

        var field = AddTmpInputField(row, fieldObjectName, initial, placeholder);
        var leF = field.gameObject.AddComponent<LayoutElement>();
        leF.minHeight = 42f;
        leF.preferredHeight = 42f;
        if (flexibleField)
            leF.flexibleWidth = 1f;
        else
        {
            leF.preferredWidth = 118f;
            leF.minWidth = 100f;
        }
    }

    private static GameObject BuildSettingsPanel(RectTransform parent)
    {
        var panelGo = new GameObject("SettingsPanel", typeof(RectTransform));
        var panel = panelGo.GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        StretchFull(panel);
        panelGo.SetActive(false);
        var dim = panelGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);

        var box = new GameObject("Box", typeof(RectTransform));
        var boxRt = box.GetComponent<RectTransform>();
        box.transform.SetParent(panel, false);
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(420f, 240f);
        box.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

        var txt = CreateTmpText(boxRt, "SettingsText", "Audio and controls — coming soon.", 20f, TextAlignmentOptions.Center);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = new Vector2(28f, 28f);
        txt.rectTransform.offsetMax = new Vector2(-28f, -28f);
        txt.color = new Color(0.85f, 0.87f, 0.92f, 1f);
        return panelGo;
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

    private static void AddMenuButton(RectTransform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.42f, 0.82f, 1f);
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.15f, 0.42f, 0.82f, 1f);
        colors.highlightedColor = new Color(0.22f, 0.52f, 0.95f, 1f);
        colors.pressedColor = new Color(0.1f, 0.32f, 0.65f, 1f);
        colors.selectedColor = colors.highlightedColor;
        btn.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        StretchFull(labelRt);
        var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFont(tmp);
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 50f;
        le.preferredHeight = 50f;
    }

    private static TMP_Text CreateTmpText(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, Mathf.Max(fontSize + 14f, 28f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFont(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    private static void ApplyDefaultFont(TextMeshProUGUI tmp)
    {
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static TMP_InputField AddTmpInputField(RectTransform parent, string name, string initial, string placeholder)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        root.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 1f);
        var field = root.AddComponent<TMP_InputField>();
        field.lineType = TMP_InputField.LineType.SingleLine;

        var textArea = new GameObject("Text Area", typeof(RectTransform));
        var textAreaRt = textArea.GetComponent<RectTransform>();
        textArea.transform.SetParent(root.transform, false);
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(12f, 8f);
        textAreaRt.offsetMax = new Vector2(-12f, -8f);

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(textArea.transform, false);
        var phRt = phGo.GetComponent<RectTransform>();
        StretchFull(phRt);
        var phTmp = phGo.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFont(phTmp);
        phTmp.text = placeholder;
        phTmp.fontSize = 17f;
        phTmp.fontStyle = FontStyles.Italic;
        phTmp.color = new Color(0.45f, 0.48f, 0.55f, 0.9f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textArea.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        StretchFull(textRt);
        var inputText = textGo.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFont(inputText);
        inputText.text = initial;
        inputText.fontSize = 17f;
        inputText.color = new Color(0.92f, 0.93f, 0.96f, 1f);

        field.textComponent = inputText;
        field.placeholder = phTmp;
        field.textViewport = textAreaRt;
        field.text = initial;
        return field;
    }
}
#endif
