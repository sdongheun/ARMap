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
    public Color previewColor = new Color(0.08f, 0.95f, 1.0f, 1.0f);
    public Color activeColor = new Color(1.0f, 0.25f, 0.1f, 1.0f);
    public Vector3 infoLabelOffset = new Vector3(0f, 1.5f, 0f);
    public Vector2 infoLabelCanvasSize = new Vector2(260f, 92f);
    public Vector2 infoLabelTextPadding = new Vector2(20f, 14f);

    private Transform _cameraTransform;
    private Vector3 _targetScaleVec;
    private SpriteRenderer[] _spriteRenderers;
    private Transform _infoLabelRoot;
    private RectTransform _infoLabelRect;
    private Image _infoBackground;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _subtitleText;

    void Start()
    {
        _cameraTransform = Camera.main.transform;
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
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
        }

        transform.localScale = Vector3.Lerp(transform.localScale, _targetScaleVec, Time.deltaTime * animSpeed);
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
        if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;

        foreach (SpriteRenderer spriteRenderer in _spriteRenderers)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }
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
            _infoLabelRoot.gameObject.SetActive(visible);
        }
    }

    void EnsureInfoLabel()
    {
        if (_infoLabelRoot != null)
        {
            return;
        }

        GameObject labelRoot = new GameObject("InfoLabelRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _infoLabelRoot = labelRoot.transform;
        _infoLabelRoot.SetParent(transform, false);
        _infoLabelRoot.localPosition = infoLabelOffset;
        _infoLabelRoot.localRotation = Quaternion.identity;
        _infoLabelRoot.localScale = Vector3.one * 0.01f;

        Canvas canvas = labelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

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
        _infoBackground.color = new Color(0.03f, 0.06f, 0.1f, 0.82f);

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
