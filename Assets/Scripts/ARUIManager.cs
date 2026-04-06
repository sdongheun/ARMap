using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class ARUIManager : MonoBehaviour
{
    [Serializable]
    public class ScreenMarkerData
    {
        public string id;
        public string label;
        public string category;
        public string address;
        public Vector2 screenPosition;
        public bool isSelected;
    }

    private class ScreenMarkerView
    {
        public GameObject root;
        public RectTransform rectTransform;
        public Image pinShadowImage;
        public RectTransform pinShadowRect;
        public Vector2 pinShadowHiddenPosition;
        public Vector2 pinShadowShownPosition;
        public Image pinImage;
        public RectTransform pinRect;
        public Vector2 pinHiddenPosition;
        public Vector2 pinShownPosition;
        public Image bubbleBackground;
        public Image bubbleShadow;
        public RectTransform bubbleShadowRect;
        public RectTransform bubbleRect;
        public CanvasGroup bubbleGroup;
        public Vector2 bubbleHiddenPosition;
        public Vector2 bubbleShownPosition;
        public Image bubbleIcon;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI categoryText;
        public TextMeshProUGUI addressText;
        public Button bubbleButton;
        public bool isSelectedTarget;
        public float selectionLerp;
    }

    // --- Inspector Variables ---
    [Header("1. Main Cards (1~3)")]
    public GameObject scanningCard;
    public GameObject detectedCard;
    public GameObject quickInfoCard;

    [Header("2. Quick Info Content")]
    public Image quickInfoIcon;
    [FormerlySerializedAs("quickTitleText")]
    public TextMeshProUGUI quickBuildingNameText;
    public TextMeshProUGUI quickCategoryText; 
    [FormerlySerializedAs("quickAddressText")]
    public TextMeshProUGUI quickDistanceText;
    
    [Header("3. Main Buttons")]
    [FormerlySerializedAs("openDetailButton")]
    public Button quickInfoTapTarget;

    [Header("4. Icons")]
    public Sprite iconScanning;
    public Sprite iconDetected;
    public Sprite iconBuilding;
    public Sprite screenMarkerDefaultSprite;
    public Sprite screenMarkerSelectedSprite;

    [Header("5. Detail View (Page 4)")]
    public ARDetailPanelDocumentController uiToolkitDetailPanel;

    [Header("6. Animation Settings")]
    public float animDuration = 0.3f; 
    public float slideOffset = 150f; 
    public float statusCardPosY = 0f;    
    public float quickCardPosY = -50f;   

    [Header("7. Toast Message")]
    public GameObject toastPanel;
    public TextMeshProUGUI toastText;
    public float toastDuration = 2.0f;
    private Coroutine _toastRoutine;

    [Header("8. Center Reticle")]
    public bool showCenterReticle = true;
    public Color centerReticleColor = new Color(1f, 1f, 1f, 0.72f);
    public float centerReticleBarLength = 16f;
    public float centerReticleBarThickness = 3f;
    public float centerReticleGap = 10f;
    public float centerReticlePulseDuration = 2.2f;
    public float centerReticlePulseAlphaMin = 0.32f;
    public float centerReticlePulseAlphaMax = 0.72f;
    public float centerReticlePulseScale = 1.04f;
    public float screenMarkerCornerRadius = 18f;
    public Vector2 screenMarkerShadowOffset = new Vector2(0f, -10f);
    public float screenMarkerShadowExpansion = 10f;
    public Color screenMarkerShadowColor = new Color(0.08f, 0.12f, 0.2f, 0.22f);

    // --- Internal Variables ---
    public event Action OnClickDetail;
    public event Action OnDetailOpened;
    public event Action OnDetailClosed;
    private enum UIState { None, Scanning, Detected, QuickInfo }
    private UIState currentState = UIState.None;
    private BuildingData _currentDetailData;

    private RectTransform _scanRect, _detectRect, _quickRect;
    private CanvasGroup _scanGroup, _detectGroup, _quickGroup;
    private Coroutine _scanRoutine, _detectRoutine, _quickRoutine;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private RectTransform _screenMarkerRoot;
    private RectTransform _centerReticleRoot;
    private Sprite _screenMarkerPanelSprite;
    private Texture2D _screenMarkerPanelTexture;
    private readonly Dictionary<string, ScreenMarkerView> _screenMarkerViews = new Dictionary<string, ScreenMarkerView>();
    private string _lastQuickInfoId;

    void Awake()
    {
        _scanRect = scanningCard.GetComponent<RectTransform>(); _scanGroup = scanningCard.GetComponent<CanvasGroup>();
        _detectRect = detectedCard.GetComponent<RectTransform>(); _detectGroup = detectedCard.GetComponent<CanvasGroup>();
        _quickRect = quickInfoCard.GetComponent<RectTransform>(); _quickGroup = quickInfoCard.GetComponent<CanvasGroup>();
        _canvasRect = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
        _screenMarkerPanelSprite = CreateRoundedPanelSprite();
    }

    void Start()
    {
        ConfigureQuickInfoCardLayout();
        if (screenMarkerSelectedSprite == null)
        {
            screenMarkerSelectedSprite = iconBuilding;
        }

        if (screenMarkerDefaultSprite == null)
        {
            screenMarkerDefaultSprite = screenMarkerSelectedSprite;
        }

        if (quickInfoTapTarget != null)
        {
            quickInfoTapTarget.onClick.AddListener(() => OnClickDetail?.Invoke());
        }

        if (uiToolkitDetailPanel != null)
        {
            uiToolkitDetailPanel.OnClosed += HandleUIToolkitDetailClosed;
            uiToolkitDetailPanel.OnPhoneRequested += OnCallPhone;
            uiToolkitDetailPanel.OnCopyRequested += OnCopyAddress;
            uiToolkitDetailPanel.OnShareRequested += OnShareDetail;
            uiToolkitDetailPanel.OnMapRequested += OnOpenMap;
        }

        InitializeCard(_scanRect, _scanGroup, true, statusCardPosY);
        InitializeCard(_detectRect, _detectGroup, false, statusCardPosY);
        InitializeQuickInfoCard(false);

        EnsureScreenMarkerRoot();
        EnsureCenterReticle();
        SetPrimaryButtonsVisible(false);
    }

    Sprite CreateRoundedPanelSprite()
    {
        const int textureSize = 64;
        int radius = Mathf.Clamp(Mathf.RoundToInt(screenMarkerCornerRadius), 2, textureSize / 2 - 2);
        _screenMarkerPanelTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _screenMarkerPanelTexture.name = "ScreenMarkerPanelTexture";
        _screenMarkerPanelTexture.wrapMode = TextureWrapMode.Clamp;
        _screenMarkerPanelTexture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[textureSize * textureSize];
        float innerMin = radius - 1f;
        float radiusSq = radius * radius;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nearestX = Mathf.Clamp(x, radius, textureSize - radius - 1);
                float nearestY = Mathf.Clamp(y, radius, textureSize - radius - 1);
                float dx = x - nearestX;
                float dy = y - nearestY;
                bool inside = (dx * dx + dy * dy) <= radiusSq || (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) < 0.01f);
                pixels[(y * textureSize) + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        _screenMarkerPanelTexture.SetPixels32(pixels);
        _screenMarkerPanelTexture.Apply();

        return Sprite.Create(
            _screenMarkerPanelTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }

    void Update()
    {
        UpdateScreenMarkerAnimations();
        UpdateCenterReticleAnimation();
    }

    #region Main Card State Control
    // 화면 상태 전환 (1번: 스캔 중)
    public void SetScanningMode()
    {
        if (currentState == UIState.Scanning) return;
        currentState = UIState.Scanning;
        _lastQuickInfoId = null;
        SetPrimaryButtonsVisible(false);
        ShowCard(scanningCard, _scanRect, _scanGroup, statusCardPosY, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        HideQuickInfoCard();
    }

    // 화면 상태 전환 (2번: 건물 감지됨)
    public void SetDetectedMode()
    {
        if (currentState == UIState.Detected) return;
        currentState = UIState.Detected;
        _lastQuickInfoId = null;
        SetPrimaryButtonsVisible(false);
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        ShowCard(detectedCard, _detectRect, _detectGroup, statusCardPosY, ref _detectRoutine);
        HideQuickInfoCard();
    }

    // 화면 상태 전환 (3번: 요약 정보 표시)
    public void ShowQuickInfo(BuildingData data)
    {
        ShowQuickInfo(data, -1f);
    }

    public void ShowQuickInfo(BuildingData data, float distanceMeters)
    {
        if (data == null)
        {
            return;
        }

        string infoId = BuildQuickInfoId(data);
        bool hasChanged = _lastQuickInfoId != infoId;
        bool enteringQuickInfo = currentState != UIState.QuickInfo;
        currentState = UIState.QuickInfo;
        _lastQuickInfoId = infoId;
        if (quickInfoIcon != null) quickInfoIcon.sprite = iconBuilding;
        if (quickBuildingNameText != null)
        {
            quickBuildingNameText.text = data.buildingName;
        }
        if (quickCategoryText != null)
        {
            quickCategoryText.text = string.IsNullOrEmpty(data.description) ? "건물 정보" : data.description;
        }
        if (quickDistanceText != null)
        {
            quickDistanceText.text = distanceMeters >= 0f
                ? $"약 {Mathf.RoundToInt(distanceMeters)}m"
                : (string.IsNullOrEmpty(data.fetchedAddress) ? "위치 정보 없음" : data.fetchedAddress);
        }
        SetPrimaryButtonsVisible(false);

        if (enteringQuickInfo)
        {
            HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
            HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
            HideQuickInfoCard();
        }
        else if (hasChanged)
        {
            HideQuickInfoCard();
        }
    }
    #endregion

    void SetPrimaryButtonsVisible(bool visible)
    {
        if (quickInfoTapTarget != null)
        {
            quickInfoTapTarget.gameObject.SetActive(visible);
        }
    }

    public void UpdateScreenMarkers(List<ScreenMarkerData> markerDataList)
    {
        EnsureScreenMarkerRoot();

        HashSet<string> activeIds = new HashSet<string>();
        foreach (ScreenMarkerData data in markerDataList)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.id)) continue;

            activeIds.Add(data.id);
            ScreenMarkerView view = GetOrCreateScreenMarkerView(data.id);
            view.root.SetActive(true);
            view.rectTransform.anchoredPosition = ClampToCanvas(data.screenPosition);
            float pinSize = data.isSelected ? 72f : 60f;
            if (view.pinRect != null)
            {
                view.pinRect.sizeDelta = new Vector2(pinSize, pinSize);
            }
            if (view.pinShadowRect != null)
            {
                view.pinShadowRect.sizeDelta = new Vector2(pinSize + 10f, pinSize + 10f);
            }
            if (view.pinShadowImage != null)
            {
                view.pinShadowImage.color = data.isSelected
                    ? new Color(0f, 0f, 0f, 0.28f)
                    : new Color(0f, 0f, 0f, 0.2f);
            }
            if (view.pinImage != null)
            {
                view.pinImage.sprite = data.isSelected ? screenMarkerSelectedSprite : screenMarkerDefaultSprite;
                view.pinImage.color = data.isSelected
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.96f);
            }
            view.isSelectedTarget = data.isSelected;
            if (view.titleText != null) view.titleText.text = data.label;
            if (view.categoryText != null) view.categoryText.text = string.IsNullOrWhiteSpace(data.category) ? "건물 정보" : data.category;
            if (view.addressText != null) view.addressText.text = string.IsNullOrWhiteSpace(data.address) ? "주소 정보 없음" : data.address;
            if (view.bubbleIcon != null) view.bubbleIcon.sprite = iconBuilding;
            if (view.bubbleButton != null) view.bubbleButton.interactable = data.isSelected;
            UpdateScreenMarkerBubbleLayout(view);
        }

        foreach (var pair in _screenMarkerViews)
        {
            if (pair.Value?.root != null)
            {
                pair.Value.root.SetActive(activeIds.Contains(pair.Key));
            }
        }
    }

    public void ClearScreenMarkers()
    {
        foreach (var pair in _screenMarkerViews)
        {
            if (pair.Value?.root != null)
            {
                pair.Value.root.SetActive(false);
            }
        }
    }

    void EnsureScreenMarkerRoot()
    {
        if (_screenMarkerRoot != null) return;

        GameObject rootObject = new GameObject("ScreenMarkerRoot", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        _screenMarkerRoot = rootObject.GetComponent<RectTransform>();
        _screenMarkerRoot.anchorMin = Vector2.zero;
        _screenMarkerRoot.anchorMax = Vector2.one;
        _screenMarkerRoot.offsetMin = Vector2.zero;
        _screenMarkerRoot.offsetMax = Vector2.zero;
        _screenMarkerRoot.SetAsLastSibling();
    }

    void EnsureCenterReticle()
    {
        if (!showCenterReticle)
        {
            if (_centerReticleRoot != null)
            {
                _centerReticleRoot.gameObject.SetActive(false);
            }
            return;
        }

        if (_centerReticleRoot == null)
        {
            GameObject rootObject = new GameObject("CenterReticle", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            _centerReticleRoot = rootObject.GetComponent<RectTransform>();
            _centerReticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.pivot = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.anchoredPosition = Vector2.zero;
            _centerReticleRoot.sizeDelta = Vector2.zero;

            CreateCenterReticleBar("TopBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, centerReticleGap), false);
            CreateCenterReticleBar("BottomBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -centerReticleGap), false);
            CreateCenterReticleBar("LeftBar", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-centerReticleGap, 0f), true);
            CreateCenterReticleBar("RightBar", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(centerReticleGap, 0f), true);
        }

        _centerReticleRoot.gameObject.SetActive(true);
        _centerReticleRoot.SetAsLastSibling();
    }

    void CreateCenterReticleBar(string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, bool horizontal)
    {
        if (_centerReticleRoot == null)
        {
            return;
        }

        GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(_centerReticleRoot, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = anchor;
        barRect.anchorMax = anchor;
        barRect.pivot = pivot;
        barRect.anchoredPosition = anchoredPosition;
        barRect.sizeDelta = horizontal
            ? new Vector2(centerReticleBarLength, centerReticleBarThickness)
            : new Vector2(centerReticleBarThickness, centerReticleBarLength);

        Image barImage = barObject.GetComponent<Image>();
        barImage.color = centerReticleColor;
        barImage.raycastTarget = false;
    }

    void UpdateCenterReticleAnimation()
    {
        if (_centerReticleRoot == null || !_centerReticleRoot.gameObject.activeSelf)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, centerReticlePulseDuration);
        float cycle = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / duration)) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(centerReticlePulseAlphaMin, centerReticlePulseAlphaMax, cycle);
        float scale = Mathf.Lerp(1f, centerReticlePulseScale, cycle);

        _centerReticleRoot.localScale = Vector3.one * scale;

        for (int i = 0; i < _centerReticleRoot.childCount; i++)
        {
            Image barImage = _centerReticleRoot.GetChild(i).GetComponent<Image>();
            if (barImage == null)
            {
                continue;
            }

            Color c = centerReticleColor;
            c.a *= alpha;
            barImage.color = c;
        }
    }

    ScreenMarkerView GetOrCreateScreenMarkerView(string id)
    {
        if (_screenMarkerViews.TryGetValue(id, out ScreenMarkerView existingView) && existingView?.root != null)
        {
            return existingView;
        }

        GameObject rootObject = new GameObject($"ScreenMarker_{id}", typeof(RectTransform));
        rootObject.transform.SetParent(_screenMarkerRoot, false);

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(680f, 360f);

        Image pinShadowImage = CreateScreenMarkerImage("PinShadow", rootObject.transform, 5f, -3f, 70f, 70f);
        Image pinImage = CreateScreenMarkerImage("Pin", rootObject.transform, 0f, 0f, 60f, 60f);
        Vector2 pinShadowHiddenPosition = new Vector2(5f, -3f);
        Vector2 pinShadowShownPosition = new Vector2(2f, 10f);
        Vector2 pinHiddenPosition = new Vector2(0f, 0f);
        Vector2 pinShownPosition = new Vector2(0f, 12f);
        if (pinShadowImage != null)
        {
            pinShadowImage.color = new Color(0f, 0f, 0f, 0.2f);
        }
        if (pinImage != null)
        {
            pinImage.color = new Color(1f, 1f, 1f, 0.96f);
        }

        Vector2 bubbleHiddenPosition = new Vector2(0f, 6f);
        Vector2 bubbleShownPosition = new Vector2(0f, 42f);

        GameObject bubbleShadowObject = new GameObject("BubbleShadow", typeof(RectTransform), typeof(Image));
        bubbleShadowObject.transform.SetParent(rootObject.transform, false);

        RectTransform bubbleShadowRect = bubbleShadowObject.GetComponent<RectTransform>();
        bubbleShadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleShadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleShadowRect.pivot = new Vector2(0.5f, 0f);
        bubbleShadowRect.anchoredPosition = bubbleHiddenPosition + screenMarkerShadowOffset;
        bubbleShadowRect.sizeDelta = new Vector2(230f, 114f);

        Image bubbleShadow = bubbleShadowObject.GetComponent<Image>();
        bubbleShadow.sprite = _screenMarkerPanelSprite;
        bubbleShadow.type = Image.Type.Sliced;
        bubbleShadow.color = screenMarkerShadowColor;
        bubbleShadow.raycastTarget = false;

        GameObject bubbleObject = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Button));
        bubbleObject.transform.SetParent(rootObject.transform, false);

        RectTransform bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.anchoredPosition = bubbleHiddenPosition;
        bubbleRect.sizeDelta = new Vector2(220f, 104f);

        Image bubbleBackground = bubbleObject.GetComponent<Image>();
        bubbleBackground.sprite = _screenMarkerPanelSprite;
        bubbleBackground.type = Image.Type.Sliced;
        bubbleBackground.color = new Color(1f, 1f, 1f, 0.82f);
        bubbleBackground.raycastTarget = true;

        CanvasGroup bubbleGroup = bubbleObject.GetComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        bubbleGroup.blocksRaycasts = false;
        bubbleGroup.interactable = false;
        bubbleObject.transform.localScale = new Vector3(0.2f, 0.12f, 1f);

        Button bubbleButton = bubbleObject.GetComponent<Button>();
        bubbleButton.transition = Selectable.Transition.None;
        bubbleButton.interactable = false;
        bubbleButton.onClick.AddListener(() => OnClickDetail?.Invoke());

        RectTransform bubbleIconRect = CreateRectTransformChild(
            "BubbleIcon",
            bubbleObject.transform,
            new Vector2(18f, -16f),
            new Vector2(24f, 24f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));
        Image bubbleIcon = bubbleIconRect.gameObject.AddComponent<Image>();
        bubbleIcon.sprite = iconBuilding;
        bubbleIcon.preserveAspect = true;
        bubbleIcon.raycastTarget = false;

        TextMeshProUGUI titleText = CreateScreenMarkerText("Title", bubbleObject.transform, 0f, 0f, 180f, 24f, TextAlignmentOptions.TopLeft);
        TextMeshProUGUI categoryText = CreateScreenMarkerText("Category", bubbleObject.transform, 0f, 0f, 180f, 20f, TextAlignmentOptions.MidlineLeft);
        TextMeshProUGUI addressText = CreateScreenMarkerText("Address", bubbleObject.transform, 0f, 0f, 72f, 20f, TextAlignmentOptions.MidlineRight);
        ConfigureBubbleText(titleText, 17f, FontStyles.Bold, new Color(0.06f, 0.07f, 0.1f, 1f), TextWrappingModes.NoWrap, TextOverflowModes.Overflow);
        titleText.overflowMode = TextOverflowModes.Overflow;
        ConfigureBubbleText(categoryText, 12f, FontStyles.Normal, new Color(0.28f, 0.33f, 0.41f, 1f), TextWrappingModes.NoWrap, TextOverflowModes.Ellipsis);
        ConfigureBubbleText(addressText, 12f, FontStyles.Bold, new Color(0.18f, 0.22f, 0.28f, 0.95f), TextWrappingModes.NoWrap, TextOverflowModes.Ellipsis);

        ScreenMarkerView view = new ScreenMarkerView
        {
            root = rootObject,
            rectTransform = rootRect,
            pinShadowImage = pinShadowImage,
            pinShadowRect = pinShadowImage != null ? pinShadowImage.rectTransform : null,
            pinShadowHiddenPosition = pinShadowHiddenPosition,
            pinShadowShownPosition = pinShadowShownPosition,
            pinImage = pinImage,
            pinRect = pinImage != null ? pinImage.rectTransform : null,
            pinHiddenPosition = pinHiddenPosition,
            pinShownPosition = pinShownPosition,
            bubbleBackground = bubbleBackground,
            bubbleShadow = bubbleShadow,
            bubbleShadowRect = bubbleShadowRect,
            bubbleRect = bubbleRect,
            bubbleGroup = bubbleGroup,
            bubbleHiddenPosition = bubbleHiddenPosition,
            bubbleShownPosition = bubbleShownPosition,
            bubbleIcon = bubbleIcon,
            titleText = titleText,
            categoryText = categoryText,
            addressText = addressText,
            bubbleButton = bubbleButton,
            isSelectedTarget = false,
            selectionLerp = 0f
        };

        UpdateScreenMarkerBubbleLayout(view);
        _screenMarkerViews[id] = view;
        return view;
    }

    TextMeshProUGUI CreateScreenMarkerText(string name, Transform parent, float posX, float posY, float width, float height, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(posX, posY);
        rect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        TMP_FontAsset fallbackFont = null;
        Material fallbackMaterial = null;
        if (quickBuildingNameText != null)
        {
            fallbackFont = quickBuildingNameText.font;
            fallbackMaterial = quickBuildingNameText.fontSharedMaterial;
        }
        else if (quickCategoryText != null)
        {
            fallbackFont = quickCategoryText.font;
            fallbackMaterial = quickCategoryText.fontSharedMaterial;
        }
        else if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
        {
            fallbackFont = TMP_Settings.defaultFontAsset;
        }

        if (fallbackFont != null)
        {
            tmp.font = fallbackFont;
        }

        if (fallbackMaterial != null)
        {
            tmp.fontSharedMaterial = fallbackMaterial;
        }
        return tmp;
    }

    RectTransform CreateRectTransformChild(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    void ConfigureBubbleText(TextMeshProUGUI text, float fontSize, FontStyles fontStyle, Color color, TextWrappingModes wrappingMode, TextOverflowModes overflowMode)
    {
        if (text == null) return;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.textWrappingMode = wrappingMode;
        text.overflowMode = overflowMode;
        text.raycastTarget = false;
    }

    string WrapTextByWordBoundary(TextMeshProUGUI text, string value, float maxWidth)
    {
        if (text == null || string.IsNullOrWhiteSpace(value) || maxWidth <= 0f)
        {
            return value ?? string.Empty;
        }

        string normalized = value.Replace("\r\n", "\n");
        string[] paragraphs = normalized.Split('\n');
        List<string> wrappedLines = new List<string>();

        foreach (string paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            string currentLine = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string candidateLine = $"{currentLine} {words[i]}";
                float singleLineWidth = text.GetPreferredValues(candidateLine, 0f, 0f).x;
                if (singleLineWidth <= maxWidth)
                {
                    currentLine = candidateLine;
                    continue;
                }

                wrappedLines.Add(currentLine);
                currentLine = words[i];
            }

            wrappedLines.Add(currentLine);
        }

        return string.Join("\n", wrappedLines);
    }

    float GetWrappedTextMaxLineWidth(TextMeshProUGUI text, string wrappedValue)
    {
        if (text == null || string.IsNullOrEmpty(wrappedValue))
        {
            return 0f;
        }

        string[] lines = wrappedValue.Split('\n');
        float maxLineWidth = 0f;
        foreach (string line in lines)
        {
            maxLineWidth = Mathf.Max(maxLineWidth, text.GetPreferredValues(line, 0f, 0f).x);
        }

        return maxLineWidth;
    }

    float GetRenderedTextHeight(TextMeshProUGUI text, string value, float width, float fallbackMinHeight)
    {
        if (text == null)
        {
            return fallbackMinHeight;
        }

        RectTransform rect = text.rectTransform;
        Vector2 previousSize = rect.sizeDelta;
        string previousText = text.text;

        rect.sizeDelta = new Vector2(width, Mathf.Max(previousSize.y, fallbackMinHeight));
        if (text.text != value)
        {
            text.text = value;
        }

        text.ForceMeshUpdate();
        float renderedHeight = Mathf.Max(fallbackMinHeight, text.preferredHeight);

        rect.sizeDelta = previousSize;
        if (text.text != previousText)
        {
            text.text = previousText;
            text.ForceMeshUpdate();
        }

        return renderedHeight;
    }

    Image CreateScreenMarkerImage(string name, Transform parent, float posX, float posY, float width, float height)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(posX, posY);
        rect.sizeDelta = new Vector2(width, height);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = screenMarkerDefaultSprite != null ? screenMarkerDefaultSprite : screenMarkerSelectedSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    Image CreateScreenMarkerBackground(Transform parent, float posX, float posY, float width, float height)
    {
        GameObject backgroundObject = new GameObject("LabelBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(posX, posY);
        rect.sizeDelta = new Vector2(width, height);

        Image image = backgroundObject.GetComponent<Image>();
        image.color = new Color(0.06f, 0.08f, 0.12f, 0.78f);
        image.raycastTarget = false;
        return image;
    }

    void UpdateScreenMarkerBubbleLayout(ScreenMarkerView view)
    {
        if (view == null || view.bubbleRect == null || view.titleText == null || view.categoryText == null || view.addressText == null)
        {
            return;
        }

        const float leftPadding = 24f;
        const float rightPadding = 28f;
        const float topPadding = 22f;
        const float iconSize = 26f;
        const float iconGap = 12f;
        const float innerGap = 4f;
        const float distanceGap = 16f;
        const float bubbleMinWidth = 220f;
        const float bubbleMinHeight = 70f;

        float bubbleMaxWidth = 420f;
        if (_canvasRect != null)
        {
            bubbleMaxWidth = Mathf.Clamp(_canvasRect.rect.width - 96f, 320f, 620f);
        }

        bool hasCategory = !string.IsNullOrWhiteSpace(view.categoryText.text);
        bool hasDistance = !string.IsNullOrWhiteSpace(view.addressText.text);
        string rawTitle = view.titleText.text ?? string.Empty;

        float maxDistanceWidth = 88f;
        float distancePreferredWidth = hasDistance
            ? view.addressText.GetPreferredValues(view.addressText.text, maxDistanceWidth, 0f).x
            : 0f;
        float distanceColumnWidth = hasDistance
            ? Mathf.Clamp(distancePreferredWidth, 52f, maxDistanceWidth)
            : 0f;
        float distanceSectionWidth = hasDistance ? distanceGap + distanceColumnWidth : 0f;

        float textMaxWidth = bubbleMaxWidth - leftPadding - rightPadding - iconSize - iconGap - distanceSectionWidth;
        float actualMiddleWidth = Mathf.Max(96f, textMaxWidth);

        string wrappedTitle = WrapTextByWordBoundary(view.titleText, rawTitle, actualMiddleWidth);
        if (view.titleText.text != wrappedTitle)
        {
            view.titleText.text = wrappedTitle;
        }

        float titleWidth = Mathf.Min(actualMiddleWidth, GetWrappedTextMaxLineWidth(view.titleText, wrappedTitle));
        float categoryWidth = hasCategory
            ? Mathf.Min(actualMiddleWidth, view.categoryText.GetPreferredValues(view.categoryText.text, 0f, 0f).x)
            : 0f;
        float middleColumnWidth = Mathf.Clamp(Mathf.Max(titleWidth, categoryWidth, 96f), 96f, textMaxWidth);
        float bubbleWidth = Mathf.Clamp(
            leftPadding + iconSize + iconGap + middleColumnWidth + distanceSectionWidth + rightPadding,
            bubbleMinWidth,
            bubbleMaxWidth);
        actualMiddleWidth = bubbleWidth - leftPadding - rightPadding - iconSize - iconGap - distanceSectionWidth;

        wrappedTitle = WrapTextByWordBoundary(view.titleText, rawTitle, actualMiddleWidth);
        if (view.titleText.text != wrappedTitle)
        {
            view.titleText.text = wrappedTitle;
        }

        float titleHeight = GetRenderedTextHeight(view.titleText, wrappedTitle, actualMiddleWidth, 20f);
        float categoryHeight = hasCategory
            ? GetRenderedTextHeight(view.categoryText, view.categoryText.text, actualMiddleWidth, 16f)
            : 0f;
        float distanceHeight = hasDistance
            ? GetRenderedTextHeight(view.addressText, view.addressText.text, distanceColumnWidth, 18f)
            : 0f;

        float middleColumnHeight = titleHeight;
        if (hasCategory)
        {
            middleColumnHeight += innerGap + categoryHeight;
        }

        float contentHeight = Mathf.Max(iconSize, Mathf.Max(middleColumnHeight, distanceHeight));
        float bubbleHeight = Mathf.Max(bubbleMinHeight, (topPadding * 2f) + contentHeight);
        float verticalInset = (bubbleHeight - contentHeight) * 0.5f;

        view.bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
        if (view.bubbleShadowRect != null)
        {
            view.bubbleShadowRect.sizeDelta = new Vector2(
                bubbleWidth + screenMarkerShadowExpansion,
                bubbleHeight + screenMarkerShadowExpansion);
        }

        RectTransform iconRect = view.bubbleIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(leftPadding, -verticalInset - Mathf.Max(0f, (contentHeight - iconSize) * 0.5f));
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        float textStartX = leftPadding + iconSize + iconGap;
        float textStartY = verticalInset + Mathf.Max(0f, (contentHeight - middleColumnHeight) * 0.5f);

        RectTransform titleRect = view.titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(textStartX, -textStartY);
        titleRect.sizeDelta = new Vector2(actualMiddleWidth, titleHeight);

        RectTransform categoryRect = view.categoryText.rectTransform;
        categoryRect.anchorMin = new Vector2(0f, 1f);
        categoryRect.anchorMax = new Vector2(0f, 1f);
        categoryRect.pivot = new Vector2(0f, 1f);
        categoryRect.anchoredPosition = new Vector2(textStartX, -textStartY - titleHeight - (hasCategory ? innerGap : 0f));
        categoryRect.sizeDelta = new Vector2(actualMiddleWidth, categoryHeight);
        view.categoryText.gameObject.SetActive(hasCategory);

        RectTransform addressRect = view.addressText.rectTransform;
        addressRect.anchorMin = new Vector2(1f, 1f);
        addressRect.anchorMax = new Vector2(1f, 1f);
        addressRect.pivot = new Vector2(1f, 0.5f);
        addressRect.anchoredPosition = new Vector2(-rightPadding, -(bubbleHeight * 0.5f));
        addressRect.sizeDelta = new Vector2(distanceColumnWidth, distanceHeight);
        view.addressText.gameObject.SetActive(hasDistance);
    }

    void UpdateScreenMarkerAnimations()
    {
        if (_screenMarkerViews.Count == 0)
        {
            return;
        }

        float step = animDuration <= 0f ? 1f : Time.deltaTime / animDuration;
        foreach (ScreenMarkerView view in _screenMarkerViews.Values)
        {
            if (view == null || view.root == null || !view.root.activeSelf)
            {
                continue;
            }

            float target = view.isSelectedTarget ? 1f : 0f;
            view.selectionLerp = Mathf.MoveTowards(view.selectionLerp, target, step);
            float t = view.selectionLerp * view.selectionLerp * (3f - 2f * view.selectionLerp);

            if (view.pinRect != null)
            {
                view.pinRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.78f, 0.78f, 1f), t);
                view.pinRect.anchoredPosition = Vector2.Lerp(view.pinHiddenPosition, view.pinShownPosition, t);
            }

            if (view.pinImage != null)
            {
                Color c = view.pinImage.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                view.pinImage.color = c;
            }

            if (view.pinShadowImage != null)
            {
                Color c = view.pinShadowImage.color;
                c.a = Mathf.Lerp(0.2f, 0.05f, t);
                view.pinShadowImage.color = c;
            }

            if (view.pinShadowRect != null)
            {
                view.pinShadowRect.anchoredPosition = Vector2.Lerp(view.pinShadowHiddenPosition, view.pinShadowShownPosition, t);
                view.pinShadowRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.82f, 0.82f, 1f), t);
            }

            if (view.bubbleGroup != null)
            {
                view.bubbleGroup.alpha = t;
                view.bubbleGroup.blocksRaycasts = t > 0.95f;
                view.bubbleGroup.interactable = t > 0.95f;
            }

            if (view.bubbleRect != null)
            {
                view.bubbleRect.localScale = Vector3.Lerp(new Vector3(0.2f, 0.12f, 1f), Vector3.one, t);
                view.bubbleRect.anchoredPosition = Vector2.Lerp(view.bubbleHiddenPosition, view.bubbleShownPosition, t);
            }

            if (view.bubbleShadowRect != null)
            {
                view.bubbleShadowRect.localScale = Vector3.Lerp(new Vector3(0.2f, 0.12f, 1f), Vector3.one, t);
                view.bubbleShadowRect.anchoredPosition = Vector2.Lerp(
                    view.bubbleHiddenPosition + screenMarkerShadowOffset,
                    view.bubbleShownPosition + screenMarkerShadowOffset,
                    t);
            }
        }
    }

    Vector2 ClampToCanvas(Vector2 screenPosition)
    {
        Vector2 localPoint;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, uiCamera, out localPoint);

        const float paddingX = 180f;
        const float paddingY = 180f;
        float halfWidth = _canvasRect.rect.width * 0.5f;
        float halfHeight = _canvasRect.rect.height * 0.5f;

        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth + paddingX, halfWidth - paddingX);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight + 96f, halfHeight - paddingY);
        return localPoint;
    }

    void ConfigureQuickInfoCardLayout()
    {
        if (_quickRect == null)
        {
            return;
        }

        if (_quickRect.parent != transform)
        {
            _quickRect.SetParent(transform, false);
            _quickRect.SetAsLastSibling();
        }

        _quickRect.anchorMin = new Vector2(0f, 0f);
        _quickRect.anchorMax = new Vector2(1f, 0f);
        _quickRect.pivot = new Vector2(0.5f, 0f);

        Vector2 offsetMin = _quickRect.offsetMin;
        Vector2 offsetMax = _quickRect.offsetMax;
        offsetMin.x = 16f;
        offsetMax.x = -16f;
        _quickRect.offsetMin = offsetMin;
        _quickRect.offsetMax = offsetMax;

        if (quickInfoIcon != null)
        {
            RectTransform iconRect = quickInfoIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(55f, 18f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            quickInfoIcon.preserveAspect = true;
        }
    }

    float GetQuickCardTargetY()
    {
        return quickCardPosY > 0f ? quickCardPosY : 20f;
    }

    float GetQuickCardHiddenY()
    {
        return -(_quickRect.sizeDelta.y + 48f);
    }

    void InitializeQuickInfoCard(bool visible)
    {
        if (_quickRect == null || _quickGroup == null) return;

        float targetY = visible ? GetQuickCardTargetY() : GetQuickCardHiddenY();
        _quickRect.anchoredPosition = new Vector2(0f, targetY);
        _quickGroup.alpha = visible ? 1f : 0f;
        if (quickInfoCard != null)
        {
            quickInfoCard.SetActive(visible);
        }
    }

    void ShowQuickInfoCard(bool animateFromBottom)
    {
        if (quickInfoCard == null || _quickRect == null || _quickGroup == null) return;
        if (_quickRoutine != null) StopCoroutine(_quickRoutine);

        quickInfoCard.SetActive(true);
        if (animateFromBottom)
        {
            _quickRect.anchoredPosition = new Vector2(0f, GetQuickCardHiddenY());
            _quickGroup.alpha = 0f;
        }

        _quickRoutine = StartCoroutine(AnimateMove(_quickRect, _quickGroup, GetQuickCardTargetY(), 1f));
    }

    void HideQuickInfoCard()
    {
        if (quickInfoCard == null || _quickRect == null || _quickGroup == null) return;
        if (_quickRoutine != null) StopCoroutine(_quickRoutine);
        _quickRoutine = StartCoroutine(AnimateMove(_quickRect, _quickGroup, GetQuickCardHiddenY(), 0f, () => quickInfoCard.SetActive(false)));
    }

    string BuildQuickInfoId(BuildingData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        return $"{data.buildingName}|{data.latitude:F5}|{data.longitude:F5}";
    }

    #region Detail View Control
    public void OpenDetailView(BuildingData data)
    {
        if (uiToolkitDetailPanel == null)
        {
            Debug.LogWarning("UI Toolkit detail panel is not assigned.");
            return;
        }

        _currentDetailData = data;
        uiToolkitDetailPanel.Show(data);
        ClearScreenMarkers();
        ToggleScreenMarkerOverlay(false);
        OnDetailOpened?.Invoke();
    }

    public void CloseDetailView()
    {
        if (uiToolkitDetailPanel != null && uiToolkitDetailPanel.IsVisible)
        {
            uiToolkitDetailPanel.Hide();
        }
    }

    void HandleUIToolkitDetailClosed()
    {
        ToggleScreenMarkerOverlay(true);
        OnDetailClosed?.Invoke();
    }
    #endregion

    void ToggleScreenMarkerOverlay(bool visible)
    {
        if (_screenMarkerRoot == null) return;
        _screenMarkerRoot.gameObject.SetActive(visible);
    }

    #region UI Utilities (Toast & Buttons)
    // 토스트 메시지 표시
    public void ShowToast(string message)
    {
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastProcess(message));
    }

    IEnumerator ToastProcess(string message)
    {
        if (toastText != null) toastText.text = message;
        toastPanel.SetActive(true);
        CanvasGroup group = toastPanel.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        float time = 0f;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            group.alpha = Mathf.Lerp(0f, 1f, time / 0.2f);
            yield return null;
        }
        group.alpha = 1f;

        yield return new WaitForSeconds(toastDuration);

        time = 0f;
        while (time < 0.3f)
        {
            time += Time.deltaTime;
            group.alpha = Mathf.Lerp(1f, 0f, time / 0.3f);
            yield return null;
        }
        toastPanel.SetActive(false);
    }

    void OnCopyAddress()
    {
        if (_currentDetailData != null)
        {
            GUIUtility.systemCopyBuffer = _currentDetailData.fetchedAddress;
            ShowToast("주소가 복사되었습니다.");
        }
    }

    void OnShareDetail()
    {
        if (_currentDetailData == null)
        {
            return;
        }

        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(_currentDetailData.buildingName)) parts.Add(_currentDetailData.buildingName);
        if (!string.IsNullOrEmpty(_currentDetailData.fetchedAddress)) parts.Add(_currentDetailData.fetchedAddress);
        if (!string.IsNullOrEmpty(_currentDetailData.placeUrl)) parts.Add(_currentDetailData.placeUrl);

        if (parts.Count == 0)
        {
            return;
        }

        GUIUtility.systemCopyBuffer = string.Join("\n", parts);
        ShowToast("건물 정보가 복사되었습니다.");
    }

    void OnCallPhone() { if (_currentDetailData != null && !string.IsNullOrEmpty(_currentDetailData.phoneNumber)) Application.OpenURL("tel:" + _currentDetailData.phoneNumber); }
    void OnOpenMap() { if (_currentDetailData != null && !string.IsNullOrEmpty(_currentDetailData.placeUrl)) Application.OpenURL(_currentDetailData.placeUrl); }
    #endregion

    #region Animations
    void ShowCard(GameObject obj, RectTransform rect, CanvasGroup group, float targetY, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); obj.SetActive(true); routine = StartCoroutine(AnimateMove(rect, group, targetY, 1)); }
    void HideCard(GameObject obj, RectTransform rect, CanvasGroup group, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); routine = StartCoroutine(AnimateMove(rect, group, slideOffset, 0, () => obj.SetActive(false))); }
    void InitializeCard(RectTransform rect, CanvasGroup group, bool visible, float targetY) { if (visible) { rect.anchoredPosition = new Vector2(0, targetY); group.alpha = 1; } else { rect.anchoredPosition = new Vector2(0, slideOffset); group.alpha = 0; } }
    
    IEnumerator AnimateMove(RectTransform rect, CanvasGroup group, float targetY, float targetAlpha, Action onComplete = null) { float startY = rect.anchoredPosition.y; float startAlpha = group.alpha; float time = 0; while (time < animDuration) { time += Time.deltaTime; float t = time / animDuration; t = t * t * (3f - 2f * t); Vector2 pos = rect.anchoredPosition; pos.y = Mathf.Lerp(startY, targetY, t); rect.anchoredPosition = pos; group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t); yield return null; } rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY); group.alpha = targetAlpha; onComplete?.Invoke(); }
    #endregion
}
