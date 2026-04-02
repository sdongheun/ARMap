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
        public Vector2 screenPosition;
        public bool isSelected;
    }

    private class ScreenMarkerView
    {
        public GameObject root;
        public RectTransform rectTransform;
        public Image pinShadowImage;
        public RectTransform pinShadowRect;
        public Image pinImage;
        public RectTransform pinRect;
        public Image labelBackground;
        public TextMeshProUGUI titleText;
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
    private readonly Dictionary<string, ScreenMarkerView> _screenMarkerViews = new Dictionary<string, ScreenMarkerView>();
    private string _lastQuickInfoId;

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
        SetPrimaryButtonsVisible(false);
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
        SetPrimaryButtonsVisible(true);

        if (enteringQuickInfo)
        {
            HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
            HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
            ShowQuickInfoCard(true);
        }
        else if (hasChanged)
        {
            ShowQuickInfoCard(true);
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

            view.labelBackground.gameObject.SetActive(true);
            view.titleText.gameObject.SetActive(true);
            view.labelBackground.color = data.isSelected
                ? new Color(0.07f, 0.08f, 0.12f, 0.9f)
                : new Color(0.07f, 0.08f, 0.12f, 0.72f);
            view.titleText.color = data.isSelected
                ? new Color(1f, 0.96f, 0.94f, 1f)
                : new Color(0.98f, 0.98f, 0.99f, 0.96f);
            view.titleText.text = data.label;
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
        rootRect.sizeDelta = new Vector2(220f, 112f);

        Image pinShadowImage = CreateScreenMarkerImage("PinShadow", rootObject.transform, 5f, -3f, 70f, 70f);
        Image pinImage = CreateScreenMarkerImage("Pin", rootObject.transform, 0f, 0f, 60f, 60f);
        if (pinShadowImage != null)
        {
            pinShadowImage.color = new Color(0f, 0f, 0f, 0.2f);
        }
        if (pinImage != null)
        {
            pinImage.color = new Color(1f, 1f, 1f, 0.96f);
        }

        Image labelBackground = CreateScreenMarkerBackground(rootObject.transform, 0f, -54f, 170f, 26f);
        TextMeshProUGUI titleText = CreateScreenMarkerText("Title", labelBackground.transform, 0f, 0f, 150f, 22f, TextAlignmentOptions.Center);
        titleText.fontSize = 13f;
        titleText.color = Color.white;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        labelBackground.gameObject.SetActive(true);
        titleText.gameObject.SetActive(true);

        ScreenMarkerView view = new ScreenMarkerView
        {
            root = rootObject,
            rectTransform = rootRect,
            pinShadowImage = pinShadowImage,
            pinShadowRect = pinShadowImage != null ? pinShadowImage.rectTransform : null,
            pinImage = pinImage,
            pinRect = pinImage != null ? pinImage.rectTransform : null,
            labelBackground = labelBackground,
            titleText = titleText
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

    Vector2 ClampToCanvas(Vector2 screenPosition)
    {
        Vector2 localPoint;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, uiCamera, out localPoint);

        const float paddingX = 110f;
        const float paddingY = 110f;
        float halfWidth = _canvasRect.rect.width * 0.5f;
        float halfHeight = _canvasRect.rect.height * 0.5f;

        localPoint.x = Mathf.Clamp(localPoint.x, -halfWidth + paddingX, halfWidth - paddingX);
        localPoint.y = Mathf.Clamp(localPoint.y, -halfHeight + 82f, halfHeight - paddingY);
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
