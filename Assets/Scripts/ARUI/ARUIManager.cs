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
        RefreshToastLayout();
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
        panelBg.color = new Color(0.02f, 0.05f, 0.09f, 0.985f);

        // 상단 감성 헤더
        GameObject heroCard = new GameObject("SearchHeroCard", typeof(RectTransform), typeof(Image));
        heroCard.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform heroRect = heroCard.GetComponent<RectTransform>();
        heroRect.anchorMin = new Vector2(0f, 1f);
        heroRect.anchorMax = new Vector2(1f, 1f);
        heroRect.pivot = new Vector2(0.5f, 1f);
        heroRect.anchoredPosition = new Vector2(0f, -22f);
        heroRect.sizeDelta = new Vector2(-36f, 126f);

        Image heroBg = heroCard.GetComponent<Image>();
        ApplyRoundedSurface(heroBg, GetNavigationPanelRoundedSprite(), new Color(0.07f, 0.12f, 0.18f, 0.99f));

        GameObject heroAccent = new GameObject("HeroAccent", typeof(RectTransform), typeof(Image));
        heroAccent.transform.SetParent(heroCard.transform, false);
        RectTransform heroAccentRect = heroAccent.GetComponent<RectTransform>();
        heroAccentRect.anchorMin = new Vector2(0f, 1f);
        heroAccentRect.anchorMax = new Vector2(1f, 1f);
        heroAccentRect.pivot = new Vector2(0.5f, 1f);
        heroAccentRect.anchoredPosition = new Vector2(0f, -10f);
        heroAccentRect.sizeDelta = new Vector2(-38f, 6f);
        Image heroAccentImage = heroAccent.GetComponent<Image>();
        ApplyRoundedSurface(heroAccentImage, GetNavigationChipRoundedSprite(), new Color(1f, 0.62f, 0.28f, 0.9f));

        GameObject heroChip = new GameObject("HeroChip", typeof(RectTransform), typeof(Image));
        heroChip.transform.SetParent(heroCard.transform, false);
        RectTransform heroChipRect = heroChip.GetComponent<RectTransform>();
        heroChipRect.anchorMin = new Vector2(0f, 1f);
        heroChipRect.anchorMax = new Vector2(0f, 1f);
        heroChipRect.pivot = new Vector2(0f, 1f);
        heroChipRect.anchoredPosition = new Vector2(18f, -24f);
        heroChipRect.sizeDelta = new Vector2(118f, 28f);
        Image heroChipImage = heroChip.GetComponent<Image>();
        ApplyRoundedSurface(heroChipImage, GetNavigationChipRoundedSprite(), new Color(0.18f, 0.54f, 0.67f, 0.82f));

        TextMeshProUGUI heroChipText = CreateHUDText("HeroChipText", heroChip.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "AR 도보 안내", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        heroChipText.color = new Color(0.91f, 0.98f, 1f, 1f);

        TextMeshProUGUI heroTitle = CreateHUDText("HeroTitle", heroCard.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -56f), new Vector2(-36f, 34f),
            "어디로 걸어갈까요?", 28f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        heroTitle.color = Color.white;

        TextMeshProUGUI heroBody = CreateHUDText("HeroBody", heroCard.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -90f), new Vector2(-36f, 40f),
            "건물명이나 장소명을 검색하면 AR 화살표로 바로 안내해 드려요.", 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        heroBody.color = new Color(0.79f, 0.86f, 0.93f, 0.96f);
        heroBody.textWrappingMode = TextWrappingModes.Normal;
        heroBody.overflowMode = TextOverflowModes.Overflow;

        // 상단 입력 카드
        GameObject topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = new Vector2(0f, -164f);
        topRect.sizeDelta = new Vector2(-36f, 76f);

        Image topBarBg = topBar.GetComponent<Image>();
        ApplyRoundedSurface(topBarBg, GetNavigationPanelRoundedSprite(), new Color(0.09f, 0.14f, 0.21f, 0.995f));

        // 입력 필드
        GameObject inputObj = new GameObject("DestinationInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObj.transform.SetParent(topBar.transform, false);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.offsetMin = new Vector2(14f, 12f);
        inputRect.offsetMax = new Vector2(-214f, -12f);

        Image inputBg = inputObj.GetComponent<Image>();
        ApplyRoundedSurface(inputBg, GetNavigationChipRoundedSprite(), new Color(0.14f, 0.18f, 0.25f, 1f));

        // 입력 필드 텍스트 영역
        GameObject inputTextArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        inputTextArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = inputTextArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(16f, 6f);
        textAreaRect.offsetMax = new Vector2(-16f, -6f);

        GameObject placeholderObj = CreateInputText("Placeholder", inputTextArea.transform, "목적지를 입력하세요", 0.5f);
        GameObject inputTextObj = CreateInputText("Text", inputTextArea.transform, "", 1f);

        TMP_InputField inputField = inputObj.GetComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputTextObj.GetComponent<TextMeshProUGUI>();
        inputField.placeholder = placeholderObj.GetComponent<TextMeshProUGUI>();
        inputField.fontAsset = SharedTMPFont;
        destinationInputField = inputField;

        TextMeshProUGUI placeholderText = placeholderObj.GetComponent<TextMeshProUGUI>();
        if (placeholderText != null)
        {
            placeholderText.fontSize = 20f;
            placeholderText.color = new Color(0.68f, 0.75f, 0.82f, 0.72f);
        }

        TextMeshProUGUI inputText = inputTextObj.GetComponent<TextMeshProUGUI>();
        if (inputText != null)
        {
            inputText.fontSize = 20f;
            inputText.color = new Color(0.97f, 0.99f, 1f, 1f);
        }

        // 검색 버튼
        searchButton = CreatePanelButton("SearchBtn", topBar.transform, "길찾기",
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-78f, 0f), new Vector2(118f, -20f),
            new Color(0.18f, 0.69f, 0.78f, 1f));

        // 닫기 버튼
        closeSearchButton = CreatePanelButton("CloseBtn", topBar.transform, "닫기",
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(68f, -20f),
            new Color(0.28f, 0.34f, 0.42f, 1f));
        closeSearchButton.onClick.AddListener(HideSearchPanel);

        TextMeshProUGUI searchButtonText = searchButton.GetComponentInChildren<TextMeshProUGUI>();
        if (searchButtonText != null)
        {
            searchButtonText.fontSize = 19f;
        }

        // 결과 영역 카드
        GameObject resultsCard = new GameObject("ResultsCard", typeof(RectTransform), typeof(Image));
        resultsCard.transform.SetParent(navigationSearchPanel.transform, false);
        RectTransform resultsCardRect = resultsCard.GetComponent<RectTransform>();
        resultsCardRect.anchorMin = new Vector2(0f, 0f);
        resultsCardRect.anchorMax = new Vector2(1f, 1f);
        resultsCardRect.offsetMin = new Vector2(18f, 100f);
        resultsCardRect.offsetMax = new Vector2(-18f, -252f);

        Image resultsBg = resultsCard.GetComponent<Image>();
        ApplyRoundedSurface(resultsBg, GetNavigationPanelRoundedSprite(), new Color(0.06f, 0.10f, 0.16f, 0.99f));

        // ScrollRect (루트)
        GameObject scrollObj = new GameObject("SearchScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollObj.transform.SetParent(resultsCard.transform, false);
        RectTransform scrollRootRect = scrollObj.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0f, 0f);
        scrollRootRect.anchorMax = new Vector2(1f, 1f);
        scrollRootRect.offsetMin = new Vector2(0f, 0f);
        scrollRootRect.offsetMax = new Vector2(0f, 0f);

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
        contentRect.offsetMin = new Vector2(14f, 0f);
        contentRect.offsetMax = new Vector2(-14f, 0f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 16, 18);
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
        _searchStatusText.transform.SetParent(resultsCard.transform, false);
        RectTransform statusRect = _searchStatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0f, 0f);
        statusRect.sizeDelta = new Vector2(420f, 86f);
        TextMeshProUGUI statusTmp = _searchStatusText.GetComponent<TextMeshProUGUI>();
        statusTmp.text = "";
        statusTmp.fontSize = 22f;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.alignment = TextAlignmentOptions.Center;
        statusTmp.color = new Color(0.82f, 0.89f, 0.95f, 1f);
        statusTmp.textWrappingMode = TextWrappingModes.Normal;
        statusTmp.overflowMode = TextOverflowModes.Overflow;
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
    private NavigationGlyphGraphic _turnIconGraphic;
    private Image _turnIconBackground;
    private TextMeshProUGUI _turnTypeText;
    private TextMeshProUGUI _destinationTitleText;
    private TextMeshProUGUI _distanceCaptionText;
    private TextMeshProUGUI _etaText;
    private Image _progressFill;
    private GameObject _trackingWarningBadge;
    private TextMeshProUGUI _trackingWarningText;
    private GameObject _reroutingIndicator;
    private TextMeshProUGUI _reroutingText;
    private Coroutine _reroutingAnimRoutine;
    private RectTransform _offScreenArrowRoot;
    private NavigationGlyphGraphic _offScreenIndicatorGraphic;
    private TextMeshProUGUI _offScreenIndicatorText;
    private Sprite _navigationPanelRoundedSprite;
    private Sprite _navigationChipRoundedSprite;
    private string _currentNavigationDestination = string.Empty;

    // 토스트 패널을 현재 UI 상태에 맞는 안전한 위치로 재배치한다.
    void RefreshToastLayout(bool bringToFront = false)
    {
        if (toastPanel == null)
        {
            return;
        }

        RectTransform toastRect = toastPanel.GetComponent<RectTransform>();
        if (toastRect != null)
        {
            bool searchPanelActive = navigationSearchPanel != null && navigationSearchPanel.activeSelf;
            bool navigationHudActive = navigationHUD != null && navigationHUD.activeSelf;
            bool navigationSurfaceActive = searchPanelActive || navigationHudActive;
            float toastBottom = searchPanelActive ? 26f : (navigationHudActive ? 110f : 88f);
            float toastHeight = navigationSurfaceActive ? 58f : 54f;

            toastRect.anchorMin = new Vector2(0.5f, 0f);
            toastRect.anchorMax = new Vector2(0.5f, 0f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.anchoredPosition = new Vector2(0f, toastBottom);
            toastRect.sizeDelta = new Vector2(Mathf.Min(Screen.width - 48f, 420f), toastHeight);
        }

        if (toastText != null)
        {
            RectTransform textRect = toastText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);
            toastText.fontSize = 18f;
            toastText.alignment = TextAlignmentOptions.Center;
            toastText.textWrappingMode = TextWrappingModes.Normal;
            toastText.overflowMode = TextOverflowModes.Ellipsis;
            toastText.raycastTarget = false;
            ApplySharedTextStyle(toastText);
        }

        if (bringToFront || toastPanel.activeSelf)
        {
            toastPanel.transform.SetAsLastSibling();
        }
    }

    // 내비 화면 진입 시 상태 배지를 즉시 치워 잔상이 비치지 않게 한다.
    void ForceHideStatusBadge()
    {
        if (_statusBadgeRoutine != null)
        {
            StopCoroutine(_statusBadgeRoutine);
            _statusBadgeRoutine = null;
        }

        SetStatusBadgeHiddenImmediate();
    }

    // 검색 패널을 로딩 문구만 보이는 상태로 초기화한다.
    public void ShowSearchLoading()
    {
        EnsureSearchPanel();
        ClearSearchResults();
        if (_searchStatusText != null)
        {
            _searchStatusText.SetActive(true);
            TextMeshProUGUI tmp = _searchStatusText.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "가까운 목적지를 찾고 있어요\n잠시만 기다려 주세요";
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
            if (tmp != null) tmp.text = string.IsNullOrWhiteSpace(message)
                ? "검색 결과가 없습니다\n다른 건물명이나 장소명을 입력해 주세요"
                : message;
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
        tmp.raycastTarget = false;
        ApplySharedTextStyle(tmp);
        return obj;
    }

    // 길찾기 패널에서 재사용하는 큰 라운드 스프라이트를 지연 생성한다.
    Sprite GetNavigationPanelRoundedSprite()
    {
        if (_navigationPanelRoundedSprite == null)
        {
            _navigationPanelRoundedSprite = CreateRoundedRectSprite(96, 28);
        }

        return _navigationPanelRoundedSprite;
    }

    // 배지와 칩에 쓰는 조금 더 작은 라운드 스프라이트를 지연 생성한다.
    Sprite GetNavigationChipRoundedSprite()
    {
        if (_navigationChipRoundedSprite == null)
        {
            _navigationChipRoundedSprite = CreateRoundedRectSprite(72, 22);
        }

        return _navigationChipRoundedSprite;
    }

    // 라운드 패널 공통 스타일을 적용한다.
    void ApplyRoundedSurface(Image image, Sprite sprite, Color color)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
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
        ApplyRoundedSurface(img, GetNavigationChipRoundedSprite(), bgColor);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = label.Length >= 5 ? 18f : 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        ApplySharedTextStyle(tmp);

        Button button = btnObj.GetComponent<Button>();
        ColorBlock cb = button.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.12f);
        cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.16f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = Color.Lerp(bgColor, Color.black, 0.4f);
        button.colors = cb;
        return button;
    }

    // 검색 패널을 열고 입력/결과/상태를 초기화한다.
    public void ShowSearchPanel()
    {
        EnsureSearchPanel();
        navigationSearchPanel.SetActive(true);
        BringNavigationSurfaceToFront(navigationSearchPanel.transform);
        RefreshToastLayout(true);
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
        RefreshToastLayout(true);
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
        itemRect.sizeDelta = new Vector2(0f, 152f);

        Image itemBg = itemObj.GetComponent<Image>();
        ApplyRoundedSurface(itemBg, GetNavigationPanelRoundedSprite(), new Color(0.08f, 0.13f, 0.19f, 0.995f));

        // 터치 피드백
        Button itemBtn = itemObj.GetComponent<Button>();
        ColorBlock cb = itemBtn.colors;
        cb.normalColor = new Color(0.09f, 0.14f, 0.2f, 0.98f);
        cb.highlightedColor = new Color(0.13f, 0.2f, 0.28f, 1f);
        cb.pressedColor = new Color(0.06f, 0.1f, 0.16f, 1f);
        cb.selectedColor = new Color(0.13f, 0.2f, 0.28f, 1f);
        itemBtn.colors = cb;

        GameObject accentObj = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentObj.transform.SetParent(itemObj.transform, false);
        RectTransform accentRect = accentObj.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(12f, 0f);
        accentRect.sizeDelta = new Vector2(8f, -22f);
        Image accentImage = accentObj.GetComponent<Image>();
        accentImage.color = new Color(0.28f, 0.78f, 0.84f, 0.96f);

        TextMeshProUGUI nameTmp = CreateResultLabel("NameText", itemObj.transform, result.placeName,
            23f, FontStyles.Bold, new Vector2(30f, -16f), new Vector2(-112f, -16f), 32f);
        TextMeshProUGUI addrTmp = CreateResultLabel("AddressText", itemObj.transform,
            result.roadAddressName ?? result.addressName,
            17f, FontStyles.Normal, new Vector2(30f, -58f), new Vector2(-30f, -58f), 42f);
        addrTmp.color = new Color(0.74f, 0.8f, 0.87f, 1f);
        addrTmp.textWrappingMode = TextWrappingModes.Normal;
        addrTmp.overflowMode = TextOverflowModes.Ellipsis;

        // 거리 라벨 (우측 상단)
        if (!string.IsNullOrEmpty(distanceStr))
        {
            GameObject distObj = new GameObject("DistanceChip", typeof(RectTransform), typeof(Image));
            distObj.transform.SetParent(itemObj.transform, false);
            RectTransform distRect = distObj.GetComponent<RectTransform>();
            distRect.anchorMin = new Vector2(1f, 1f);
            distRect.anchorMax = new Vector2(1f, 1f);
            distRect.pivot = new Vector2(1f, 1f);
            distRect.anchoredPosition = new Vector2(-16f, -16f);
            distRect.sizeDelta = new Vector2(84f, 30f);
            Image distBg = distObj.GetComponent<Image>();
            ApplyRoundedSurface(distBg, GetNavigationChipRoundedSprite(), new Color(1f, 0.63f, 0.28f, 0.96f));

            TextMeshProUGUI distTmp = CreateHUDText("DistanceText", distObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                distanceStr, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            distTmp.text = distanceStr;
            distTmp.color = new Color(0.17f, 0.12f, 0.08f, 1f);
        }

        GameObject hintObj = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObj.transform.SetParent(itemObj.transform, false);
        RectTransform hintRect = hintObj.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.offsetMin = new Vector2(30f, 16f);
        hintRect.offsetMax = new Vector2(-24f, 38f);

        TextMeshProUGUI hintTmp = hintObj.GetComponent<TextMeshProUGUI>();
        hintTmp.text = "탭하면 바로 AR 길안내를 시작합니다";
        hintTmp.fontSize = 14f;
        hintTmp.fontStyle = FontStyles.Normal;
        hintTmp.alignment = TextAlignmentOptions.MidlineLeft;
        hintTmp.textWrappingMode = TextWrappingModes.NoWrap;
        hintTmp.overflowMode = TextOverflowModes.Ellipsis;
        hintTmp.raycastTarget = false;
        ApplySharedTextStyle(hintTmp);
        hintTmp.color = new Color(0.53f, 0.83f, 0.9f, 0.88f);

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
        tmp.raycastTarget = false;
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

        // HUD 루트 패널
        navigationHUD = new GameObject("NavigationHUD", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        navigationHUD.transform.SetParent(transform, false);

        RectTransform hudRect = navigationHUD.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(1f, 1f);
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.anchoredPosition = new Vector2(0f, 0f);
        hudRect.sizeDelta = new Vector2(0f, 364f);

        Image hudRootBg = navigationHUD.GetComponent<Image>();
        hudRootBg.color = new Color(0f, 0f, 0f, 0.001f);

        GameObject cardObj = new GameObject("NavigationCard", typeof(RectTransform), typeof(Image));
        cardObj.transform.SetParent(navigationHUD.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);
        cardRect.anchoredPosition = new Vector2(0f, -18f);
        cardRect.sizeDelta = new Vector2(-24f, 340f);

        Image cardBg = cardObj.GetComponent<Image>();
        ApplyRoundedSurface(cardBg, GetNavigationPanelRoundedSprite(), new Color(0.06f, 0.11f, 0.17f, 0.97f));

        GameObject cardAccent = new GameObject("CardAccent", typeof(RectTransform), typeof(Image));
        cardAccent.transform.SetParent(cardObj.transform, false);
        RectTransform cardAccentRect = cardAccent.GetComponent<RectTransform>();
        cardAccentRect.anchorMin = new Vector2(0f, 1f);
        cardAccentRect.anchorMax = new Vector2(1f, 1f);
        cardAccentRect.pivot = new Vector2(0.5f, 1f);
        cardAccentRect.anchoredPosition = new Vector2(0f, -10f);
        cardAccentRect.sizeDelta = new Vector2(-36f, 6f);
        Image cardAccentImage = cardAccent.GetComponent<Image>();
        ApplyRoundedSurface(cardAccentImage, GetNavigationChipRoundedSprite(), new Color(0.28f, 0.77f, 0.84f, 0.9f));

        // 턴 아이콘 배지
        GameObject iconBadge = new GameObject("TurnIconBadge", typeof(RectTransform), typeof(Image));
        iconBadge.transform.SetParent(cardObj.transform, false);
        RectTransform iconBadgeRect = iconBadge.GetComponent<RectTransform>();
        iconBadgeRect.anchorMin = new Vector2(0f, 1f);
        iconBadgeRect.anchorMax = new Vector2(0f, 1f);
        iconBadgeRect.pivot = new Vector2(0f, 1f);
        iconBadgeRect.anchoredPosition = new Vector2(18f, -18f);
        iconBadgeRect.sizeDelta = new Vector2(88f, 88f);
        _turnIconBackground = iconBadge.GetComponent<Image>();
        ApplyRoundedSurface(_turnIconBackground, GetNavigationPanelRoundedSprite(), new Color(0.12f, 0.28f, 0.34f, 0.98f));

        GameObject glyphObj = new GameObject("TurnGlyph", typeof(RectTransform), typeof(NavigationGlyphGraphic));
        glyphObj.transform.SetParent(iconBadge.transform, false);
        RectTransform glyphRect = glyphObj.GetComponent<RectTransform>();
        glyphRect.anchorMin = Vector2.zero;
        glyphRect.anchorMax = Vector2.one;
        glyphRect.offsetMin = new Vector2(18f, 18f);
        glyphRect.offsetMax = new Vector2(-18f, -18f);
        _turnIconGraphic = glyphObj.GetComponent<NavigationGlyphGraphic>();
        _turnIconGraphic.color = new Color(0.86f, 0.98f, 1f, 1f);
        _turnIconGraphic.SetGlyph(NavigationGlyphGraphic.GlyphKind.Straight);

        GameObject destinationChip = new GameObject("DestinationChip", typeof(RectTransform), typeof(Image));
        destinationChip.transform.SetParent(cardObj.transform, false);
        RectTransform destinationChipRect = destinationChip.GetComponent<RectTransform>();
        destinationChipRect.anchorMin = new Vector2(0f, 1f);
        destinationChipRect.anchorMax = new Vector2(1f, 1f);
        destinationChipRect.pivot = new Vector2(0f, 1f);
        destinationChipRect.anchoredPosition = new Vector2(118f, -18f);
        destinationChipRect.sizeDelta = new Vector2(-308f, 30f);
        Image destinationChipBg = destinationChip.GetComponent<Image>();
        ApplyRoundedSurface(destinationChipBg, GetNavigationChipRoundedSprite(), new Color(0.11f, 0.18f, 0.24f, 0.96f));

        _destinationTitleText = CreateHUDText("DestinationTitle", destinationChip.transform,
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(-28f, 0f),
            string.IsNullOrWhiteSpace(_currentNavigationDestination) ? "선택한 목적지" : _currentNavigationDestination,
            16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _destinationTitleText.color = new Color(0.94f, 0.98f, 1f, 1f);

        GameObject turnTypeChip = new GameObject("TurnTypeChip", typeof(RectTransform), typeof(Image));
        turnTypeChip.transform.SetParent(cardObj.transform, false);
        RectTransform turnTypeChipRect = turnTypeChip.GetComponent<RectTransform>();
        turnTypeChipRect.anchorMin = new Vector2(0f, 1f);
        turnTypeChipRect.anchorMax = new Vector2(0f, 1f);
        turnTypeChipRect.pivot = new Vector2(0f, 1f);
        turnTypeChipRect.anchoredPosition = new Vector2(118f, -56f);
        turnTypeChipRect.sizeDelta = new Vector2(110f, 28f);
        Image turnTypeChipBg = turnTypeChip.GetComponent<Image>();
        ApplyRoundedSurface(turnTypeChipBg, GetNavigationChipRoundedSprite(), new Color(0.18f, 0.5f, 0.58f, 0.94f));

        _turnTypeText = CreateHUDText("TurnTypeText", turnTypeChip.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "직진", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        _turnTypeText.color = new Color(0.95f, 0.99f, 1f, 1f);

        // 트래킹 경고 배지
        _trackingWarningBadge = new GameObject("TrackingWarning", typeof(RectTransform), typeof(Image));
        _trackingWarningBadge.transform.SetParent(cardObj.transform, false);
        RectTransform warningRect = _trackingWarningBadge.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(1f, 1f);
        warningRect.anchorMax = new Vector2(1f, 1f);
        warningRect.pivot = new Vector2(1f, 1f);
        warningRect.anchoredPosition = new Vector2(-18f, -18f);
        warningRect.sizeDelta = new Vector2(134f, 28f);
        Image warningBg = _trackingWarningBadge.GetComponent<Image>();
        ApplyRoundedSurface(warningBg, GetNavigationChipRoundedSprite(), new Color(0.9f, 0.42f, 0.18f, 0.96f));
        _trackingWarningText = CreateHUDText("TrackingWarningText", _trackingWarningBadge.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "위치 정확도 낮음", 13f, FontStyles.Bold, TextAlignmentOptions.Center);
        _trackingWarningText.color = new Color(1f, 0.97f, 0.92f, 1f);
        _trackingWarningBadge.SetActive(false);

        remainingDistanceText = CreateHUDText("RemainingDistance", cardObj.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -104f), new Vector2(-34f, 54f),
            "경로 계산 중...", 36f, FontStyles.Bold, TextAlignmentOptions.Center);
        remainingDistanceText.color = Color.white;

        _distanceCaptionText = CreateHUDText("DistanceCaption", cardObj.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -162f), new Vector2(-34f, 18f),
            "목적지까지 남은 거리", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
        _distanceCaptionText.color = new Color(0.72f, 0.82f, 0.9f, 0.92f);

        // ETA 텍스트
        _etaText = CreateHUDText("ETA", cardObj.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -186f), new Vector2(-40f, 22f),
            "", 17f, FontStyles.Normal, TextAlignmentOptions.Center);
        _etaText.color = new Color(0.78f, 0.87f, 0.94f, 0.96f);

        // 다음 안내 텍스트
        nextGuideText = CreateHUDText("NextGuide", cardObj.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -214f), new Vector2(-40f, 44f),
            "안내를 준비하고 있어요", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
        nextGuideText.color = new Color(0.9f, 0.97f, 1f, 1f);
        nextGuideText.textWrappingMode = TextWrappingModes.Normal;
        nextGuideText.overflowMode = TextOverflowModes.Overflow;

        // 진행률 바 (배경)
        GameObject progressBg = new GameObject("ProgressBg", typeof(RectTransform), typeof(Image));
        progressBg.transform.SetParent(cardObj.transform, false);
        RectTransform pbgRect = progressBg.GetComponent<RectTransform>();
        pbgRect.anchorMin = new Vector2(0f, 0f);
        pbgRect.anchorMax = new Vector2(1f, 0f);
        pbgRect.pivot = new Vector2(0.5f, 0f);
        pbgRect.anchoredPosition = new Vector2(0f, 62f);
        pbgRect.sizeDelta = new Vector2(-36f, 8f);
        Image pbgImg = progressBg.GetComponent<Image>();
        ApplyRoundedSurface(pbgImg, GetNavigationChipRoundedSprite(), new Color(0.14f, 0.19f, 0.25f, 1f));

        // 진행률 바 (채움)
        GameObject progressFillObj = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        progressFillObj.transform.SetParent(progressBg.transform, false);
        RectTransform pfRect = progressFillObj.GetComponent<RectTransform>();
        pfRect.anchorMin = new Vector2(0f, 0f);
        pfRect.anchorMax = new Vector2(1f, 1f);
        pfRect.offsetMin = Vector2.zero;
        pfRect.offsetMax = Vector2.zero;
        _progressFill = progressFillObj.GetComponent<Image>();
        ApplyRoundedSurface(_progressFill, GetNavigationChipRoundedSprite(), new Color(0.28f, 0.77f, 0.84f, 1f));
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = 0;
        _progressFill.fillAmount = 0f;

        // 중지 버튼 (하단 오른쪽)
        stopNavigationButton = CreatePanelButton("StopNavBtn", cardObj.transform, "안내 종료",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-18f, 16f), new Vector2(118f, 42f),
            new Color(0.89f, 0.36f, 0.24f, 1f));
        BindStopNavigationButton();

        // 화면 보정 버튼 (하단 왼쪽 - drift 발생 시 현재 카메라 위치 기준으로 화살표 재정렬)
        recalibrateButton = CreatePanelButton("RecalibrateBtn", cardObj.transform, "화면 보정",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(18f, 16f), new Vector2(118f, 42f),
            new Color(0.18f, 0.69f, 0.78f, 1f));
        BindRecalibrateButton();

        // 리루트 인디케이터
        _reroutingIndicator = new GameObject("ReroutingIndicator", typeof(RectTransform), typeof(Image));
        _reroutingIndicator.transform.SetParent(cardObj.transform, false);
        RectTransform riRect = _reroutingIndicator.GetComponent<RectTransform>();
        riRect.anchorMin = new Vector2(0.5f, 1f);
        riRect.anchorMax = new Vector2(0.5f, 1f);
        riRect.pivot = new Vector2(0.5f, 1f);
        riRect.anchoredPosition = new Vector2(0f, -92f);
        riRect.sizeDelta = new Vector2(196f, 30f);
        Image reroutingBg = _reroutingIndicator.GetComponent<Image>();
        ApplyRoundedSurface(reroutingBg, GetNavigationChipRoundedSprite(), new Color(0.97f, 0.71f, 0.22f, 0.96f));
        _reroutingText = CreateHUDText("ReroutingText", _reroutingIndicator.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "경로 재탐색 중", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        _reroutingText.color = new Color(0.28f, 0.16f, 0.04f, 1f);
        _reroutingIndicator.SetActive(false);

        // 화면 밖 방향 표시 인디케이터
        if (offScreenIndicator == null)
        {
            GameObject indicatorObj = new GameObject("OffScreenIndicator", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            indicatorObj.transform.SetParent(transform, false);
            offScreenIndicator = indicatorObj.GetComponent<RectTransform>();
            offScreenIndicator.sizeDelta = new Vector2(84f, 84f);

            Image indicatorBg = indicatorObj.GetComponent<Image>();
            ApplyRoundedSurface(indicatorBg, GetNavigationPanelRoundedSprite(), new Color(0.05f, 0.12f, 0.18f, 0.95f));
            indicatorBg.raycastTarget = false;

            GameObject indicatorArrowObj = new GameObject("Arrow", typeof(RectTransform));
            indicatorArrowObj.transform.SetParent(offScreenIndicator, false);
            _offScreenArrowRoot = indicatorArrowObj.GetComponent<RectTransform>();
            _offScreenArrowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _offScreenArrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _offScreenArrowRoot.pivot = new Vector2(0.5f, 0.5f);
            _offScreenArrowRoot.anchoredPosition = new Vector2(0f, 8f);
            _offScreenArrowRoot.sizeDelta = new Vector2(32f, 32f);

            GameObject indicatorGlyphObj = new GameObject("Glyph", typeof(RectTransform), typeof(NavigationGlyphGraphic));
            indicatorGlyphObj.transform.SetParent(_offScreenArrowRoot, false);
            RectTransform indicatorGlyphRect = indicatorGlyphObj.GetComponent<RectTransform>();
            indicatorGlyphRect.anchorMin = Vector2.zero;
            indicatorGlyphRect.anchorMax = Vector2.one;
            indicatorGlyphRect.offsetMin = Vector2.zero;
            indicatorGlyphRect.offsetMax = Vector2.zero;
            _offScreenIndicatorGraphic = indicatorGlyphObj.GetComponent<NavigationGlyphGraphic>();
            _offScreenIndicatorGraphic.color = new Color(0.88f, 0.98f, 1f, 1f);
            _offScreenIndicatorGraphic.SetGlyph(NavigationGlyphGraphic.GlyphKind.Straight);

            _offScreenIndicatorText = CreateHUDText("OffScreenLabel", offScreenIndicator,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(56f, 18f),
                "경로", 12f, FontStyles.Bold, TextAlignmentOptions.Center);
            _offScreenIndicatorText.color = new Color(0.78f, 0.9f, 0.97f, 0.98f);
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
        tmp.raycastTarget = false;
        ApplySharedTextStyle(tmp);
        return tmp;
    }

    // 내비 HUD를 표시한다.
    public void ShowNavigationHUD()
    {
        EnsureNavigationHUD();
        navigationHUD.SetActive(true);
        BringNavigationSurfaceToFront(navigationHUD.transform);
        RefreshToastLayout(true);
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
        RefreshToastLayout(true);
    }

    // 현재 길안내 중인 목적지명을 HUD에 반영한다.
    public void SetNavigationDestination(string destinationName)
    {
        _currentNavigationDestination = string.IsNullOrWhiteSpace(destinationName)
            ? string.Empty
            : destinationName.Trim();

        if (_destinationTitleText != null)
        {
            _destinationTitleText.text = string.IsNullOrWhiteSpace(_currentNavigationDestination)
                ? "선택한 목적지"
                : _currentNavigationDestination;
        }
    }

    // 남은 총 거리를 HUD 상단 문구 형식으로 갱신한다.
    public void UpdateRemainingDistance(float meters)
    {
        EnsureNavigationHUD();
        if (remainingDistanceText == null) return;
        remainingDistanceText.text = FormatCompactDistance(meters);

        if (_distanceCaptionText != null)
        {
            _distanceCaptionText.text = meters <= 25f
                ? "거의 도착했어요"
                : "목적지까지 남은 거리";
        }
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
        string normalizedGuidance = string.IsNullOrWhiteSpace(guidance) ? string.Empty : guidance.Trim();

        if (nextGuideText != null)
        {
            if (string.IsNullOrWhiteSpace(normalizedGuidance))
            {
                nextGuideText.text = "화살표를 따라 천천히 이동해 주세요";
            }
            else if (distance > 0f)
            {
                nextGuideText.text = $"{FormatCompactDistance(distance)} 뒤 {normalizedGuidance}";
            }
            else
            {
                nextGuideText.text = normalizedGuidance;
            }
        }

        if (_turnTypeText != null)
        {
            _turnTypeText.text = GetTurnLabel(guideType);
        }

        if (_turnIconGraphic != null)
        {
            _turnIconGraphic.SetGlyph(GetTurnGlyphKind(guideType));
            _turnIconGraphic.color = GetTurnAccentColor(guideType);
        }

        if (_turnIconBackground != null)
        {
            _turnIconBackground.color = Color.Lerp(
                new Color(0.08f, 0.14f, 0.18f, 0.98f),
                GetTurnAccentColor(guideType),
                0.24f
            );
        }
    }

    // HUD와 검색 패널에서 공통으로 쓰는 짧은 거리 문자열을 만든다.
    string FormatCompactDistance(float meters)
    {
        if (meters <= 0f)
        {
            return "0m";
        }

        if (meters >= 1000f)
        {
            return $"{meters / 1000f:F1}km";
        }

        return $"{Mathf.Max(1, Mathf.RoundToInt(meters))}m";
    }

    // turnType을 사용자용 짧은 안내 라벨로 변환한다.
    string GetTurnLabel(int type)
    {
        switch (type)
        {
            case 12:
            case 16:
            case 17:
                return "좌회전";
            case 13:
            case 18:
            case 19:
                return "우회전";
            case 14:
                return "U턴";
            case 125:
                return "육교";
            case 126:
                return "지하보도";
            case 127:
                return "계단";
            case 128:
                return "경사로";
            case 201:
                return "도착";
            case 211:
            case 212:
            case 213:
            case 214:
            case 215:
            case 216:
            case 217:
                return "횡단보도";
            case 218:
                return "엘리베이터";
            default:
                return "직진";
        }
    }

    // turnType을 벡터 아이콘 종류로 변환한다.
    NavigationGlyphGraphic.GlyphKind GetTurnGlyphKind(int type)
    {
        switch (type)
        {
            case 12:
            case 17:
                return NavigationGlyphGraphic.GlyphKind.TurnLeft;
            case 13:
            case 19:
                return NavigationGlyphGraphic.GlyphKind.TurnRight;
            case 16:
                return NavigationGlyphGraphic.GlyphKind.SlightLeft;
            case 18:
                return NavigationGlyphGraphic.GlyphKind.SlightRight;
            case 14:
                return NavigationGlyphGraphic.GlyphKind.UTurn;
            case 125:
                return NavigationGlyphGraphic.GlyphKind.Bridge;
            case 126:
                return NavigationGlyphGraphic.GlyphKind.Underpass;
            case 127:
                return NavigationGlyphGraphic.GlyphKind.Stairs;
            case 128:
                return NavigationGlyphGraphic.GlyphKind.Ramp;
            case 201:
                return NavigationGlyphGraphic.GlyphKind.Arrival;
            case 211:
            case 212:
            case 213:
            case 214:
            case 215:
            case 216:
            case 217:
                return NavigationGlyphGraphic.GlyphKind.Crosswalk;
            case 218:
                return NavigationGlyphGraphic.GlyphKind.Elevator;
            default:
                return NavigationGlyphGraphic.GlyphKind.Straight;
        }
    }

    // 안내 종류별 강조색을 정해 직관적인 상태 변화를 만든다.
    Color GetTurnAccentColor(int type)
    {
        switch (type)
        {
            case 12:
            case 13:
            case 14:
            case 16:
            case 17:
            case 18:
            case 19:
                return new Color(1f, 0.7f, 0.32f, 1f);
            case 125:
            case 126:
            case 127:
            case 128:
            case 218:
                return new Color(0.82f, 0.9f, 0.56f, 1f);
            case 201:
                return new Color(1f, 0.54f, 0.36f, 1f);
            case 211:
            case 212:
            case 213:
            case 214:
            case 215:
            case 216:
            case 217:
                return new Color(0.48f, 0.86f, 0.84f, 1f);
            default:
                return new Color(0.84f, 0.97f, 1f, 1f);
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
            _etaText.text = "곧 도착해요";
        }
        else
        {
            DateTime arrival = DateTime.Now.AddSeconds(remainingSeconds);
            _etaText.text = $"{minutes}분 남음 · {arrival:HH:mm} 도착 예정";
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
        {
            _trackingWarningBadge.SetActive(show);
        }
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
            if (_reroutingText != null)
            {
                _reroutingText.text = "경로 재탐색 중";
            }

            _reroutingAnimRoutine = StartCoroutine(AnimateReroutingDots());
        }
    }

    // 재탐색 문구 뒤의 점 개수를 순환시켜 진행 중 느낌을 만든다.
    IEnumerator AnimateReroutingDots()
    {
        TextMeshProUGUI tmp = _reroutingText;
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
        if (_offScreenArrowRoot != null)
        {
            _offScreenArrowRoot.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        if (_offScreenIndicatorText != null)
        {
            _offScreenIndicatorText.text = screenPoint.z < 0f ? "뒤쪽" : "경로";
        }
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
        RefreshToastLayout();

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
        ForceHideStatusBadge();
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
        RefreshToastLayout(true);
    }

    // 내비 모드 종료 시 HUD를 정리하고 기본 스캔 상태 UI로 복귀한다.
    public void ExitNavigationMode()
    {
        HideNavigationHUD();
        HideSearchPanel();
        SetNavigationDestination(string.Empty);
        if (mainNavigateButton != null) mainNavigateButton.gameObject.SetActive(true);
        if (worldInfoDetailButton != null) worldInfoDetailButton.gameObject.SetActive(true);
        _toolkitBottomBarNavigationMode = false;
        SetBottomActionBarToolkitVisible(true);
        if (_toolkitNavigateButton != null) _toolkitNavigateButton.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        if (_toolkitDetailButton != null) _toolkitDetailButton.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        RefreshBottomActionBarToolkitLayout();
        RefreshToastLayout(true);
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
