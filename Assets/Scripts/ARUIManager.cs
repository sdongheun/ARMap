using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class ARUIManager : MonoBehaviour
{
    // --- Inspector Variables ---
    [Header("1. Main Cards")]
    public GameObject scanningCard;
    public GameObject detectedCard;

    [Header("2. Icons")]
    public Sprite iconScanning;
    public Sprite iconDetected;
    public Sprite iconBuilding;

    [Header("3. Detail View (Page 4)")]
    public ARDetailPanelDocumentController uiToolkitDetailPanel;

    [Header("4. Animation Settings")]
    public float animDuration = 0.3f; 
    public float slideOffset = 150f; 
    public float statusCardPosY = 0f;    

    [Header("5. Toast Message")]
    public GameObject toastPanel;
    public TextMeshProUGUI toastText;
    public float toastDuration = 2.0f;
    private Coroutine _toastRoutine;

    [Header("6. Navigation UI - 검색 패널")]
    public GameObject navigationSearchPanel;
    public TMP_InputField destinationInputField;
    public Button searchButton;
    public Transform searchResultContainer;
    public GameObject searchResultItemPrefab;
    public Button closeSearchButton;

    [Header("7. Navigation HUD")]
    public GameObject navigationHUD;
    public TextMeshProUGUI remainingDistanceText;
    public TextMeshProUGUI nextGuideText;
    public Button stopNavigationButton;
    public Button recalibrateButton;
    public RectTransform offScreenIndicator;

    [Header("8. Navigation Buttons")]
    public Button mainNavigateButton;
    public Button detailNavigateButton;
    public Button worldInfoDetailButton;
    [Header("9. Center Reticle")]
    public bool showCenterReticle = true;
    public Color centerReticleColor = new Color(1f, 1f, 1f, 0.72f);
    public float centerReticleBarLength = 16f;
    public float centerReticleBarThickness = 3f;
    public float centerReticleGap = 10f;
    public float centerReticlePulseDuration = 2.2f;
    public float centerReticlePulseAlphaMin = 0.32f;
    public float centerReticlePulseAlphaMax = 0.72f;
    public float centerReticlePulseScale = 1.04f;

    // --- Internal Variables ---
    public event Action OnDetailOpened;
    public event Action OnDetailClosed;
    public event Action OnNavigateRequested;
    public event Action<BuildingData> OnNavigateFromDetailRequested;
    public event Action OnStopNavigationRequested;
    public event Action OnRecalibrateRequested;
    private bool _stopButtonBound;
    private bool _recalibrateButtonBound;

    void BindStopNavigationButton()
    {
        if (_stopButtonBound || stopNavigationButton == null) return;
        stopNavigationButton.onClick.AddListener(() => OnStopNavigationRequested?.Invoke());
        _stopButtonBound = true;
    }

    void BindRecalibrateButton()
    {
        if (_recalibrateButtonBound || recalibrateButton == null) return;
        recalibrateButton.onClick.AddListener(() => OnRecalibrateRequested?.Invoke());
        _recalibrateButtonBound = true;
    }
    private enum UIState { None, Scanning, Detected, QuickInfo }
    private UIState currentState = UIState.None;
    private BuildingData _currentDetailData;

    private RectTransform _scanRect, _detectRect;
    private CanvasGroup _scanGroup, _detectGroup;
    private Coroutine _scanRoutine, _detectRoutine;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private RectTransform _centerReticleRoot;
    private RectTransform _debugOverlayRoot;
    private ScrollRect _debugOverlayScrollRect;
    private RectTransform _debugOverlayContent;
    private TextMeshProUGUI _debugOverlayText;
    private Image _worldInfoDetailButtonImage;
    private TextMeshProUGUI _worldInfoDetailButtonText;
    private BuildingData _worldInfoButtonData;
    private TMP_FontAsset SharedTMPFont => toastText != null && toastText.font != null
        ? toastText.font
        : TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
    private Material SharedTMPMaterial => toastText != null ? toastText.fontSharedMaterial : null;

    void Awake()
    {
        _scanRect = scanningCard.GetComponent<RectTransform>(); _scanGroup = scanningCard.GetComponent<CanvasGroup>();
        _detectRect = detectedCard.GetComponent<RectTransform>(); _detectGroup = detectedCard.GetComponent<CanvasGroup>();
        _canvasRect = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
    }

    void Start()
    {
        if (uiToolkitDetailPanel != null)
        {
            uiToolkitDetailPanel.OnClosed += HandleUIToolkitDetailClosed;
            uiToolkitDetailPanel.OnPhoneRequested += OnCallPhone;
            uiToolkitDetailPanel.OnCopyRequested += OnCopyAddress;
            uiToolkitDetailPanel.OnMapRequested += OnOpenMap;
        }

        if (mainNavigateButton != null)
            mainNavigateButton.onClick.AddListener(() => OnNavigateRequested?.Invoke());
        if (detailNavigateButton != null)
            detailNavigateButton.onClick.AddListener(() =>
            {
                if (_currentDetailData != null)
                {
                    OnNavigateFromDetailRequested?.Invoke(_currentDetailData);
                }
            });
        if (closeSearchButton != null)
            closeSearchButton.onClick.AddListener(HideSearchPanel);
        BindStopNavigationButton();
        BindRecalibrateButton();

        InitializeCard(_scanRect, _scanGroup, true, statusCardPosY);
        InitializeCard(_detectRect, _detectGroup, false, statusCardPosY);

        if (navigationSearchPanel != null) navigationSearchPanel.SetActive(false);
        if (navigationHUD != null) navigationHUD.SetActive(false);
        if (offScreenIndicator != null) offScreenIndicator.gameObject.SetActive(false);

        EnsureNavigationButton();
        EnsureWorldInfoDetailButton();
        EnsureCenterReticle();
    }

    void Update()
    {
        UpdateCenterReticleAnimation();
    }

    #region Navigation UI
    void EnsureSearchPanel()
    {
        if (navigationSearchPanel != null) return;

        // 검색 패널 루트
        navigationSearchPanel = new GameObject("NavigationSearchPanel", typeof(RectTransform), typeof(Image));
        navigationSearchPanel.transform.SetParent(transform, false);

        RectTransform panelRect = navigationSearchPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = navigationSearchPanel.GetComponent<Image>();
        panelBg.color = new Color(0.02f, 0.05f, 0.1f, 0.96f);

        // 상단 바 (입력 + 검색 + 닫기)
        GameObject topBar = new GameObject("TopBar", typeof(RectTransform));
        topBar.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = new Vector2(0f, -60f);
        topRect.sizeDelta = new Vector2(-48f, 56f);

        // 입력 필드
        GameObject inputObj = new GameObject("DestinationInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(topBar.transform, false);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.offsetMin = new Vector2(0f, 0f);
        inputRect.offsetMax = new Vector2(-140f, 0f);

        Image inputBg = inputObj.GetComponent<Image>();
        inputBg.color = new Color(0.15f, 0.18f, 0.22f, 1f);

        // 입력 필드 텍스트 영역
        GameObject inputTextArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        inputTextArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = inputTextArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(12f, 4f);
        textAreaRect.offsetMax = new Vector2(-12f, -4f);

        GameObject placeholderObj = CreateInputText("Placeholder", inputTextArea.transform, "목적지를 입력하세요", 0.5f);
        GameObject inputTextObj = CreateInputText("Text", inputTextArea.transform, "", 1f);

        TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputTextObj.GetComponent<TextMeshProUGUI>();
        inputField.placeholder = placeholderObj.GetComponent<TextMeshProUGUI>();
        inputField.fontAsset = SharedTMPFont;
        destinationInputField = inputField;

        // 검색 버튼
        searchButton = CreatePanelButton("SearchBtn", topBar.transform, "검색",
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-70f, 0f), new Vector2(68f, 0f),
            new Color(0.08f, 0.78f, 0.96f, 1f));

        // 닫기 버튼
        closeSearchButton = CreatePanelButton("CloseBtn", topBar.transform, "✕",
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(56f, 0f),
            new Color(0.4f, 0.4f, 0.45f, 1f));
        closeSearchButton.onClick.AddListener(HideSearchPanel);

        // ScrollRect (루트)
        GameObject scrollObj = new GameObject("SearchScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollObj.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform scrollRootRect = scrollObj.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0f, 0f);
        scrollRootRect.anchorMax = new Vector2(1f, 1f);
        scrollRootRect.offsetMin = new Vector2(24f, 24f);
        scrollRootRect.offsetMax = new Vector2(-24f, -130f);

        Image scrollBg = scrollObj.GetComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.001f); // 거의 투명(레이캐스트용)

        // Viewport
        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportObj.transform.SetParent(scrollObj.transform, false);
        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImg = viewportObj.GetComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.001f);

        // Content
        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(0f, 0f);
        contentRect.offsetMax = new Vector2(0f, 0f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 8);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;
        layout.childControlWidth = true;

        ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect 구성
        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;

        searchResultContainer = contentObj.transform;

        // 상태 인디케이터 (로딩 / 빈결과) - Scroll 위에 오버레이
        _searchStatusText = new GameObject("SearchStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        _searchStatusText.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform statusRect = _searchStatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0f, 0f);
        statusRect.sizeDelta = new Vector2(400f, 60f);
        TextMeshProUGUI statusTmp = _searchStatusText.GetComponent<TextMeshProUGUI>();
        statusTmp.text = "";
        statusTmp.fontSize = 22f;
        statusTmp.alignment = TextAlignmentOptions.Center;
        statusTmp.color = new Color(0.7f, 0.78f, 0.86f, 1f);
        ApplySharedTextStyle(statusTmp);
        _searchStatusText.SetActive(false);

        // 엔터키 서브밋
        if (destinationInputField != null && !_inputSubmitBound)
        {
            destinationInputField.onSubmit.AddListener(val =>
            {
                if (searchButton != null) searchButton.onClick.Invoke();
            });
            _inputSubmitBound = true;
        }

        navigationSearchPanel.SetActive(false);
    }

    GameObject _searchStatusText;
    private bool _inputSubmitBound;
    private TextMeshProUGUI _turnIconText;
    private TextMeshProUGUI _etaText;
    private Image _progressFill;
    private GameObject _trackingWarningBadge;
    private GameObject _reroutingIndicator;
    private Coroutine _reroutingAnimRoutine;

    public void ShowSearchLoading()
    {
        EnsureSearchPanel();
        ClearSearchResults();
        if (_searchStatusText != null)
        {
            _searchStatusText.SetActive(true);
            TextMeshProUGUI tmp = _searchStatusText.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "검색 중...";
        }
    }

    public void ShowEmptyResults(string message)
    {
        EnsureSearchPanel();
        ClearSearchResults();
        if (_searchStatusText != null)
        {
            _searchStatusText.SetActive(true);
            TextMeshProUGUI tmp = _searchStatusText.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = message;
        }
    }

    void HideSearchStatus()
    {
        if (_searchStatusText != null) _searchStatusText.SetActive(false);
    }

    GameObject CreateInputText(string name, Transform parent, string text, float alpha)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.color = new Color(1f, 1f, 1f, alpha);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplySharedTextStyle(tmp);
        return obj;
    }

    Button CreatePanelButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.pivot = pivot;
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = sizeDelta;

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplySharedTextStyle(tmp);

        return btnObj.GetComponent<Button>();
    }

    public void ShowSearchPanel()
    {
        EnsureSearchPanel();
        navigationSearchPanel.SetActive(true);
        if (destinationInputField != null)
        {
            destinationInputField.text = "";
            destinationInputField.ActivateInputField();
        }
        ClearSearchResults();
        HideSearchStatus();
    }

    public void HideSearchPanel()
    {
        if (navigationSearchPanel != null)
            navigationSearchPanel.SetActive(false);
    }

    public void UpdateSearchResults(List<DestinationResult> results, Action<DestinationResult> onSelect)
    {
        ClearSearchResults();
        EnsureSearchPanel();
        if (searchResultContainer == null) return;

        if (results == null || results.Count == 0)
        {
            ShowEmptyResults("검색 결과가 없습니다");
            return;
        }

        HideSearchStatus();

        foreach (DestinationResult result in results)
        {
            GameObject item = CreateSearchResultItem(result);
            item.transform.SetParent(searchResultContainer, false);

            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                DestinationResult captured = result;
                btn.onClick.AddListener(() => onSelect?.Invoke(captured));
            }
        }
    }

    GameObject CreateSearchResultItem(DestinationResult result)
    {
        string distanceStr = FormatDistance(result.distance);

        if (searchResultItemPrefab != null)
        {
            GameObject item = Instantiate(searchResultItemPrefab);
            TextMeshProUGUI nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI addressText = item.transform.Find("AddressText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI categoryText = item.transform.Find("CategoryText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI distanceText = item.transform.Find("DistanceText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null) nameText.text = result.placeName;
            if (addressText != null) addressText.text = result.roadAddressName ?? result.addressName;
            if (categoryText != null) categoryText.text = result.categoryName ?? "";
            if (distanceText != null) distanceText.text = distanceStr;
            return item;
        }

        GameObject itemObj = new GameObject("SearchResult", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform itemRect = itemObj.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0f, 88f);

        Image itemBg = itemObj.GetComponent<Image>();
        itemBg.color = new Color(0.12f, 0.15f, 0.2f, 1f);

        // 터치 피드백
        Button itemBtn = itemObj.GetComponent<Button>();
        ColorBlock cb = itemBtn.colors;
        cb.normalColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        cb.highlightedColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        cb.pressedColor = new Color(0.08f, 0.1f, 0.14f, 1f);
        cb.selectedColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        itemBtn.colors = cb;

        TextMeshProUGUI nameTmp = CreateResultLabel("NameText", itemObj.transform, result.placeName,
            24f, FontStyles.Bold, new Vector2(16f, -10f), new Vector2(-84f, -10f), 30f);
        TextMeshProUGUI addrTmp = CreateResultLabel("AddressText", itemObj.transform,
            result.roadAddressName ?? result.addressName,
            18f, FontStyles.Normal, new Vector2(16f, -42f), new Vector2(-84f, -42f), 24f);
        addrTmp.color = new Color(0.7f, 0.75f, 0.82f, 1f);

        if (!string.IsNullOrEmpty(result.categoryName))
        {
            TextMeshProUGUI catTmp = CreateResultLabel("CategoryText", itemObj.transform, result.categoryName,
                16f, FontStyles.Normal, new Vector2(16f, -66f), new Vector2(-84f, -66f), 20f);
            catTmp.color = new Color(0.08f, 0.78f, 0.96f, 0.9f);
        }

        // 거리 라벨 (우측 상단)
        if (!string.IsNullOrEmpty(distanceStr))
        {
            GameObject distObj = new GameObject("DistanceText", typeof(RectTransform), typeof(TextMeshProUGUI));
            distObj.transform.SetParent(itemObj.transform, false);
            RectTransform distRect = distObj.GetComponent<RectTransform>();
            distRect.anchorMin = new Vector2(1f, 1f);
            distRect.anchorMax = new Vector2(1f, 1f);
            distRect.pivot = new Vector2(1f, 1f);
            distRect.anchoredPosition = new Vector2(-16f, -14f);
            distRect.sizeDelta = new Vector2(80f, 28f);

            TextMeshProUGUI distTmp = distObj.GetComponent<TextMeshProUGUI>();
            distTmp.text = distanceStr;
            distTmp.fontSize = 20f;
            distTmp.fontStyle = FontStyles.Bold;
            distTmp.color = new Color(0.08f, 0.78f, 0.96f, 1f);
            distTmp.alignment = TextAlignmentOptions.MidlineRight;
            ApplySharedTextStyle(distTmp);
        }

        return itemObj;
    }

    string FormatDistance(int meters)
    {
        if (meters <= 0) return "";
        if (meters >= 1000) return $"{meters / 1000f:F1}km";
        return $"{meters}m";
    }

    TextMeshProUGUI CreateResultLabel(string name, Transform parent, string text,
        float fontSize, FontStyles style, Vector2 offsetMin, Vector2 offsetMax, float height)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(offsetMin.x, offsetMin.y);
        rect.sizeDelta = new Vector2(offsetMax.x - offsetMin.x, height);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        ApplySharedTextStyle(tmp);
        return tmp;
    }

    void ClearSearchResults()
    {
        if (searchResultContainer == null) return;
        foreach (Transform child in searchResultContainer)
        {
            Destroy(child.gameObject);
        }
    }

    void EnsureNavigationHUD()
    {
        if (navigationHUD != null) return;

        // HUD 루트 패널 (화면 상단)
        navigationHUD = new GameObject("NavigationHUD", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        navigationHUD.transform.SetParent(transform, false);

        RectTransform hudRect = navigationHUD.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(1f, 1f);
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.anchoredPosition = new Vector2(0f, 0f);
        hudRect.sizeDelta = new Vector2(0f, 220f);

        Image hudBg = navigationHUD.GetComponent<Image>();
        hudBg.color = new Color(0.02f, 0.06f, 0.12f, 0.92f);

        // 턴 아이콘 (좌측 원형)
        GameObject iconObj = new GameObject("TurnIcon", typeof(RectTransform), typeof(TextMeshProUGUI));
        iconObj.transform.SetParent(navigationHUD.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(20f, -20f);
        iconRect.sizeDelta = new Vector2(72f, 72f);
        _turnIconText = iconObj.GetComponent<TextMeshProUGUI>();
        _turnIconText.text = "↑";
        _turnIconText.fontSize = 56f;
        _turnIconText.fontStyle = FontStyles.Bold;
        _turnIconText.alignment = TextAlignmentOptions.Center;
        _turnIconText.color = new Color(0.08f, 0.78f, 0.96f, 1f);
        ApplySharedTextStyle(_turnIconText);

        // 남은 거리 텍스트 (턴 아이콘 오른쪽)
        remainingDistanceText = CreateHUDText("RemainingDistance", navigationHUD.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(46f, -34f), new Vector2(-24f, 44f),
            "경로 계산 중...", 30f, FontStyles.Bold, TextAlignmentOptions.Center);

        // ETA 텍스트
        _etaText = CreateHUDText("ETA", navigationHUD.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(46f, -76f), new Vector2(-24f, 28f),
            "", 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        _etaText.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);

        // 다음 안내 텍스트
        nextGuideText = CreateHUDText("NextGuide", navigationHUD.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -110f), new Vector2(-24f, 32f),
            "", 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        nextGuideText.color = new Color(0.7f, 0.85f, 1f, 0.9f);

        // 진행률 바 (배경)
        GameObject progressBg = new GameObject("ProgressBg", typeof(RectTransform), typeof(Image));
        progressBg.transform.SetParent(navigationHUD.transform, false);
        RectTransform pbgRect = progressBg.GetComponent<RectTransform>();
        pbgRect.anchorMin = new Vector2(0f, 1f);
        pbgRect.anchorMax = new Vector2(1f, 1f);
        pbgRect.pivot = new Vector2(0.5f, 1f);
        pbgRect.anchoredPosition = new Vector2(0f, -150f);
        pbgRect.sizeDelta = new Vector2(-40f, 6f);
        Image pbgImg = progressBg.GetComponent<Image>();
        pbgImg.color = new Color(0.2f, 0.24f, 0.3f, 1f);

        // 진행률 바 (채움)
        GameObject progressFillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        progressFillObj.transform.SetParent(progressBg.transform, false);
        RectTransform pfRect = progressFillObj.GetComponent<RectTransform>();
        pfRect.anchorMin = new Vector2(0f, 0f);
        pfRect.anchorMax = new Vector2(1f, 1f);
        pfRect.offsetMin = Vector2.zero;
        pfRect.offsetMax = Vector2.zero;
        _progressFill = progressFillObj.GetComponent<Image>();
        _progressFill.color = new Color(0.08f, 0.78f, 0.96f, 1f);
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = 0;
        _progressFill.fillAmount = 0f;

        // 중지 버튼 (하단 오른쪽)
        stopNavigationButton = CreatePanelButton("StopNavBtn", navigationHUD.transform, "안내 중지",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(80f, 16f), new Vector2(140f, 44f),
            new Color(0.85f, 0.2f, 0.15f, 1f));
        BindStopNavigationButton();

        // 화면 보정 버튼 (하단 왼쪽 - drift 발생 시 현재 카메라 위치 기준으로 화살표 재정렬)
        recalibrateButton = CreatePanelButton("RecalibrateBtn", navigationHUD.transform, "화면 보정",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-80f, 16f), new Vector2(140f, 44f),
            new Color(0.08f, 0.78f, 0.96f, 1f));
        BindRecalibrateButton();

        // 트래킹 경고 배지 (GPS 신호 약함)
        _trackingWarningBadge = new GameObject("TrackingWarning", typeof(RectTransform), typeof(Image));
        _trackingWarningBadge.transform.SetParent(navigationHUD.transform, false);
        RectTransform twRect = _trackingWarningBadge.GetComponent<RectTransform>();
        twRect.anchorMin = new Vector2(1f, 1f);
        twRect.anchorMax = new Vector2(1f, 1f);
        twRect.pivot = new Vector2(1f, 1f);
        twRect.anchoredPosition = new Vector2(-16f, -16f);
        twRect.sizeDelta = new Vector2(16f, 16f);
        Image twImg = _trackingWarningBadge.GetComponent<Image>();
        twImg.color = new Color(0.95f, 0.25f, 0.15f, 1f);
        _trackingWarningBadge.SetActive(false);

        // 리루트 인디케이터
        _reroutingIndicator = new GameObject("ReroutingIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        _reroutingIndicator.transform.SetParent(navigationHUD.transform, false);
        RectTransform riRect = _reroutingIndicator.GetComponent<RectTransform>();
        riRect.anchorMin = new Vector2(0.5f, 0f);
        riRect.anchorMax = new Vector2(0.5f, 0f);
        riRect.pivot = new Vector2(0.5f, 0f);
        riRect.anchoredPosition = new Vector2(0f, 68f);
        riRect.sizeDelta = new Vector2(260f, 28f);
        TextMeshProUGUI riTmp = _reroutingIndicator.GetComponent<TextMeshProUGUI>();
        riTmp.text = "경로 재탐색 중...";
        riTmp.fontSize = 18f;
        riTmp.fontStyle = FontStyles.Bold;
        riTmp.alignment = TextAlignmentOptions.Center;
        riTmp.color = new Color(1f, 0.8f, 0.2f, 1f);
        ApplySharedTextStyle(riTmp);
        _reroutingIndicator.SetActive(false);

        // 화면 밖 방향 표시 인디케이터
        if (offScreenIndicator == null)
        {
            GameObject indicatorObj = new GameObject("OffScreenIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            indicatorObj.transform.SetParent(transform, false);
            offScreenIndicator = indicatorObj.GetComponent<RectTransform>();
            offScreenIndicator.sizeDelta = new Vector2(60f, 60f);

            TextMeshProUGUI indicatorText = indicatorObj.GetComponent<TextMeshProUGUI>();
            indicatorText.text = "V";
            indicatorText.fontSize = 40f;
            indicatorText.fontStyle = FontStyles.Bold;
            indicatorText.alignment = TextAlignmentOptions.Center;
            indicatorText.color = new Color(0.08f, 0.78f, 0.96f, 0.9f);
            ApplySharedTextStyle(indicatorText);
            indicatorObj.SetActive(false);
        }

        navigationHUD.SetActive(false);
    }

    TextMeshProUGUI CreateHUDText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        ApplySharedTextStyle(tmp);
        return tmp;
    }

    public void ShowNavigationHUD()
    {
        EnsureNavigationHUD();
        navigationHUD.SetActive(true);
    }

    public void HideNavigationHUD()
    {
        if (navigationHUD != null) navigationHUD.SetActive(false);
        if (offScreenIndicator != null) offScreenIndicator.gameObject.SetActive(false);
        if (_trackingWarningBadge != null) _trackingWarningBadge.SetActive(false);
        if (_reroutingIndicator != null) _reroutingIndicator.SetActive(false);
        if (_reroutingAnimRoutine != null)
        {
            StopCoroutine(_reroutingAnimRoutine);
            _reroutingAnimRoutine = null;
        }
    }

    public void UpdateRemainingDistance(float meters)
    {
        EnsureNavigationHUD();
        if (remainingDistanceText == null) return;
        if (meters >= 1000f)
            remainingDistanceText.text = $"약 {meters / 1000f:F1}km";
        else
            remainingDistanceText.text = $"약 {Mathf.RoundToInt(meters)}m";
    }

    public void UpdateNextGuide(string guidance, float distance)
    {
        UpdateNextGuide(guidance, distance, 0);
    }

    public void UpdateNextGuide(string guidance, float distance, int guideType)
    {
        EnsureNavigationHUD();
        if (nextGuideText != null)
        {
            if (string.IsNullOrWhiteSpace(guidance))
            {
                nextGuideText.text = "";
            }
            else if (distance > 0f)
            {
                nextGuideText.text = $"{Mathf.RoundToInt(distance)}m 후 {guidance}";
            }
            else
            {
                nextGuideText.text = guidance;
            }
        }

        if (_turnIconText != null)
        {
            _turnIconText.text = GetTurnSymbol(guideType);
        }
    }

    // TMAP 보행자 길찾기 turnType → 심볼 매핑
    string GetTurnSymbol(int type)
    {
        switch (type)
        {
            case 11: return "↑";  // 직진
            case 12: return "↖";  // 좌회전
            case 13: return "↗";  // 우회전
            case 14: return "↺";  // U턴
            case 16: return "↰";  // 8시 좌회전
            case 17: return "↖";  // 10시 좌회전
            case 18: return "↗";  // 2시 우회전
            case 19: return "↳";  // 4시 우회전
            case 125: return "⌒"; // 육교
            case 126: return "▼"; // 지하보도
            case 127: return "⊓"; // 계단
            case 128: return "⁄"; // 경사로
            case 200: return "↑"; // 출발
            case 201: return "●"; // 도착
            case 211: // 횡단보도
            case 212:
            case 213:
            case 214:
            case 215:
            case 216:
            case 217: return "⊞"; // 횡단보도
            case 218: return "⊡"; // 엘리베이터
            default: return "↑";
        }
    }

    public void UpdateETA(int remainingSeconds)
    {
        EnsureNavigationHUD();
        if (_etaText == null) return;
        if (remainingSeconds <= 0)
        {
            _etaText.text = "";
            return;
        }
        int minutes = Mathf.RoundToInt(remainingSeconds / 60f);
        if (minutes < 1)
        {
            _etaText.text = "곧 도착";
        }
        else
        {
            DateTime arrival = DateTime.Now.AddSeconds(remainingSeconds);
            _etaText.text = $"약 {minutes}분 · {arrival:HH:mm} 도착";
        }
    }

    public void UpdateProgress(float normalized)
    {
        EnsureNavigationHUD();
        if (_progressFill == null) return;
        _progressFill.fillAmount = Mathf.Clamp01(normalized);
    }

    public void SetTrackingWarning(bool show)
    {
        EnsureNavigationHUD();
        if (_trackingWarningBadge != null)
            _trackingWarningBadge.SetActive(show);
    }

    public void ShowRerouting(bool show)
    {
        EnsureNavigationHUD();
        if (_reroutingIndicator == null) return;
        _reroutingIndicator.SetActive(show);
        if (_reroutingAnimRoutine != null)
        {
            StopCoroutine(_reroutingAnimRoutine);
            _reroutingAnimRoutine = null;
        }
        if (show)
        {
            _reroutingAnimRoutine = StartCoroutine(AnimateReroutingDots());
        }
    }

    IEnumerator AnimateReroutingDots()
    {
        TextMeshProUGUI tmp = _reroutingIndicator != null ? _reroutingIndicator.GetComponent<TextMeshProUGUI>() : null;
        if (tmp == null) yield break;
        int dotCount = 0;
        while (_reroutingIndicator != null && _reroutingIndicator.activeSelf)
        {
            string dots = new string('.', (dotCount % 3) + 1);
            tmp.text = $"경로 재탐색 중{dots}";
            dotCount++;
            yield return new WaitForSeconds(0.4f);
        }
    }

    public void UpdateOffScreenIndicator(Vector3 targetWorldPos, Camera cam)
    {
        if (offScreenIndicator == null || cam == null) return;

        Vector3 screenPoint = cam.WorldToScreenPoint(targetWorldPos);

        bool isOnScreen = screenPoint.z > 0f
            && screenPoint.x > 0f && screenPoint.x < Screen.width
            && screenPoint.y > 0f && screenPoint.y < Screen.height;

        if (isOnScreen)
        {
            offScreenIndicator.gameObject.SetActive(false);
            return;
        }

        offScreenIndicator.gameObject.SetActive(true);

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir;

        if (screenPoint.z < 0f)
            dir = new Vector2(-screenPoint.x + screenCenter.x, -screenPoint.y + screenCenter.y);
        else
            dir = new Vector2(screenPoint.x - screenCenter.x, screenPoint.y - screenCenter.y);

        dir.Normalize();

        const float edgePadding = 80f;
        float halfW = screenCenter.x - edgePadding;
        float halfH = screenCenter.y - edgePadding;

        float scale = Mathf.Min(
            Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
            Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue
        );

        Vector2 edgePos = screenCenter + dir * scale;

        Vector2 localPoint;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, edgePos, uiCamera, out localPoint);
        offScreenIndicator.anchoredPosition = localPoint;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        offScreenIndicator.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    void EnsureNavigationButton()
    {
        EnsureNavigationManager();

        if (mainNavigateButton != null)
        {
            mainNavigateButton.gameObject.SetActive(true);
            return;
        }

        GameObject btnObj = new GameObject("MainNavigateButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-24f, 200f);
        btnRect.sizeDelta = new Vector2(64f, 64f);

        Image btnImage = btnObj.GetComponent<Image>();
        btnImage.color = new Color(0.08f, 0.78f, 0.96f, 1f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObj.GetComponent<TextMeshProUGUI>();
        btnText.text = "길찾기";
        btnText.fontSize = 18f;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        ApplySharedTextStyle(btnText);

        mainNavigateButton = btnObj.GetComponent<Button>();
        mainNavigateButton.onClick.AddListener(() => OnNavigateRequested?.Invoke());
    }

    void EnsureWorldInfoDetailButton()
    {
        if (worldInfoDetailButton != null)
        {
            worldInfoDetailButton.gameObject.SetActive(true);
            return;
        }

        GameObject btnObj = new GameObject("WorldInfoDetailButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(0f, 0f);
        btnRect.pivot = new Vector2(0f, 0f);
        btnRect.anchoredPosition = new Vector2(24f, 200f);
        btnRect.sizeDelta = new Vector2(128f, 64f);

        _worldInfoDetailButtonImage = btnObj.GetComponent<Image>();
        _worldInfoDetailButtonImage.color = new Color(0.28f, 0.32f, 0.38f, 0.9f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _worldInfoDetailButtonText = textObj.GetComponent<TextMeshProUGUI>();
        _worldInfoDetailButtonText.text = "상세정보";
        _worldInfoDetailButtonText.fontSize = 18f;
        _worldInfoDetailButtonText.alignment = TextAlignmentOptions.Center;
        _worldInfoDetailButtonText.color = new Color(0.82f, 0.86f, 0.9f, 0.9f);
        _worldInfoDetailButtonText.raycastTarget = false;
        ApplySharedTextStyle(_worldInfoDetailButtonText);

        worldInfoDetailButton = btnObj.GetComponent<Button>();
        worldInfoDetailButton.onClick.AddListener(() =>
        {
            if (_worldInfoButtonData != null)
            {
                OpenDetailView(_worldInfoButtonData);
            }
        });

        SetWorldInfoDetailButtonState(null, false);
    }

    void EnsureNavigationManager()
    {
        if (FindFirstObjectByType<NavigationManager>() != null) return;

        GameObject navObj = new GameObject("NavigationManager");
        navObj.AddComponent<NavigationManager>();
        Debug.Log("[ARUIManager] NavigationManager를 자동 생성했습니다.");
    }

    public void EnterNavigationMode()
    {
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(false);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(false);
    }

    public void ExitNavigationMode()
    {
        HideNavigationHUD();
        HideSearchPanel();
        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(true);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(true);
        SetScanningMode();
    }
    #endregion

    #region Animations
    void ShowCard(GameObject obj, RectTransform rect, CanvasGroup group, float targetY, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); obj.SetActive(true); routine = StartCoroutine(AnimateMove(rect, group, targetY, 1)); }
    void HideCard(GameObject obj, RectTransform rect, CanvasGroup group, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); routine = StartCoroutine(AnimateMove(rect, group, slideOffset, 0, () => obj.SetActive(false))); }
    void InitializeCard(RectTransform rect, CanvasGroup group, bool visible, float targetY) { if (visible) { rect.anchoredPosition = new Vector2(0, targetY); group.alpha = 1; } else { rect.anchoredPosition = new Vector2(0, slideOffset); group.alpha = 0; } }
    
    IEnumerator AnimateMove(RectTransform rect, CanvasGroup group, float targetY, float targetAlpha, Action onComplete = null) { float startY = rect.anchoredPosition.y; float startAlpha = group.alpha; float time = 0; while (time < animDuration) { time += Time.deltaTime; float t = time / animDuration; t = t * t * (3f - 2f * t); Vector2 pos = rect.anchoredPosition; pos.y = Mathf.Lerp(startY, targetY, t); rect.anchoredPosition = pos; group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t); yield return null; } rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY); group.alpha = targetAlpha; onComplete?.Invoke(); }
    #endregion
}
