using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingMarker : MonoBehaviour
{
    public enum MarkerVisualState
    {
        Hidden,
        Preview,
        Selected
    }

    [Header("Settings")]
    public float hiddenScale = 0.0f;  // 숨김 상태
    public float previewScale = 1.1f; // 주변 앵커 기본 표시 크기
    public float activeScale = 1.8f;  // 선택된 앵커 강조 크기
    public float animSpeed = 5.0f;    // 커지는 속도
    public Color hiddenColor = new Color(1f, 1f, 1f, 0f);
    public Color previewColor = new Color(1.0f, 0.18f, 0.14f, 1.0f);
    public Color activeColor = new Color(1.0f, 0.08f, 0.08f, 1.0f);
    public Vector3 infoLabelOffset = new Vector3(0f, 1.5f, 0f);
    public Vector2 infoLabelCanvasSize = new Vector2(260f, 92f);
    public Vector2 infoLabelTextPadding = new Vector2(20f, 14f);

    private Transform _cameraTransform;
    private Vector3 _targetScaleVec;
    private SpriteRenderer[] _spriteRenderers;
    private Transform _markerVisualRoot;
    private CanvasGroup _markerCanvasGroup;
    private TextMeshProUGUI _pinShadowCircleText;
    private TextMeshProUGUI _pinShadowTailText;
    private TextMeshProUGUI _pinGlowCircleText;
    private TextMeshProUGUI _pinGlowTailText;
    private TextMeshProUGUI _pinBodyCircleText;
    private TextMeshProUGUI _pinBodyTailText;
    private TextMeshProUGUI _pinHoleText;
    private TextMeshProUGUI _pinHighlightText;
    private Transform _infoLabelRoot;
    private RectTransform _infoLabelRect;
    private CanvasGroup _infoCanvasGroup;
    private Image _infoBackground;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _subtitleText;
    private bool _infoVisibleTarget;
    private float _infoVisibilityLerp;

    void Start()
    {
        _cameraTransform = Camera.main.transform;
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        EnsureMarkerVisual();
        EnsureInfoLabel();
        SetState(MarkerVisualState.Hidden, true);
        SetInfoVisible(false);
    }

    void Update()
    {
        if (_cameraTransform != null)
        {
            transform.LookAt(transform.position + _cameraTransform.rotation * Vector3.forward,
                             _cameraTransform.rotation * Vector3.up);

            if (_infoLabelRoot != null)
            {
                _infoLabelRoot.LookAt(_infoLabelRoot.position + _cameraTransform.rotation * Vector3.forward,
                                      _cameraTransform.rotation * Vector3.up);
            }

            if (_markerVisualRoot != null)
            {
                _markerVisualRoot.LookAt(_markerVisualRoot.position + _cameraTransform.rotation * Vector3.forward,
                                         _cameraTransform.rotation * Vector3.up);
            }
        }

        transform.localScale = Vector3.Lerp(transform.localScale, _targetScaleVec, Time.deltaTime * animSpeed);
        UpdateInfoLabelVisual();
    }

    public void SetState(MarkerVisualState state, bool immediate = false)
    {
        float targetScale = hiddenScale;
        Color targetColor = hiddenColor;

        switch (state)
        {
            case MarkerVisualState.Preview:
                targetScale = previewScale;
                targetColor = previewColor;
                break;
            case MarkerVisualState.Selected:
                targetScale = activeScale;
                targetColor = activeColor;
                break;
        }

        _targetScaleVec = Vector3.one * targetScale;
        ApplyColor(targetColor);

        if (immediate)
        {
            transform.localScale = _targetScaleVec;
        }
    }

    void ApplyColor(Color color)
    {
        if (_markerCanvasGroup != null)
        {
            bool visible = color.a > 0.01f;
            _markerCanvasGroup.alpha = color.a;
            if (_markerVisualRoot != null)
            {
                _markerVisualRoot.gameObject.SetActive(visible);
            }

            Color accentColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.42f));
            if (_pinGlowCircleText != null) _pinGlowCircleText.color = accentColor;
            if (_pinGlowTailText != null) _pinGlowTailText.color = accentColor;
        }

        if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;

        foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }
    }

    void EnsureMarkerVisual()
    {
        if (_markerVisualRoot != null)
        {
            return;
        }

        GameObject markerRoot = new GameObject("MarkerVisualRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _markerVisualRoot = markerRoot.transform;
        _markerVisualRoot.SetParent(transform, false);
        _markerVisualRoot.localPosition = Vector3.zero;
        _markerVisualRoot.localRotation = Quaternion.identity;
        _markerVisualRoot.localScale = Vector3.one * 0.01f;

        Canvas canvas = markerRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 450;

        _markerCanvasGroup = markerRoot.GetComponent<CanvasGroup>();
        _markerCanvasGroup.alpha = 1f;

        RectTransform rootRect = markerRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(180f, 220f);

        _pinShadowCircleText = CreateMarkerText("PinShadowCircle", markerRoot.transform, 7f, -12f, 120f, 120f, 82f, new Color(0f, 0f, 0f, 0.26f));
        _pinShadowTailText = CreateMarkerText("PinShadowTail", markerRoot.transform, 7f, -56f, 92f, 72f, 62f, new Color(0f, 0f, 0f, 0.24f));
        _pinGlowCircleText = CreateMarkerText("PinGlowCircle", markerRoot.transform, 0f, -6f, 128f, 128f, 88f, new Color(1f, 0.16f, 0.12f, 0.42f));
        _pinGlowTailText = CreateMarkerText("PinGlowTail", markerRoot.transform, 0f, -50f, 96f, 76f, 66f, new Color(1f, 0.16f, 0.12f, 0.42f));
        _pinBodyCircleText = CreateMarkerText("PinBodyCircle", markerRoot.transform, 0f, -8f, 118f, 118f, 82f, new Color(0.06f, 0.07f, 0.1f, 1f));
        _pinBodyTailText = CreateMarkerText("PinBodyTail", markerRoot.transform, 0f, -52f, 90f, 72f, 62f, new Color(0.06f, 0.07f, 0.1f, 1f));
        _pinHoleText = CreateMarkerText("PinHole", markerRoot.transform, 0f, 4f, 56f, 56f, 34f, Color.white);
        _pinHighlightText = CreateMarkerText("PinHighlight", markerRoot.transform, -18f, 20f, 28f, 28f, 18f, new Color(1f, 1f, 1f, 0.2f));

        _pinShadowCircleText.text = "●";
        _pinShadowTailText.text = "▼";
        _pinGlowCircleText.text = "●";
        _pinGlowTailText.text = "▼";
        _pinBodyCircleText.text = "●";
        _pinBodyTailText.text = "▼";
        _pinHoleText.text = "●";
        _pinHighlightText.text = "●";
    }

    TextMeshProUGUI CreateMarkerText(string objectName, Transform parent, float posX, float posY, float width, float height, float fontSize, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(posX, posY);
        rect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        return text;
    }

    public void SetInfoContent(string title, string subtitle)
    {
        EnsureInfoLabel();

        if (_titleText != null)
        {
            _titleText.text = string.IsNullOrWhiteSpace(title) ? "장소 정보" : title;
        }

        if (_subtitleText != null)
        {
            _subtitleText.text = subtitle ?? string.Empty;
            _subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
        }
    }

    public void SetInfoWorldPosition(Vector3 worldPosition)
    {
        EnsureInfoLabel();

        if (_infoLabelRoot != null)
        {
            _infoLabelRoot.position = worldPosition + infoLabelOffset;
        }
    }

    public void SetInfoVisible(bool visible)
    {
        EnsureInfoLabel();

        if (_infoLabelRoot != null)
        {
            _infoVisibleTarget = visible;
            if (visible)
            {
                _infoLabelRoot.gameObject.SetActive(true);
            }
        }
    }

    void UpdateInfoLabelVisual()
    {
        if (_infoLabelRoot == null || _infoCanvasGroup == null)
        {
            return;
        }

        float targetVisibility = _infoVisibleTarget ? 1f : 0f;
        _infoVisibilityLerp = Mathf.Lerp(_infoVisibilityLerp, targetVisibility, Time.deltaTime * animSpeed);
        _infoCanvasGroup.alpha = _infoVisibilityLerp;

        float scale = Mathf.Lerp(0.72f, 1f, _infoVisibilityLerp) * 0.01f;
        _infoLabelRoot.localScale = Vector3.one * scale;

        if (!_infoVisibleTarget && _infoVisibilityLerp <= 0.02f)
        {
            _infoLabelRoot.gameObject.SetActive(false);
        }
    }

    void EnsureInfoLabel()
    {
        if (_infoLabelRoot != null)
        {
            return;
        }

        GameObject labelRoot = new GameObject("InfoLabelRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _infoLabelRoot = labelRoot.transform;
        _infoLabelRoot.SetParent(transform, false);
        _infoLabelRoot.localPosition = infoLabelOffset;
        _infoLabelRoot.localRotation = Quaternion.identity;
        _infoLabelRoot.localScale = Vector3.one * 0.0072f;

        Canvas canvas = labelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;
        _infoCanvasGroup = labelRoot.GetComponent<CanvasGroup>();
        _infoCanvasGroup.alpha = 0f;
        _infoVisibilityLerp = 0f;

        _infoLabelRect = labelRoot.GetComponent<RectTransform>();
        _infoLabelRect.sizeDelta = infoLabelCanvasSize;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(_infoLabelRoot, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        _infoBackground = backgroundObject.GetComponent<Image>();
        _infoBackground.color = new Color(0.03f, 0.06f, 0.1f, 0.9f);

        _titleText = CreateText("Title", backgroundObject.transform, -18f, 30f, 1.0f, 26f, FontStyles.Bold);
        _subtitleText = CreateText("Subtitle", backgroundObject.transform, -48f, 24f, 0.82f, 18f, FontStyles.Normal);
    }

    TextMeshProUGUI CreateText(string objectName, Transform parent, float anchoredPosY, float height, float alpha, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(infoLabelTextPadding.x, 0f);
        rect.offsetMax = new Vector2(-infoLabelTextPadding.x, 0f);
        rect.anchoredPosition = new Vector2(0f, anchoredPosY);
        rect.sizeDelta = new Vector2(0f, height);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = new Color(1f, 1f, 1f, alpha);

        return text;
    }
}
