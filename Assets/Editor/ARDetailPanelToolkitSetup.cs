using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class ARDetailPanelToolkitSetup
{
    private const string ToolkitFolder = "Assets/UI Toolkit/DetailPanel";
    private const string PanelSettingsPath = ToolkitFolder + "/ARDetailPanelPanelSettings.asset";
    private const string UxmlPath = ToolkitFolder + "/ARDetailPanel.uxml";
    private const string UssPath = ToolkitFolder + "/ARDetailPanel.uss";

    [MenuItem("Tools/AR/Setup Detail Panel UI Toolkit")]
    public static void Setup()
    {
        EnsureFolder("Assets/UI Toolkit");
        EnsureFolder(ToolkitFolder);

        PanelSettings panelSettings = LoadOrCreatePanelSettings();
        VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

        if (visualTreeAsset == null)
        {
            Debug.LogError($"VisualTreeAsset not found: {UxmlPath}");
            return;
        }

        GameObject documentObject = GameObject.Find("ARDetailPanelUIDocument");
        if (documentObject == null)
        {
            documentObject = new GameObject("ARDetailPanelUIDocument");
            Undo.RegisterCreatedObjectUndo(documentObject, "Create ARDetailPanelUIDocument");
        }

        UIDocument uiDocument = GetOrAddComponent<UIDocument>(documentObject);
        ARDetailPanelDocumentController controller = GetOrAddComponent<ARDetailPanelDocumentController>(documentObject);

        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = visualTreeAsset;

        if (styleSheet != null && panelSettings != null)
        {
            SerializedObject serializedPanel = new SerializedObject(panelSettings);
            SerializedProperty themeStyleSheet = serializedPanel.FindProperty("themeStyleSheet");
            if (themeStyleSheet != null && themeStyleSheet.objectReferenceValue == null)
            {
                themeStyleSheet.objectReferenceValue = styleSheet;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(panelSettings);
            }
        }

        ARUIManager uiManager = Object.FindFirstObjectByType<ARUIManager>();
        if (uiManager != null)
        {
            Undo.RecordObject(uiManager, "Assign UI Toolkit detail panel");
            uiManager.uiToolkitDetailPanel = controller;
            EditorUtility.SetDirty(uiManager);
        }

        Selection.activeGameObject = documentObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("UI Toolkit detail panel setup completed. Open the scene and press Play to verify the panel.");
    }

    private static PanelSettings LoadOrCreatePanelSettings()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings != null)
        {
            return panelSettings;
        }

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        EditorUtility.SetDirty(panelSettings);
        AssetDatabase.SaveAssets();
        return panelSettings;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parentPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(assetPath);

        if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
        {
            EnsureFolder(parentPath);
        }

        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(target);
        }

        return component;
    }
}
