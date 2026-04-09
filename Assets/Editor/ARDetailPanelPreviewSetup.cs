using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class ARDetailPanelPreviewSetup
{
    private const string ToolkitFolder = "Assets/UI Toolkit/DetailPanel";
    private const string PanelSettingsPath = ToolkitFolder + "/ARDetailPanelPanelSettings.asset";
    private const string UxmlPath = ToolkitFolder + "/ARDetailPanel.uxml";

    [MenuItem("Tools/AR/Setup Detail Panel Preview In Current Scene")]
    public static void SetupPreviewInCurrentScene()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

        if (panelSettings == null || visualTreeAsset == null)
        {
            Debug.LogError("UI Toolkit detail panel assets are missing. Run 'Tools/AR/Setup Detail Panel UI Toolkit' first.");
            return;
        }

        GameObject previewObject = GameObject.Find("ARDetailPanelPreviewDocument");
        if (previewObject == null)
        {
            previewObject = new GameObject("ARDetailPanelPreviewDocument");
            Undo.RegisterCreatedObjectUndo(previewObject, "Create AR detail panel preview");
        }

        UIDocument uiDocument = GetOrAddComponent<UIDocument>(previewObject);
        ARDetailPanelDocumentController controller = GetOrAddComponent<ARDetailPanelDocumentController>(previewObject);
        ARDetailPanelPreview preview = GetOrAddComponent<ARDetailPanelPreview>(previewObject);

        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = visualTreeAsset;

        preview.ApplyPreviewFromEditor();

        Selection.activeGameObject = previewObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("AR detail panel preview has been added to the current scene. Select ARDetailPanelPreviewDocument to edit sample text in the Inspector.");
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
