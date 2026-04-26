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
    // public GameObject toastPanel; // 현재 미사용
    public TextMeshProUGUI toastText;
    public float toastDuration = 2.0f;
    // private Coroutine _toastRoutine; // 현재 미사용

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
    public Button landscapeModeButton;
    public Button debugModeButton;
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
    [Header("10. Status Badge")]
    public Vector2 statusBadgeSize = new Vector2(260f, 60f);
    public Vector2 statusBadgeTopOffset = new Vector2(0f, -48f);
    [Header("11. Action Bar Style")]
    public int bottomActionBarCornerRadius = 14;
    public int bottomActionBarSpriteSize = 64;

    // --- Internal Variables ---
    public event Action OnDetailOpened;
    public event Action OnDetailClosed;
    public event Action OnNavigateRequested;
    public event Action<BuildingData> OnNavigateFromDetailRequested;
    public event Action OnStopNavigationRequested;
    public event Action OnRecalibrateRequested;
    public event Action<bool> OnLandscapeModeToggleRequested;
    public event Action<bool> OnDebugModeToggleRequested;
    private bool _stopButtonBound;
    private bool _recalibrateButtonBound;
    private bool _isLandscapeModeEnabled;
    private bool _isDebugModeEnabled;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    // 안내 중지 버튼을 한 번만 이벤트에 연결한다.
    void BindStopNavigationButton()
    {
        if (_stopButtonBound || stopNavigationButton == null) return;
        stopNavigationButton.onClick.AddListener(() => OnStopNavigationRequested?.Invoke());
        _stopButtonBound = true;
    }

    // 화면 보정 버튼을 한 번만 이벤트에 연결한다.
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
    private RectTransform _statusBadgeRoot;
    private Image _statusBadgeBackground;
    private TextMeshProUGUI _statusBadgeText;
    private CanvasGroup _statusBadgeGroup;
    private Coroutine _statusBadgeRoutine;
    private RectTransform _bottomActionBarRoot;
    private HorizontalLayoutGroup _bottomActionBarLayout;
    private Image _bottomActionBarBackground;
    private Sprite _bottomActionBarRoundedSprite;
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

    // 상태 카드와 캔버스 관련 참조를 미리 캐시한다.
    void Awake()
    {
        if (scanningCard != null)
        {
            _scanRect = scanningCard.GetComponent<RectTransform>();
            _scanGroup = scanningCard.GetComponent<CanvasGroup>();
        }
        if (detectedCard != null)
        {
            _detectRect = detectedCard.GetComponent<RectTransform>();
            _detectGroup = detectedCard.GetComponent<CanvasGroup>();
        }
        _canvasRect = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
    }

    // 시작 시 상세 패널, 버튼, 카드 초기 상태를 한 번에 연결한다.
    void Start()
    {
        EnsureNavigationManager();

        if (uiToolkitDetailPanel != null)
        {
            uiToolkitDetailPanel.OnClosed += HandleUIToolkitDetailClosed;
            uiToolkitDetailPanel.OnPhoneRequested += OnCallPhone;
            uiToolkitDetailPanel.OnMapRequested += OnOpenMap;
        }

        if (mainNavigateButton != null)
            mainNavigateButton.onClick.AddListener(DispatchNavigateRequested);
        if (detailNavigateButton != null)
            detailNavigateButton.onClick.AddListener(() =>
            {
                if (_currentDetailData != null)
                {
                    DispatchNavigateFromDetailRequested(_currentDetailData);
                }
            });
        if (closeSearchButton != null)
            closeSearchButton.onClick.AddListener(HideSearchPanel);
        BindStopNavigationButton();
        BindRecalibrateButton();

        InitializeCard(_scanRect, _scanGroup, false, statusCardPosY);
        InitializeCard(_detectRect, _detectGroup, false, statusCardPosY);
        EnsureStatusBadge();
        SetStatusBadgeMessage("주변을 바라보세요");

        if (navigationSearchPanel != null) navigationSearchPanel.SetActive(false);
        if (navigationHUD != null) navigationHUD.SetActive(false);
        if (offScreenIndicator != null) offScreenIndicator.gameObject.SetActive(false);

        showCenterReticle = false;
        EnsureDebugModeButton();
        EnsureBottomActionBarToolkit();
        RefreshFloatingButtonLayout(force: true);
        EnsureCenterReticle();
    }

    // 프레임마다 중앙 레티클 애니메이션만 갱신한다.
    void Update()
    {
        RefreshFloatingButtonLayout();
        RefreshStatusBadgeLayout();
        UpdateCenterReticleAnimation();
    }

    #region Navigation UI
    // 검색 패널 전체를 코드로 생성하고 입력/결과/상태 표시를 연결한다.
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

    // 검색 패널을 로딩 문구만 보이는 상태로 초기화한다.
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

    // 검색 결과가 없을 때 안내 문구만 보이는 상태로 초기화한다.
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

    // 검색 패널의 상태 문구를 숨긴다.
    void HideSearchStatus()
    {
        if (_searchStatusText != null) _searchStatusText.SetActive(false);
    }

    // 입력 필드용 TMP 텍스트 오브젝트를 생성한다.
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

    // 검색 패널과 HUD에서 재사용하는 공통 버튼 UI를 생성한다.
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

    // 검색 패널을 열고 입력/결과/상태를 초기화한다.
    public void ShowSearchPanel()
    {
        EnsureSearchPanel();
        navigationSearchPanel.SetActive(true);
        BringNavigationSurfaceToFront(navigationSearchPanel.transform);
        if (destinationInputField != null)
        {
            destinationInputField.text = "";
            destinationInputField.ActivateInputField();
        }
        ClearSearchResults();
        HideSearchStatus();
    }

    // 검색 패널을 비활성화한다.
    public void HideSearchPanel()
    {
        if (navigationSearchPanel != null)
            navigationSearchPanel.SetActive(false);
    }

    // 검색 결과 리스트를 새로 만들고 각 항목의 선택 콜백을 연결한다.
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

    // 검색 결과 한 항목의 시각 요소를 프리팹 또는 코드 생성으로 구성한다.
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

    // 검색 결과에서 쓸 거리 문자열을 m/km 형식으로 포맷한다.
    string FormatDistance(int meters)
    {
        if (meters <= 0) return "";
        if (meters >= 1000) return $"{meters / 1000f:F1}km";
        return $"{meters}m";
    }

    // 검색 결과 아이템에서 공통으로 쓰는 라벨을 생성한다.
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

    // 기존 검색 결과 아이템들을 모두 제거한다.
    void ClearSearchResults()
    {
        if (searchResultContainer == null) return;
        foreach (Transform child in searchResultContainer)
        {
            Destroy(child.gameObject);
        }
    }

    // 길찾기 HUD 전체를 코드로 생성하고 각 상태 위젯을 연결한다.
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

    // HUD에서 공통으로 쓰는 텍스트 라벨을 생성한다.
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

    // 내비 HUD를 표시한다.
    public void ShowNavigationHUD()
    {
        EnsureNavigationHUD();
        navigationHUD.SetActive(true);
        BringNavigationSurfaceToFront(navigationHUD.transform);
    }

    // 내비 HUD와 관련 인디케이터를 모두 숨기고 애니메이션을 정리한다.
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

    // 남은 총 거리를 HUD 상단 문구 형식으로 갱신한다.
    public void UpdateRemainingDistance(float meters)
    {
        EnsureNavigationHUD();
        if (remainingDistanceText == null) return;
        if (meters >= 1000f)
            remainingDistanceText.text = $"약 {meters / 1000f:F1}km";
        else
            remainingDistanceText.text = $"약 {Mathf.RoundToInt(meters)}m";
    }

    // 방향 타입 없이 다음 안내 문구만 갱신하는 편의 오버로드다.
    public void UpdateNextGuide(string guidance, float distance)
    {
        UpdateNextGuide(guidance, distance, 0);
    }

    // 다음 안내 문구와 턴 심볼을 함께 갱신한다.
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
    // TMAP turnType 값을 화면용 방향 심볼로 변환한다.
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

    // 남은 시간으로 ETA 또는 도착 예정 시각 문구를 계산해 표시한다.
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

    // 진행률 바 채움 비율을 0~1 범위로 갱신한다.
    public void UpdateProgress(float normalized)
    {
        EnsureNavigationHUD();
        if (_progressFill == null) return;
        _progressFill.fillAmount = Mathf.Clamp01(normalized);
    }

    // 트래킹 불안정 경고 배지를 표시하거나 숨긴다.
    public void SetTrackingWarning(bool show)
    {
        EnsureNavigationHUD();
        if (_trackingWarningBadge != null)
            _trackingWarningBadge.SetActive(show);
    }

    // 재탐색 인디케이터와 점 애니메이션의 표시 상태를 제어한다.
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

    // 재탐색 문구 뒤의 점 개수를 순환시켜 진행 중 느낌을 만든다.
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

    // 목적지가 화면 밖일 때 가장자리 방향 인디케이터의 위치와 회전을 계산한다.
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

    // 메인 길찾기 버튼이 없으면 생성하고 NavigationManager 존재도 보장한다.
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
        btnRect.anchoredPosition = new Vector2(-24f, 140f);
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
        mainNavigateButton.onClick.AddListener(DispatchNavigateRequested);
    }

    void DispatchNavigateRequested()
    {
        EnsureNavigationManager();
        OnNavigateRequested?.Invoke();
    }

    void DispatchNavigateFromDetailRequested(BuildingData building)
    {
        if (building == null)
        {
            return;
        }

        EnsureNavigationManager();
        OnNavigateFromDetailRequested?.Invoke(building);
    }

    // 가로모드 토글 버튼을 생성하거나 기존 버튼을 재사용한다.
    void EnsureLandscapeModeButton()
    {
        if (landscapeModeButton != null)
        {
            landscapeModeButton.gameObject.SetActive(true);
            SetLandscapeModeButtonState(_isLandscapeModeEnabled);
            return;
        }

        GameObject btnObj = new GameObject("LandscapeModeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = new Vector2(-24f, -24f);
        btnRect.sizeDelta = new Vector2(120f, 56f);

        Image btnImage = btnObj.GetComponent<Image>();
        btnImage.color = new Color(0.28f, 0.32f, 0.38f, 0.9f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObj.GetComponent<TextMeshProUGUI>();
        btnText.text = "가로모드";
        btnText.fontSize = 18f;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = new Color(0.82f, 0.86f, 0.9f, 0.9f);
        btnText.raycastTarget = false;
        ApplySharedTextStyle(btnText);

        landscapeModeButton = btnObj.GetComponent<Button>();
        landscapeModeButton.onClick.AddListener(() =>
        {
            _isLandscapeModeEnabled = !_isLandscapeModeEnabled;
            ApplyLandscapeScreenOrientation(_isLandscapeModeEnabled);
            SetLandscapeModeButtonState(_isLandscapeModeEnabled);
            OnLandscapeModeToggleRequested?.Invoke(_isLandscapeModeEnabled);
            RefreshFloatingButtonLayout(force: true);
        });

        SetLandscapeModeButtonState(_isLandscapeModeEnabled);
    }

    // 디버그 화면과 실제 화면을 즉시 전환하는 토글 버튼을 생성하거나 재사용한다.
    void EnsureDebugModeButton()
    {
        if (debugModeButton != null)
        {
            debugModeButton.gameObject.SetActive(true);
            SetDebugModeButtonState(_isDebugModeEnabled);
            return;
        }

        GameObject btnObj = new GameObject("DebugModeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = new Vector2(-24f, -92f);
        btnRect.sizeDelta = new Vector2(120f, 56f);

        Image btnImage = btnObj.GetComponent<Image>();
        btnImage.color = new Color(0.28f, 0.32f, 0.38f, 0.9f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObj.GetComponent<TextMeshProUGUI>();
        btnText.text = "디버그 OFF";
        btnText.fontSize = 18f;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = new Color(0.82f, 0.86f, 0.9f, 0.9f);
        btnText.raycastTarget = false;
        ApplySharedTextStyle(btnText);

        debugModeButton = btnObj.GetComponent<Button>();
        debugModeButton.onClick.AddListener(() =>
        {
            _isDebugModeEnabled = !_isDebugModeEnabled;
            SetDebugModeButtonState(_isDebugModeEnabled);
            OnDebugModeToggleRequested?.Invoke(_isDebugModeEnabled);
            RefreshFloatingButtonLayout(force: true);
        });

        SetDebugModeButtonState(_isDebugModeEnabled);
    }

    // 가로모드 버튼의 활성 여부를 외부에서 동기화할 수 있게 한다.
    public void SetLandscapeModeUIState(bool enabled)
    {
        _isLandscapeModeEnabled = enabled;
        ApplyLandscapeScreenOrientation(_isLandscapeModeEnabled);
        SetLandscapeModeButtonState(_isLandscapeModeEnabled);
        RefreshFloatingButtonLayout(force: true);
    }

    // 디버그 모드 토글 UI 상태를 외부에서 동기화한다.
    public void SetDebugModeUIState(bool enabled)
    {
        _isDebugModeEnabled = enabled;
        SetDebugModeButtonState(_isDebugModeEnabled);
        RefreshFloatingButtonLayout(force: true);
    }

    // 가로모드 토글 상태에 따라 화면 방향을 세로 또는 가로로 전환한다.
    void ApplyLandscapeScreenOrientation(bool enabled)
    {
        Screen.orientation = enabled
            ? ScreenOrientation.LandscapeLeft
            : ScreenOrientation.Portrait;
    }

    // 가로모드 버튼의 색상과 문구를 현재 토글 상태에 맞춰 갱신한다.
    void SetLandscapeModeButtonState(bool enabled)
    {
        UpdateToolkitLandscapeButtonState(enabled);

        if (landscapeModeButton == null)
        {
            return;
        }

        Image buttonImage = landscapeModeButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = enabled
                ? new Color(0.08f, 0.78f, 0.96f, 1f)
                : new Color(0.28f, 0.32f, 0.38f, 0.9f);
        }

        TextMeshProUGUI buttonText = landscapeModeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = enabled ? "세로 복귀" : "가로모드";
            buttonText.color = enabled
                ? Color.white
                : new Color(0.82f, 0.86f, 0.9f, 0.9f);
        }

        landscapeModeButton.transform.SetAsLastSibling();
    }

    // 디버그 버튼의 색상과 문구를 현재 토글 상태에 맞춰 갱신한다.
    void SetDebugModeButtonState(bool enabled)
    {
        if (debugModeButton == null)
        {
            return;
        }

        Image buttonImage = debugModeButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = enabled
                ? new Color(0.95f, 0.56f, 0.12f, 1f)
                : new Color(0.28f, 0.32f, 0.38f, 0.9f);
        }

        TextMeshProUGUI buttonText = debugModeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = enabled ? "디버그 ON" : "디버그 OFF";
            buttonText.color = enabled
                ? Color.white
                : new Color(0.82f, 0.86f, 0.9f, 0.9f);
        }

        debugModeButton.transform.SetAsLastSibling();
    }

    // 월드 선택 대상의 상세 진입 버튼을 생성하거나 기존 버튼을 재사용한다.
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
        btnRect.anchoredPosition = new Vector2(24f, 140f);
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

    // 화면 방향이 바뀌면 플로팅 버튼 배치를 다시 잡아 버튼이 가려지지 않게 한다.
    void RefreshFloatingButtonLayout(bool force = false)
    {
        if (!force && _lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
        {
            return;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        bool isLandscapeLike = Screen.width > Screen.height;
        EnsureBottomActionBarToolkit();
        RefreshBottomActionBarToolkitLayout();

        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(false);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(false);
        if (landscapeModeButton != null) landscapeModeButton.gameObject.SetActive(false);

        Vector2 debugSize = isLandscapeLike ? new Vector2(88f, 36f) : new Vector2(104f, 48f);
        float debugBottomInset = isLandscapeLike ? 68f : 116f;
        UpdateFloatingButtonRect(debugModeButton, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24f, debugBottomInset), debugSize);

        if (debugModeButton != null)
        {
            debugModeButton.transform.SetAsLastSibling();
        }
    }

    // 플로팅 버튼 위치를 공통 방식으로 갱신한다.
    void UpdateFloatingButtonRect(Button button, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    // 버튼 내부 TMP 텍스트 크기를 현재 레이아웃에 맞게 조정한다.
    void SetButtonTextSize(Button button, float fontSize)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText == null)
        {
            return;
        }

        buttonText.fontSize = fontSize;
    }

    // 레거시 uGUI 플로팅 바 생성 루틴은 더 이상 사용하지 않는다.
    void EnsureBottomActionBar()
    {
        return;
    }

    // 지정한 radius를 가진 rounded rectangle 9-slice 스프라이트를 런타임에 만든다.
    Sprite CreateRoundedRectSprite(int textureSize, int radius)
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "BottomActionBarRoundedSprite";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[textureSize * textureSize];
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 fill = new Color32(255, 255, 255, 255);
        Vector2 centerOffset = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
        float halfSize = textureSize * 0.5f;
        float innerHalf = Mathf.Max(0f, halfSize - radius);
        float radiusSq = radius * radius;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float localX = Mathf.Abs((x + 0.5f) - centerOffset.x);
                float localY = Mathf.Abs((y + 0.5f) - centerOffset.y);

                bool insideCore = localX <= innerHalf || localY <= innerHalf;
                bool insideCorner = false;

                if (!insideCore)
                {
                    float dx = localX - innerHalf;
                    float dy = localY - innerHalf;
                    insideCorner = (dx * dx) + (dy * dy) <= radiusSq;
                }

                pixels[y * textureSize + x] = (insideCore || insideCorner) ? fill : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // 씬에 NavigationManager가 없을 때만 자동 생성한다.
    void EnsureNavigationManager()
    {
        if (FindFirstObjectByType<NavigationManager>() != null) return;

        GameObject navObj = new GameObject("NavigationManager");
        navObj.AddComponent<NavigationManager>();
        Debug.Log("[ARUIManager] NavigationManager를 자동 생성했습니다.");
    }

    // 내비 모드 진입 시 일반 상태 카드와 진입 버튼을 숨긴다.
    public void EnterNavigationMode()
    {
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        SetStatusBadgeMessage(null);
        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(false);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(false);
        if (landscapeModeButton != null) landscapeModeButton.gameObject.SetActive(false);
        _toolkitBottomBarNavigationMode = true;
        if (_toolkitNavigateButton != null) _toolkitNavigateButton.style.display = UnityEngine.UIElements.DisplayStyle.None;
        if (_toolkitDetailButton != null) _toolkitDetailButton.style.display = UnityEngine.UIElements.DisplayStyle.None;
        SetBottomActionBarToolkitVisible(false);
        RefreshBottomActionBarToolkitLayout();
        if (navigationSearchPanel != null && navigationSearchPanel.activeSelf)
        {
            BringNavigationSurfaceToFront(navigationSearchPanel.transform);
        }
        if (navigationHUD != null && navigationHUD.activeSelf)
        {
            BringNavigationSurfaceToFront(navigationHUD.transform);
        }
    }

    // 내비 모드 종료 시 HUD를 정리하고 기본 스캔 상태 UI로 복귀한다.
    public void ExitNavigationMode()
    {
        HideNavigationHUD();
        HideSearchPanel();
        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(true);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(true);
        _toolkitBottomBarNavigationMode = false;
        SetBottomActionBarToolkitVisible(true);
        if (_toolkitNavigateButton != null) _toolkitNavigateButton.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        if (_toolkitDetailButton != null) _toolkitDetailButton.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        RefreshBottomActionBarToolkitLayout();
        SetScanningMode();
    }

    void BringNavigationSurfaceToFront(Transform navigationSurface)
    {
        if (navigationSurface == null)
        {
            return;
        }

        if (_statusBadgeRoot != null)
        {
            _statusBadgeRoot.SetAsFirstSibling();
        }

        navigationSurface.SetAsLastSibling();

        if (offScreenIndicator != null && offScreenIndicator.gameObject.activeSelf)
        {
            offScreenIndicator.SetAsLastSibling();
        }
    }
    #endregion

    #region Animations
    // 카드를 활성화한 뒤 지정 위치와 알파로 슬라이드 인시킨다.
    void ShowCard(GameObject obj, RectTransform rect, CanvasGroup group, float targetY, ref Coroutine routine) { if (obj == null || rect == null || group == null) return; if (routine != null) StopCoroutine(routine); obj.SetActive(true); routine = StartCoroutine(AnimateMove(rect, group, targetY, 1)); }
    // 카드를 화면 아래로 슬라이드 아웃시키고 완료 후 비활성화한다.
    void HideCard(GameObject obj, RectTransform rect, CanvasGroup group, ref Coroutine routine) { if (obj == null || rect == null || group == null) return; if (routine != null) StopCoroutine(routine); routine = StartCoroutine(AnimateMove(rect, group, slideOffset, 0, () => obj.SetActive(false))); }
    // 카드의 시작 위치와 투명도를 표시 여부에 맞게 즉시 세팅한다.
    void InitializeCard(RectTransform rect, CanvasGroup group, bool visible, float targetY) { if (rect == null || group == null) return; if (visible) { rect.anchoredPosition = new Vector2(0, targetY); group.alpha = 1; } else { rect.anchoredPosition = new Vector2(0, slideOffset); group.alpha = 0; } }

    // 카드 위치와 알파를 부드럽게 보간해 상태 전환 애니메이션을 수행한다.
    IEnumerator AnimateMove(RectTransform rect, CanvasGroup group, float targetY, float targetAlpha, Action onComplete = null) { float startY = rect.anchoredPosition.y; float startAlpha = group.alpha; float time = 0; while (time < animDuration) { time += Time.deltaTime; float t = time / animDuration; t = t * t * (3f - 2f * t); Vector2 pos = rect.anchoredPosition; pos.y = Mathf.Lerp(startY, targetY, t); rect.anchoredPosition = pos; group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t); yield return null; } rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY); group.alpha = targetAlpha; onComplete?.Invoke(); }
    #endregion
}
