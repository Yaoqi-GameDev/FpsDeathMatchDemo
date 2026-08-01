#if UNITY_EDITOR
using UIFramework;
using UnityEditor;
using UnityEngine;

/// <summary>Keeps <see cref="UISettings.screensToRegister"/> filled with known screen prefabs.</summary>
public static class UiSettingsScreenList
{
    public const string UiSettingsPath = "Assets/01_Project/Data/UISettings/UISettings.asset";
    public const string ScreensDir = "Assets/01_Project/Prefabs/UI/Screens";

    public static readonly string[] ScreenPrefabPaths =
    {
        ScreensDir + "/LobbyMenuWindowController.prefab",
        ScreensDir + "/EndGameWindowController.prefab",
    };

    public static void WireAllPresentScreens()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UISettings>(UiSettingsPath);
        if (settings == null)
        {
            Debug.LogError("[UiSettingsScreenList] UISettings not found at " + UiSettingsPath);
            return;
        }

        var so = new SerializedObject(settings);
        var listProp = so.FindProperty("screensToRegister");
        listProp.ClearArray();

        int index = 0;
        foreach (var path in ScreenPrefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;
            if (prefab.GetComponent<IScreenController>() == null)
            {
                Debug.LogError("[UiSettingsScreenList] Missing IScreenController on " + path);
                continue;
            }

            listProp.InsertArrayElementAtIndex(index);
            listProp.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
            index++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }
}
#endif
