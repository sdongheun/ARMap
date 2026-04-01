using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class ARUIManager : MonoBehaviour
{
    [Serializable]
    public class ScreenMarkerData
    {
        public string id;
        public string label;
        public string subtitle;
        public Vector2 screenPosition;
        public bool isSelected;
    }

    private class ScreenMarkerView
    {
        public GameObject root;
        public RectTransform rectTransform;
        public TextMeshProUGUI pinShadowCircleText;
        public TextMeshProUGUI pinShadowTailText;
        public TextMeshProUGUI pinGlowCircleText;
        public TextMeshProUGUI pinGlowTailText;
        public TextMeshProUGUI pinBodyCircleText;
        public TextMeshProUGUI pinBodyTailText;
        public TextMeshProUGUI pinHoleText;
        public TextMeshProUGUI pinHighlightText;
        public Image labelBackground;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
    }

    // --- Inspector Variables ---
    [Header("1. Main Cards (1~3)")]
    public GameObject scanningCard;
    public GameObject detectedCard;
    public GameObject quickInfoCard;

    [Header("2. Quick Info Content")]
    public Image quickInfoIcon;
    public TextMeshProUGUI quickTitleText;    
    public TextMeshProUGUI quickCategoryText; 
    public TextMeshProUGUI quickAddressText;  
    
    [Header("3. Main Buttons")]
    public Button actionButton;
    public TextMeshProUGUI actionButtonText; 
    public Button openDetailButton;

    [Header("4. Icons")]
    public Sprite iconScanning;
    public Sprite iconDetected;
    public Sprite iconBuilding;

    [Header("5. Detail View (Page 4)")]
    public GameObject detailViewObject;
    public RectTransform detailPanelRect;
    public ScrollRect detailScrollView;
    
    public TextMeshProUGUI detailTitle;
    public TextMeshProUGUI detailCategory;
    public TextMeshProUGUI detailAddress;
    public TextMeshProUGUI detailZipCode;
    public TextMeshProUGUI detailPhone;
    
    [Header("6. Detail View Buttons")]
    public Button closeDetailButton;
    public Button copyAddressButton;
    public Button callPhoneButton;
    public Button openMapButton;

    [Header("7. Facility List (Dynamic)")]
    public GameObject facilityItemPrefab;
    public Transform facilityContainer;

    [Header("8. Animation Settings")]
    public float animDuration = 0.3f; 
    public float slideOffset = 150f; 
    public float statusCardPosY = 0f;    
    public float quickCardPosY = -50f;   

    [Header("9. Toast Message")]
    public GameObject toastPanel;
    public TextMeshProUGUI toastText;
    public float toastDuration = 2.0f;
    private Coroutine _toastRoutine;

    // --- Internal Variables ---
    public event Action OnClickDetail;
    public event Action OnDetailOpened;
    public event Action OnDetailClosed;
    private enum UIState { None, Scanning, Detected, QuickInfo }
    private UIState currentState = UIState.None;
    private BuildingData _currentDetailData;
    private BuildingData _originalDetailData;
    private Coroutine _detailRoutine;
    private float _hiddenY; 

    private RectTransform _scanRect, _detectRect, _quickRect;
    private CanvasGroup _scanGroup, _detectGroup, _quickGroup;
    private Coroutine _scanRoutine, _detectRoutine, _quickRoutine;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private RectTransform _screenMarkerRoot;
    private readonly Dictionary<string, ScreenMarkerView> _screenMarkerViews = new Dictionary<string, ScreenMarkerView>();

    void Awake()
    {
        _scanRect = scanningCard.GetComponent<RectTransform>(); _scanGroup = scanningCard.GetComponent<CanvasGroup>();
        _detectRect = detectedCard.GetComponent<RectTransform>(); _detectGroup = detectedCard.GetComponent<CanvasGroup>();
        _quickRect = quickInfoCard.GetComponent<RectTransform>(); _quickGroup = quickInfoCard.GetComponent<CanvasGroup>();
        _canvasRect = GetComponent<RectTransform>();
        _canvas = GetComponent<Canvas>();
    }

    void Start()
    {
        if (openDetailButton != null)
        {
            openDetailButton.onClick.AddListener(() => OnClickDetail?.Invoke());
        }

        if (closeDetailButton != null) closeDetailButton.onClick.AddListener(CloseDetailView);
        if (copyAddressButton != null) copyAddressButton.onClick.AddListener(OnCopyAddress);
        if (callPhoneButton != null) callPhoneButton.onClick.AddListener(OnCallPhone);
        if (openMapButton != null) openMapButton.onClick.AddListener(OnOpenMap);

        InitializeCard(_scanRect, _scanGroup, true, statusCardPosY);
        InitializeCard(_detectRect, _detectGroup, false, statusCardPosY);
        InitializeCard(_quickRect, _quickGroup, false, quickCardPosY);

        if (detailViewObject != null) detailViewObject.SetActive(false);
        
        if (detailPanelRect != null) _hiddenY = -2500f;

        EnsureScreenMarkerRoot();
        ApplyCodeDrivenDetailTheme();
        SetPrimaryButtonsVisible(false);
    }

    #region Main Card State Control
    // 화면 상태 전환 (1번: 스캔 중)
    public void SetScanningMode()
    {
        if (currentState == UIState.Scanning) return;
        currentState = UIState.Scanning;
        SetPrimaryButtonsVisible(false);
        ShowCard(scanningCard, _scanRect, _scanGroup, statusCardPosY, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        HideCard(quickInfoCard, _quickRect, _quickGroup, ref _quickRoutine);
    }

    // 화면 상태 전환 (2번: 건물 감지됨)
    public void SetDetectedMode()
    {
        if (currentState == UIState.Detected) return;
        currentState = UIState.Detected;
        SetPrimaryButtonsVisible(false);
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        ShowCard(detectedCard, _detectRect, _detectGroup, statusCardPosY, ref _detectRoutine);
        HideCard(quickInfoCard, _quickRect, _quickGroup, ref _quickRoutine);
    }

    // 화면 상태 전환 (3번: 요약 정보 표시)
    public void ShowQuickInfo(BuildingData data)
    {
        currentState = UIState.QuickInfo;
        if (quickInfoIcon != null) quickInfoIcon.sprite = iconBuilding;
        quickTitleText.text = data.buildingName;
        quickCategoryText.text = string.IsNullOrEmpty(data.description) ? "장소 정보" : data.description;
        quickAddressText.text = data.fetchedAddress;
        SetPrimaryButtonsVisible(true);
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        ShowCard(quickInfoCard, _quickRect, _quickGroup, quickCardPosY, ref _quickRoutine);
    }
    #endregion

    void SetPrimaryButtonsVisible(bool visible)
    {
        if (openDetailButton != null)
        {
            openDetailButton.gameObject.SetActive(visible);
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
            Color accentColor = data.isSelected ? new Color(1.0f, 0.22f, 0.18f, 0.62f) : new Color(1.0f, 0.16f, 0.12f, 0.42f);
            float circleSize = data.isSelected ? 56f : 46f;
            float tailSize = data.isSelected ? 38f : 32f;
            float holeSize = data.isSelected ? 23f : 18f;
            float highlightSize = data.isSelected ? 16f : 12f;

            view.pinShadowCircleText.fontSize = circleSize;
            view.pinShadowTailText.fontSize = tailSize;
            view.pinGlowCircleText.fontSize = circleSize + 5f;
            view.pinGlowTailText.fontSize = tailSize + 4f;
            view.pinBodyCircleText.fontSize = circleSize;
            view.pinBodyTailText.fontSize = tailSize;
            view.pinHoleText.fontSize = holeSize;
            view.pinHighlightText.fontSize = highlightSize;

            view.pinGlowCircleText.color = accentColor;
            view.pinGlowTailText.color = accentColor;

            bool showExpandedLabel = data.isSelected;
            view.labelBackground.gameObject.SetActive(showExpandedLabel);
            view.titleText.gameObject.SetActive(showExpandedLabel);
            view.subtitleText.gameObject.SetActive(showExpandedLabel);
            view.titleText.text = data.label;
            view.subtitleText.text = data.subtitle ?? string.Empty;
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
        rootRect.sizeDelta = new Vector2(260f, 120f);

        TextMeshProUGUI shadowCircleText = CreateScreenMarkerText("PinShadowCircle", rootObject.transform, 3f, -2f, 72f, 72f, TextAlignmentOptions.Center);
        TextMeshProUGUI shadowTailText = CreateScreenMarkerText("PinShadowTail", rootObject.transform, 3f, -26f, 48f, 36f, TextAlignmentOptions.Center);
        TextMeshProUGUI glowCircleText = CreateScreenMarkerText("PinGlowCircle", rootObject.transform, 0f, 0f, 78f, 78f, TextAlignmentOptions.Center);
        TextMeshProUGUI glowTailText = CreateScreenMarkerText("PinGlowTail", rootObject.transform, 0f, -24f, 54f, 40f, TextAlignmentOptions.Center);
        TextMeshProUGUI bodyCircleText = CreateScreenMarkerText("PinBodyCircle", rootObject.transform, 0f, 0f, 72f, 72f, TextAlignmentOptions.Center);
        TextMeshProUGUI bodyTailText = CreateScreenMarkerText("PinBodyTail", rootObject.transform, 0f, -24f, 48f, 36f, TextAlignmentOptions.Center);
        TextMeshProUGUI holeText = CreateScreenMarkerText("PinHole", rootObject.transform, 0f, 6f, 24f, 24f, TextAlignmentOptions.Center);
        TextMeshProUGUI highlightText = CreateScreenMarkerText("PinHighlight", rootObject.transform, -8f, 14f, 18f, 18f, TextAlignmentOptions.Center);

        Image labelBackground = CreateScreenMarkerBackground(rootObject.transform, 0f, -54f, 240f, 58f);
        TextMeshProUGUI titleText = CreateScreenMarkerText("Title", labelBackground.transform, 0f, 10f, 208f, 24f, TextAlignmentOptions.Center);
        TextMeshProUGUI subtitleText = CreateScreenMarkerText("Subtitle", labelBackground.transform, 0f, -12f, 208f, 20f, TextAlignmentOptions.Center);
        shadowCircleText.text = "●";
        shadowTailText.text = "▼";
        glowCircleText.text = "●";
        glowTailText.text = "▼";
        bodyCircleText.text = "●";
        bodyTailText.text = "▼";
        holeText.text = "●";
        highlightText.text = "●";
        shadowCircleText.fontSize = 46f;
        shadowTailText.fontSize = 32f;
        glowCircleText.fontSize = 51f;
        glowTailText.fontSize = 36f;
        bodyCircleText.fontSize = 46f;
        bodyTailText.fontSize = 32f;
        holeText.fontSize = 18f;
        highlightText.fontSize = 12f;
        titleText.fontSize = 22f;
        subtitleText.fontSize = 16f;
        shadowCircleText.color = new Color(0f, 0f, 0f, 0.24f);
        shadowTailText.color = new Color(0f, 0f, 0f, 0.24f);
        glowCircleText.color = new Color(1.0f, 0.16f, 0.12f, 0.42f);
        glowTailText.color = new Color(1.0f, 0.16f, 0.12f, 0.42f);
        bodyCircleText.color = new Color(0.06f, 0.07f, 0.1f, 1f);
        bodyTailText.color = new Color(0.06f, 0.07f, 0.1f, 1f);
        holeText.color = Color.white;
        highlightText.color = new Color(1f, 1f, 1f, 0.2f);
        titleText.color = Color.white;
        subtitleText.color = new Color(0.86f, 0.93f, 1f, 0.95f);
        shadowCircleText.enableWordWrapping = false;
        shadowTailText.enableWordWrapping = false;
        glowCircleText.enableWordWrapping = false;
        glowTailText.enableWordWrapping = false;
        bodyCircleText.enableWordWrapping = false;
        bodyTailText.enableWordWrapping = false;
        holeText.enableWordWrapping = false;
        highlightText.enableWordWrapping = false;
        shadowCircleText.raycastTarget = false;
        shadowTailText.raycastTarget = false;
        glowCircleText.raycastTarget = false;
        glowTailText.raycastTarget = false;
        bodyCircleText.raycastTarget = false;
        bodyTailText.raycastTarget = false;
        holeText.raycastTarget = false;
        highlightText.raycastTarget = false;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        subtitleText.enableWordWrapping = false;
        subtitleText.overflowMode = TextOverflowModes.Ellipsis;
        labelBackground.gameObject.SetActive(false);
        titleText.gameObject.SetActive(false);
        subtitleText.gameObject.SetActive(false);

        ScreenMarkerView view = new ScreenMarkerView
        {
            root = rootObject,
            rectTransform = rootRect,
            pinShadowCircleText = shadowCircleText,
            pinShadowTailText = shadowTailText,
            pinGlowCircleText = glowCircleText,
            pinGlowTailText = glowTailText,
            pinBodyCircleText = bodyCircleText,
            pinBodyTailText = bodyTailText,
            pinHoleText = holeText,
            pinHighlightText = highlightText,
            labelBackground = labelBackground,
            titleText = titleText,
            subtitleText = subtitleText
        };

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
        if (quickTitleText != null)
        {
            tmp.font = quickTitleText.font;
            tmp.fontSharedMaterial = quickTitleText.fontSharedMaterial;
        }
        return tmp;
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
        image.color = new Color(0.04f, 0.08f, 0.14f, 0.92f);
        return image;
    }

    Vector2 ClampToCanvas(Vector2 screenPosition)
    {
        Vector2 localPoint;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, uiCamera, out localPoint);

        const float paddingX = 120f;
        const float paddingY = 160f;
        float halfWidth = _canvasRect.rect.width * 0.5f;
        float halfHeight = _canvasRect.rect.height * 0.5f;

        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth + paddingX, halfWidth - paddingX);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight + paddingY, halfHeight - paddingY);
        return localPoint;
    }

    #region Detail View Control
    // 상세 페이지 열기 및 데이터 채우기
    public void OpenDetailView(BuildingData data)
    {
        _currentDetailData = data;
        _originalDetailData = data;

        if (detailViewObject != null)
        {
            detailViewObject.transform.SetAsLastSibling();
        }

        detailTitle.text = data.buildingName;
        detailCategory.text = string.IsNullOrEmpty(data.description) ? "상업 시설" : data.description;
        detailAddress.text = data.fetchedAddress;
        
        if (detailZipCode != null)
        {
            if (string.IsNullOrEmpty(data.zipCode))
            {
                detailZipCode.text = "";
                detailZipCode.gameObject.SetActive(false);
            }
            else
            {
                detailZipCode.text = "지번: " + data.zipCode; 
                detailZipCode.gameObject.SetActive(true);
            }
        }

        if (string.IsNullOrEmpty(data.phoneNumber)) {
            detailPhone.text = "전화번호 없음";
            callPhoneButton.interactable = false;
        } else {
            detailPhone.text = data.phoneNumber;
            callPhoneButton.interactable = true;
        }

        UpdateFacilityList(data.facilities);

        detailViewObject.SetActive(true);
        ClearScreenMarkers();
        ToggleScreenMarkerOverlay(false);
        if (detailScrollView != null) detailScrollView.verticalNormalizedPosition = 1f;
        OnDetailOpened?.Invoke();
        
        if (_detailRoutine != null) StopCoroutine(_detailRoutine);
        _detailRoutine = StartCoroutine(AnimateDetailPanel(_hiddenY, 0f));
    }

    // 상세 페이지 닫기
    public void CloseDetailView()
    {
        if (_detailRoutine != null) StopCoroutine(_detailRoutine);

        if (detailPanelRect == null)
        {
            FinalizeDetailClose();
            return;
        }

        float startY = detailPanelRect.anchoredPosition.y;
        _detailRoutine = StartCoroutine(AnimateDetailPanel(startY, _hiddenY, FinalizeDetailClose));
    }

    void FinalizeDetailClose()
    {
        if (detailViewObject != null)
        {
            detailViewObject.SetActive(false);
        }

        ToggleScreenMarkerOverlay(true);
        OnDetailClosed?.Invoke();
    }

    // 입점 상가 리스트 동적 생성
    void UpdateFacilityList(List<FacilityInfo> facilities)
    {
        foreach (Transform child in facilityContainer)
        {
            if (child.name == "Header") continue;
            Destroy(child.gameObject);
        }

        if (facilities == null || facilities.Count == 0)
        {
            GameObject emptyItem = Instantiate(facilityItemPrefab, facilityContainer);
            StyleFacilityItem(emptyItem);
            TextMeshProUGUI nameTxt = emptyItem.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI phoneTxt = emptyItem.transform.Find("PhoneText")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null) nameTxt.text = "등록된 주요시설이 없습니다";
            if (phoneTxt != null) phoneTxt.text = "주변 데이터가 확인되면 표시됩니다";

            Button emptyButton = emptyItem.GetComponent<Button>();
            if (emptyButton != null)
            {
                emptyButton.interactable = false;
            }
            return;
        }

        foreach (var info in facilities)
        {
            GameObject item = Instantiate(facilityItemPrefab, facilityContainer);
            
            Vector3 pos = item.transform.localPosition;
            pos.z = 0;
            item.transform.localPosition = pos;
            StyleFacilityItem(item);

            TextMeshProUGUI nameTxt = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI phoneTxt = item.transform.Find("PhoneText")?.GetComponent<TextMeshProUGUI>();

            if (nameTxt != null) nameTxt.text = info.name;
            if (phoneTxt != null) phoneTxt.text = string.IsNullOrEmpty(info.phone) ? "" : info.phone;

            Button btn = item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => SelectFacility(info));
            }
        }
    }

    // 입점 상가 선택 시 정보 교체
    void SelectFacility(FacilityInfo facility)
    {
        if (_originalDetailData == null) return;

        BuildingData facilityData = new BuildingData();
        
        facilityData.buildingName = facility.name;
        facilityData.phoneNumber = facility.phone;
        facilityData.fetchedAddress = _originalDetailData.fetchedAddress;
        facilityData.zipCode = _originalDetailData.zipCode;  
        facilityData.facilities = _originalDetailData.facilities; 

        if (string.IsNullOrEmpty(facility.placeUrl))
            facilityData.placeUrl = _originalDetailData.placeUrl; 
        else
            facilityData.placeUrl = facility.placeUrl; 

        if (string.IsNullOrEmpty(facility.category))
            facilityData.description = "입점 시설";
        else
            facilityData.description = facility.category;

        _currentDetailData = facilityData;

        if (quickTitleText != null) quickTitleText.text = facilityData.buildingName;
        if (quickCategoryText != null) quickCategoryText.text = facilityData.description;

        if (detailTitle != null) detailTitle.text = facilityData.buildingName;
        if (detailCategory != null) detailCategory.text = facilityData.description;
        
        if (string.IsNullOrEmpty(facilityData.phoneNumber)) {
            if (detailPhone != null) detailPhone.text = "전화번호 없음";
            if (callPhoneButton != null) callPhoneButton.interactable = false;
        } else {
            if (detailPhone != null) detailPhone.text = facilityData.phoneNumber;
            if (callPhoneButton != null) callPhoneButton.interactable = true;
        }

        if (detailScrollView != null) detailScrollView.verticalNormalizedPosition = 1f;
        
        Debug.Log($"[시설 선택] {facility.name} 정보로 전환됨");
    }

    // 원본 건물 정보로 복구
    public void RestoreOriginalView()
    {
        if (_originalDetailData == null) return;

        _currentDetailData = _originalDetailData; 

        if (quickTitleText != null) quickTitleText.text = _originalDetailData.buildingName;
        if (quickCategoryText != null) quickCategoryText.text = _originalDetailData.description; 

        if (detailTitle != null) detailTitle.text = _originalDetailData.buildingName;
        if (detailCategory != null) detailCategory.text = string.IsNullOrEmpty(_originalDetailData.description) ? "상업 시설" : _originalDetailData.description;
        
        if (string.IsNullOrEmpty(_originalDetailData.phoneNumber)) {
            if (detailPhone != null) detailPhone.text = "전화번호 없음";
            if (callPhoneButton != null) callPhoneButton.interactable = false;
        } else {
            if (detailPhone != null) detailPhone.text = _originalDetailData.phoneNumber;
            if (callPhoneButton != null) callPhoneButton.interactable = true;
        }
        
        Debug.Log("원본 건물 정보로 복구되었습니다.");
    }
    #endregion

    #region Code Driven Detail Theme
    void ApplyCodeDrivenDetailTheme()
    {
        if (detailPanelRect == null) return;

        EnsureDetailSheetChrome();

        foreach (Image image in detailPanelRect.GetComponentsInChildren<Image>(true))
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        StylePanelSurface(detailPanelRect.GetComponent<Image>(), new Color(0.985f, 0.988f, 0.995f, 0.985f), new Color(0.86f, 0.89f, 0.94f, 1f));

        Image overlayImage = null;
        if (detailViewObject != null)
        {
            overlayImage = detailViewObject.GetComponent<Image>();
            if (overlayImage == null)
            {
                overlayImage = detailViewObject.AddComponent<Image>();
            }
        }
        if (overlayImage != null)
        {
            overlayImage.sprite = null;
            overlayImage.color = new Color(0.04f, 0.06f, 0.1f, 0.28f);
            overlayImage.raycastTarget = true;
        }

        if (detailScrollView != null)
        {
            Image scrollImage = detailScrollView.GetComponent<Image>();
            if (scrollImage != null)
            {
                scrollImage.color = new Color(0f, 0f, 0f, 0f);
                RemoveGraphicEffects(scrollImage.gameObject);
            }
        }

        RectTransform contentRect = detailScrollView != null ? detailScrollView.content : null;
        VerticalLayoutGroup contentLayout = contentRect != null ? contentRect.GetComponent<VerticalLayoutGroup>() : null;
        if (contentLayout != null)
        {
            contentLayout.padding.left = 24;
            contentLayout.padding.right = 24;
            contentLayout.padding.top = 28;
            contentLayout.padding.bottom = 28;
            contentLayout.spacing = 18;
        }

        StyleCard("LocationCard", new Color(1f, 1f, 1f, 1f), new Color(0.91f, 0.93f, 0.96f, 1f));
        StyleCard("PhoneCard", new Color(1f, 1f, 1f, 1f), new Color(0.91f, 0.93f, 0.96f, 1f));
        StyleCard("MapCard", new Color(1f, 0.978f, 0.975f, 1f), new Color(1f, 0.87f, 0.86f, 1f));
        StyleCard("FacilityCard", new Color(0.995f, 0.998f, 1f, 1f), new Color(0.91f, 0.93f, 0.96f, 1f));

        SetCardHeight("LocationCard", 136f);
        SetCardHeight("PhoneCard", 110f);
        SetCardHeight("MapCard", 92f);
        SetCardMinHeight("FacilityCard", 180f);

        StyleButton(closeDetailButton, new Color(0.12f, 0.14f, 0.18f, 1f), new Color(0.24f, 0.27f, 0.34f, 1f), "X", 13f);
        StyleButton(callPhoneButton, new Color(0.94f, 0.955f, 0.975f, 1f), new Color(0.84f, 0.87f, 0.92f, 1f), "전화 걸기", 14f);
        StyleButton(openMapButton, new Color(0.93f, 0.14f, 0.14f, 1f), new Color(1f, 0.4f, 0.4f, 1f), "카카오맵 열기", 15f);

        if (copyAddressButton != null) copyAddressButton.gameObject.SetActive(false);
        if (detailCategory != null) detailCategory.gameObject.SetActive(false);
        HideObject("CategoryCard");
        if (detailZipCode != null) detailZipCode.gameObject.SetActive(false);

        SetSectionText("LcationTitleText", "위치");
        SetSectionText("PhoneTitleText", "전화번호");
        SetSectionText("FacilityTtileText", "주요시설");
        SetSectionText("MapTitleText", "카카오맵 연동");

        SetTextStyle(detailTitle, 30f, new Color(0.08f, 0.1f, 0.14f, 1f), FontStyles.Bold);
        SetTextStyle(detailAddress, 16f, new Color(0.18f, 0.21f, 0.27f, 1f), FontStyles.Normal);
        SetTextStyle(detailPhone, 16f, new Color(0.18f, 0.21f, 0.27f, 1f), FontStyles.Normal);

        SetSectionStyle("LcationTitleText");
        SetSectionStyle("PhoneTitleText");
        SetSectionStyle("FacilityTtileText");
        SetSectionStyle("MapTitleText");

        HideDecorativeImage("BuildingIconCard");
        HideDecorativeImage("CameraIconCard");
        HideDecorativeImage("PhoneColorIcon");
        HideDecorativeImage("MapWhiteIcon");
        HideDecorativeImage("PhoneIcon");
        HideDecorativeImage("MapIcon");
    }

    void StyleFacilityItem(GameObject item)
    {
        if (item == null) return;

        Image rootImage = item.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.type = Image.Type.Simple;
            rootImage.color = new Color(0.985f, 0.989f, 0.996f, 1f);
            EnsureOutline(rootImage.gameObject, new Color(0.91f, 0.93f, 0.96f, 1f), new Vector2(1f, -1f));
            EnsureShadow(rootImage.gameObject, new Color(0f, 0f, 0f, 0.05f), new Vector2(0f, -3f));
        }

        Transform phoneIcon = item.transform.Find("PhoneIcon");
        if (phoneIcon != null) phoneIcon.gameObject.SetActive(false);

        LayoutElement layoutElement = item.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = item.AddComponent<LayoutElement>();
        layoutElement.minHeight = 68f;
        layoutElement.preferredHeight = 68f;

        TextMeshProUGUI nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI phoneText = item.transform.Find("PhoneText")?.GetComponent<TextMeshProUGUI>();
        SetTextStyle(nameText, 15f, new Color(0.08f, 0.1f, 0.14f, 1f), FontStyles.Bold);
        SetTextStyle(phoneText, 12f, new Color(0.46f, 0.5f, 0.57f, 1f), FontStyles.Normal);
    }

    void StyleCard(string objectName, Color fillColor, Color borderColor)
    {
        Transform target = FindDescendant(detailPanelRect, objectName);
        if (target == null) return;

        Image image = target.GetComponent<Image>();
        if (image == null) return;

        image.color = fillColor;
        EnsureOutline(image.gameObject, borderColor, new Vector2(1f, -1f));
        EnsureShadow(image.gameObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -6f));
    }

    void StylePanelSurface(Image image, Color fillColor, Color borderColor)
    {
        if (image == null) return;

        image.color = fillColor;
        EnsureOutline(image.gameObject, borderColor, new Vector2(1f, -1f));
        EnsureShadow(image.gameObject, new Color(0f, 0f, 0f, 0.14f), new Vector2(0f, -14f));
    }

    void StyleButton(Button button, Color fillColor, Color borderColor, string fallbackText, float labelSize)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fillColor;
            EnsureOutline(image.gameObject, borderColor, new Vector2(1f, -1f));
            EnsureShadow(image.gameObject, new Color(0f, 0f, 0f, 0.1f), new Vector2(0f, -4f));
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            Color textColor = fillColor.r > 0.8f && fillColor.g > 0.8f ? new Color(0.11f, 0.13f, 0.18f, 1f) : Color.white;
            label.text = string.IsNullOrEmpty(fallbackText) ? label.text : fallbackText;
            SetTextStyle(label, labelSize, textColor, FontStyles.Bold);
        }
        else if (!string.IsNullOrEmpty(fallbackText))
        {
            EnsureButtonFallbackText(button.transform, fallbackText, fillColor.r > 0.8f ? new Color(0.11f, 0.13f, 0.18f, 1f) : Color.white, labelSize);
        }
    }

    void EnsureButtonFallbackText(Transform parent, string textValue, Color color, float fontSize)
    {
        Transform existing = parent.Find("CodeLabel");
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;

        if (label == null)
        {
            GameObject textObject = new GameObject("CodeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        label.text = textValue;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
    }

    void HideDecorativeImage(string objectName)
    {
        Transform target = FindDescendant(detailPanelRect, objectName);
        if (target == null) return;

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
        }
    }

    void SetTextStyle(TextMeshProUGUI text, float fontSize, Color color, FontStyles style)
    {
        if (text == null) return;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.enableWordWrapping = true;
    }

    void SetSectionText(string objectName, string value)
    {
        TextMeshProUGUI text = FindDescendant(detailPanelRect, objectName)?.GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = value;
    }

    void SetSectionStyle(string objectName)
    {
        TextMeshProUGUI text = FindDescendant(detailPanelRect, objectName)?.GetComponent<TextMeshProUGUI>();
        if (text == null) return;
        text.fontSize = 13f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.5f, 0.55f, 0.62f, 1f);
        text.enableWordWrapping = false;
    }

    void HideObject(string objectName)
    {
        Transform target = FindDescendant(detailPanelRect, objectName);
        if (target != null)
        {
            target.gameObject.SetActive(false);
        }
    }

    void SetCardHeight(string objectName, float minHeight)
    {
        RectTransform rect = FindDescendant(detailPanelRect, objectName) as RectTransform;
        if (rect == null) return;

        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = minHeight;
        layout.preferredHeight = minHeight;
    }

    void SetCardMinHeight(string objectName, float minHeight)
    {
        RectTransform rect = FindDescendant(detailPanelRect, objectName) as RectTransform;
        if (rect == null) return;

        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = minHeight;
    }

    void EnsureDetailSheetChrome()
    {
        Transform existingHandle = detailPanelRect.Find("SheetHandle");
        Image handle = existingHandle != null ? existingHandle.GetComponent<Image>() : null;
        if (handle == null)
        {
            GameObject handleObject = new GameObject("SheetHandle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(detailPanelRect, false);
            RectTransform rect = handleObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(52f, 6f);
            handle = handleObject.GetComponent<Image>();
            handle.raycastTarget = false;
        }

        handle.sprite = null;
        handle.color = new Color(0.8f, 0.83f, 0.88f, 1f);
    }

    void ToggleScreenMarkerOverlay(bool visible)
    {
        if (_screenMarkerRoot == null) return;
        _screenMarkerRoot.gameObject.SetActive(visible);
    }

    Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName) return child;
        }
        return null;
    }

    void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        if (target == null) return;
        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = false;
    }

    void EnsureShadow(GameObject target, Color color, Vector2 distance)
    {
        if (target == null) return;
        Shadow shadow = null;
        foreach (Shadow effect in target.GetComponents<Shadow>())
        {
            if (effect != null && effect.GetType() == typeof(Shadow))
            {
                shadow = effect;
                break;
            }
        }
        if (shadow == null) shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    void RemoveGraphicEffects(GameObject target)
    {
        if (target == null) return;
        Outline outline = target.GetComponent<Outline>();
        if (outline != null) Destroy(outline);
        foreach (Shadow shadow in target.GetComponents<Shadow>())
        {
            if (shadow != null && shadow.GetType() == typeof(Shadow))
            {
                Destroy(shadow);
            }
        }
    }
    #endregion

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

    void OnCallPhone() { if (_currentDetailData != null && !string.IsNullOrEmpty(_currentDetailData.phoneNumber)) Application.OpenURL("tel:" + _currentDetailData.phoneNumber); }
    void OnOpenMap() { if (_currentDetailData != null && !string.IsNullOrEmpty(_currentDetailData.placeUrl)) Application.OpenURL(_currentDetailData.placeUrl); }
    #endregion

    #region Animations
    IEnumerator AnimateDetailPanel(float startY, float targetY, Action onComplete = null)
    {
        float time = 0f;
        float duration = 0.5f; 
        
        Vector2 pos = detailPanelRect.anchoredPosition;
        pos.y = startY;
        detailPanelRect.anchoredPosition = pos;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            t = t * t * (3f - 2f * t); 

            pos.y = Mathf.Lerp(startY, targetY, t);
            detailPanelRect.anchoredPosition = pos;
            yield return null;
        }
        
        pos.y = targetY;
        detailPanelRect.anchoredPosition = pos;
        onComplete?.Invoke();
    }

    void ShowCard(GameObject obj, RectTransform rect, CanvasGroup group, float targetY, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); obj.SetActive(true); routine = StartCoroutine(AnimateMove(rect, group, targetY, 1)); }
    void HideCard(GameObject obj, RectTransform rect, CanvasGroup group, ref Coroutine routine) { if (routine != null) StopCoroutine(routine); routine = StartCoroutine(AnimateMove(rect, group, slideOffset, 0, () => obj.SetActive(false))); }
    void InitializeCard(RectTransform rect, CanvasGroup group, bool visible, float targetY) { if (visible) { rect.anchoredPosition = new Vector2(0, targetY); group.alpha = 1; } else { rect.anchoredPosition = new Vector2(0, slideOffset); group.alpha = 0; } }
    
    IEnumerator AnimateMove(RectTransform rect, CanvasGroup group, float targetY, float targetAlpha, Action onComplete = null) { float startY = rect.anchoredPosition.y; float startAlpha = group.alpha; float time = 0; while (time < animDuration) { time += Time.deltaTime; float t = time / animDuration; t = t * t * (3f - 2f * t); Vector2 pos = rect.anchoredPosition; pos.y = Mathf.Lerp(startY, targetY, t); rect.anchoredPosition = pos; group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t); yield return null; } rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY); group.alpha = targetAlpha; onComplete?.Invoke(); }
    #endregion
}
