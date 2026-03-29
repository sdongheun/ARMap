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
        public TextMeshProUGUI pinHeadText;
        public TextMeshProUGUI pinTailText;
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
            view.pinHeadText.text = "●";
            view.pinTailText.text = "▼";
            view.pinHeadText.fontSize = data.isSelected ? 56 : 44;
            view.pinTailText.fontSize = data.isSelected ? 46 : 36;

            Color markerColor = data.isSelected ? new Color(1.0f, 0.35f, 0.16f, 1f) : new Color(0.05f, 0.78f, 0.96f, 1f);
            view.pinHeadText.color = markerColor;
            view.pinTailText.color = markerColor;

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

        TextMeshProUGUI headText = CreateScreenMarkerText("PinHead", rootObject.transform, 0f, 8f, 72f, 72f, TextAlignmentOptions.Center);
        TextMeshProUGUI tailText = CreateScreenMarkerText("PinTail", rootObject.transform, 0f, -18f, 72f, 48f, TextAlignmentOptions.Center);

        Image labelBackground = CreateScreenMarkerBackground(rootObject.transform, 0f, -54f, 240f, 58f);
        TextMeshProUGUI titleText = CreateScreenMarkerText("Title", labelBackground.transform, 0f, 10f, 208f, 24f, TextAlignmentOptions.Center);
        TextMeshProUGUI subtitleText = CreateScreenMarkerText("Subtitle", labelBackground.transform, 0f, -12f, 208f, 20f, TextAlignmentOptions.Center);
        titleText.fontSize = 22f;
        subtitleText.fontSize = 16f;
        titleText.color = Color.white;
        subtitleText.color = new Color(0.86f, 0.93f, 1f, 0.95f);
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
            pinHeadText = headText,
            pinTailText = tailText,
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
        foreach (var info in facilities)
        {
            GameObject item = Instantiate(facilityItemPrefab, facilityContainer);
            
            Vector3 pos = item.transform.localPosition;
            pos.z = 0;
            item.transform.localPosition = pos;

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
