using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class BuildingMarker : MonoBehaviour
{
    public enum MarkerVisualState
    {
        Hidden,
        Preview,
        Selected
    }

    [Header("Text Marker")]
    public float hiddenScale = 0.0f;
    public float previewScale = 0.18f;
    public float activeScale = 0.26f;
    public float animSpeed = 8.0f;
    public float labelHeight = 1.4f;
    public float titleFontSize = 4.2f;
    public float subtitleFontSize = 2.4f;
    public TMP_FontAsset fontAssetOverride;
    public Material fontMaterialOverride;
    public Color hiddenColor = new Color(1f, 1f, 1f, 0f);
    public Color previewColor = new Color(0.95f, 0.98f, 1f, 0.96f);
    public Color activeColor = new Color(1.0f, 0.92f, 0.25f, 1.0f);

    private Transform _visualRoot;
    private TextMeshPro _titleText;
    private TextMeshPro _subtitleText;
    private Vector3 _targetScaleVec = Vector3.zero;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private bool _isInitialized;
    private bool _isInitializing;
    private MarkerVisualState _currentState = MarkerVisualState.Hidden;
    private bool _isInfoVisible;
    private Transform _cachedCameraTransform;
    private const float DefaultPreviewTextScale = 0.18f;
    private const float DefaultActiveTextScale = 0.26f;
    private const float LegacyScaleThreshold = 0.6f;

    void Awake()
    {
        InitializeMarker();
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScaleVec, Time.deltaTime * animSpeed);

        if (_cachedCameraTransform == null && Camera.main != null)
        {
            _cachedCameraTransform = Camera.main.transform;
        }

        if (_visualRoot != null && _cachedCameraTransform != null)
        {
            _visualRoot.forward = _visualRoot.position - _cachedCameraTransform.position;
        }
    }

    void InitializeMarker()
    {
        if (_isInitialized || _isInitializing)
        {
            return;
        }

        _isInitializing = true;
        RemoveLegacyChildren();
        CreateTextVisual();
        RefreshText();
        _isInitialized = true;
        _isInitializing = false;
        SetState(MarkerVisualState.Hidden, true);
    }

    void RemoveLegacyChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    void CreateTextVisual()
    {
        GameObject rootObject = new GameObject("TextMarker");
        _visualRoot = rootObject.transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = new Vector3(0f, labelHeight, 0f);
        _visualRoot.localRotation = Quaternion.identity;
        _visualRoot.localScale = Vector3.one;

        _titleText = CreateTextNode("Title", _visualRoot, new Vector3(0f, 0.28f, 0f), titleFontSize, FontStyles.Bold);
        _subtitleText = CreateTextNode("Subtitle", _visualRoot, new Vector3(0f, -0.18f, 0f), subtitleFontSize, FontStyles.Normal);
    }

    TextMeshPro CreateTextNode(string objectName, Transform parent, Vector3 localPosition, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        tmp.rectTransform.sizeDelta = new Vector2(24f, 4f);
        ApplyFontSettings(tmp);
        return tmp;
    }

    void ApplyFontSettings(TextMeshPro tmp)
    {
        TMP_FontAsset fontAsset = fontAssetOverride;
        if (fontAsset == null && TMP_Settings.defaultFontAsset != null)
        {
            fontAsset = TMP_Settings.defaultFontAsset;
        }

        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }
        else
        {
            Debug.LogWarning("BuildingMarker: TMP default font asset is missing.");
        }

        Material fontMaterial = fontMaterialOverride;
        if (fontMaterial == null && tmp.font != null)
        {
            fontMaterial = tmp.font.material;
        }

        if (fontMaterial != null)
        {
            tmp.fontSharedMaterial = fontMaterial;
        }

        MeshRenderer textRenderer = tmp.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            textRenderer.lightProbeUsage = LightProbeUsage.Off;
            textRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    void RefreshText()
    {
        if (_titleText != null)
        {
            _titleText.text = string.IsNullOrWhiteSpace(_title) ? "이름 없음" : _title;
        }

        if (_subtitleText != null)
        {
            bool hasSubtitle = !string.IsNullOrWhiteSpace(_subtitle);
            _subtitleText.text = hasSubtitle ? _subtitle : string.Empty;
            _subtitleText.gameObject.SetActive(hasSubtitle);
        }
    }

    void ApplyColor(Color color)
    {
        bool visible = color.a > 0.01f;

        if (_visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(visible);
        }

        if (_titleText != null)
        {
            _titleText.color = color;
        }

        if (_subtitleText != null)
        {
            Color subtitleColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.9f));
            _subtitleText.color = subtitleColor;
        }
    }

    public void SetState(MarkerVisualState state, bool immediate = false)
    {
        if (!_isInitialized)
        {
            InitializeMarker();
            if (!_isInitialized)
            {
                return;
            }
        }

        if (!immediate && state == _currentState)
        {
            return;
        }

        float targetScale = hiddenScale;
        Color targetColor = hiddenColor;

        switch (state)
        {
            case MarkerVisualState.Preview:
                targetScale = ResolveScale(previewScale, DefaultPreviewTextScale);
                targetColor = previewColor;
                break;
            case MarkerVisualState.Selected:
                targetScale = ResolveScale(activeScale, DefaultActiveTextScale);
                targetColor = activeColor;
                break;
        }

        _targetScaleVec = Vector3.one * targetScale;
        ApplyColor(targetColor);
        _currentState = state;

        if (immediate)
        {
            transform.localScale = _targetScaleVec;
        }
    }

    float ResolveScale(float configuredValue, float fallbackValue)
    {
        if (configuredValue < 0f)
        {
            return fallbackValue;
        }

        if (configuredValue > LegacyScaleThreshold)
        {
            return fallbackValue;
        }

        return configuredValue;
    }

    public void SetInfoContent(string title, string subtitle)
    {
        if (!_isInitialized)
        {
            InitializeMarker();
            if (!_isInitialized)
            {
                return;
            }
        }

        _title = title;
        _subtitle = subtitle;
        RefreshText();
    }

    public void SetInfoWorldPosition(Vector3 worldPosition)
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.position = worldPosition;
    }

    public void SetInfoVisible(bool visible)
    {
        if (!_isInitialized)
        {
            InitializeMarker();
            if (!_isInitialized)
            {
                return;
            }
        }

        if (_isInfoVisible == visible)
        {
            return;
        }

        if (_visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(visible);
        }
        _isInfoVisible = visible;
    }
}
