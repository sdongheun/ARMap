using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class QuickInfoPreviewSceneSetup
{
    private const string ScenePath = "Assets/Scenes/QuickInfoPreviewScene.unity";
    private const string IconPath = "Assets/UI Toolkit/DetailPanel/Icons/building-check-svgrepo-com.png";

    [MenuItem("Tools/AR/Create Quick Info Preview Scene")]
    public static void CreateQuickInfoPreviewScene()
    {
        SceneSetupResult result = BuildScene();
        EditorSceneManager.SaveScene(result.Scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePath);
        Selection.activeGameObject = result.CanvasObject;
        Debug.Log("Quick info preview scene created. Open Assets/Scenes/QuickInfoPreviewScene.unity and press Play to preview the bottom label.");
    }

    private static SceneSetupResult BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ARUIManager), typeof(QuickInfoPreview));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create QuickInfo Preview Canvas");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1179f, 2556f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Sprite roundedSprite = LoadSprite("Assets/diagram/QuickInfoCard_BG.png");
        Sprite buildingIcon = LoadSprite(IconPath);

        GameObject scanningCard = CreateTopCard("ScanningCard", canvasObject.transform, roundedSprite, "앵커가 표시되고 있습니다");
        GameObject detectedCard = CreateTopCard("DetectedCard", canvasObject.transform, roundedSprite, "건물 감지됨");
        detectedCard.SetActive(false);

        QuickCardParts quickCard = CreateQuickInfoCard(canvasObject.transform, roundedSprite, buildingIcon);

        ARUIManager uiManager = canvasObject.GetComponent<ARUIManager>();
        uiManager.scanningCard = scanningCard;
        uiManager.detectedCard = detectedCard;
        uiManager.quickInfoCard = quickCard.Root;
        uiManager.quickInfoIcon = quickCard.Icon;
        uiManager.quickBuildingNameText = quickCard.Title;
        uiManager.quickCategoryText = quickCard.Category;
        uiManager.quickDistanceText = quickCard.Address;
        uiManager.quickInfoTapTarget = quickCard.DetailButton;
        uiManager.iconBuilding = buildingIcon;
        uiManager.quickCardPosY = -65f;

        return new SceneSetupResult
        {
            Scene = scene,
            CanvasObject = canvasObject
        };
    }

    private static GameObject CreateTopCard(string name, Transform parent, Sprite backgroundSprite, string message)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -80f);
        rect.sizeDelta = new Vector2(760f, 140f);

        Image image = card.GetComponent<Image>();
        image.sprite = backgroundSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 1f, 1f, 0.92f);

        CreateText("Message", card.transform, message, 34, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(640f, 64f));
        return card;
    }

    private static QuickCardParts CreateQuickInfoCard(Transform parent, Sprite backgroundSprite, Sprite buildingIcon)
    {
        GameObject card = new GameObject("QuickInfoCard", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(16f, 0f);
        rect.offsetMax = new Vector2(-16f, 140f);
        rect.sizeDelta = new Vector2(0f, 140f);

        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = backgroundSprite;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = new Color(1f, 1f, 1f, 0.96f);

        GameObject iconObject = new GameObject("BuildingIconImage", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(card.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(42f, 18f);
        iconRect.sizeDelta = new Vector2(24f, 24f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = buildingIcon;
        iconImage.preserveAspect = true;

        TextMeshProUGUI title = CreateText("BuildingNameText", card.transform, "인제대학교 대학원", 26, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(132f, 24f), new Vector2(560f, 38f));
        TextMeshProUGUI category = CreateText("CategoryText", card.transform, "키워드 검색", 14, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(132f, -6f), new Vector2(400f, 24f));
        TextMeshProUGUI address = CreateText("AddressText", card.transform, "약 130m", 16, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(132f, -34f), new Vector2(240f, 28f));

        GameObject buttonObject = new GameObject("OpenDetailButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-28f, 0f);
        buttonRect.sizeDelta = new Vector2(140f, 52f);
        buttonObject.GetComponent<Image>().color = new Color(0.1f, 0.45f, 0.84f, 0.12f);
        CreateText("ButtonLabel", buttonObject.transform, "상세 보기", 18, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(140f, 52f));

        return new QuickCardParts
        {
            Root = card,
            Icon = iconImage,
            Title = title,
            Category = category,
            Address = address,
            DetailButton = buttonObject.GetComponent<Button>()
        };
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = name == "CategoryText" || name == "AddressText"
            ? new Color(0.18f, 0.18f, 0.18f, 0.82f)
            : new Color(0.07f, 0.07f, 0.08f, 1f);
        tmp.enableWordWrapping = false;
        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        return tmp;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private sealed class SceneSetupResult
    {
        public UnityEngine.SceneManagement.Scene Scene;
        public GameObject CanvasObject;
    }

    private sealed class QuickCardParts
    {
        public GameObject Root;
        public Image Icon;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Category;
        public TextMeshProUGUI Address;
        public Button DetailButton;
    }
}
