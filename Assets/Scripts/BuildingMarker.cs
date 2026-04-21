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
    public float previewScale = 0.36f;
    public float activeScale = 0.54f;
    public float animSpeed = 8.0f;
    public float labelHeight = 1.4f;
    public float previewDotSize = 3f;
    public float titleFontSize = 8.1f;
    public float subtitleFontSize = 4.2f;
    public float distanceFontSize = 4.3f;
    public Vector2 buttonPlateSize = new Vector2(11.2f, 5.6f);
    public Texture2D labelBackgroundTexture;
    public TMP_FontAsset fontAssetOverride;
    public Material fontMaterialOverride;
    public Color hiddenColor = new Color(1f, 1f, 1f, 0f);
    public Color previewColor = new Color(0.33f, 0.47f, 0.65f, 0.98f);
    public Color activeColor = new Color(0.92f, 0.95f, 0.98f, 1.0f);
    public Color titleTextColor = new Color(0.96f, 0.98f, 1f, 1f);
    public Color subtitleTextColor = new Color(0.68f, 0.75f, 0.85f, 1f);
    public Color distanceTextColor = new Color(0.52f, 0.84f, 0.97f, 1f);
    public Color panelColor = new Color(0.06f, 0.10f, 0.15f, 0.94f);
    public Color panelOutlineColor = new Color(0.18f, 0.26f, 0.38f, 0.92f);

    private Transform _visualRoot;
    private Transform _dotRoot;
    private TextMeshPro _titleText;
    private TextMeshPro _subtitleText;
    private TextMeshPro _distanceText;
    private Vector3 _targetScaleVec = Vector3.zero;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _distance = string.Empty;
    private bool _isInitialized;
    private bool _isInitializing;
    private MarkerVisualState _currentState = MarkerVisualState.Hidden;
    private bool _isInfoVisible;
    private Transform _cachedCameraTransform;
    private Renderer _buttonPlateRenderer;
    private Material _buttonPlateMaterial;
    private Renderer _dotRenderer;
    private Material _dotMaterial;
    private BoxCollider _hitCollider;
    private BuildingData _boundBuilding;
    private const float DefaultPreviewTextScale = 0.36f;
    private const float DefaultActiveTextScale = 0.54f;

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

        CreateDotVisual();
        CreateButtonPlate(_visualRoot);
        _titleText = CreateTextNode("Title", _visualRoot, new Vector3(0f, 1.08f, 0f), titleFontSize, FontStyles.Bold);
        _subtitleText = CreateTextNode("Subtitle", _visualRoot, new Vector3(0f, 0.18f, 0f), subtitleFontSize, FontStyles.Normal);
        _distanceText = CreateTextNode("Distance", _visualRoot, new Vector3(0f, -0.76f, 0f), distanceFontSize, FontStyles.Bold);
        EnsureHitCollider();
    }

    void CreateDotVisual()
    {
        GameObject dotObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotObject.name = "PreviewDot";
        _dotRoot = dotObject.transform;
        _dotRoot.SetParent(transform, false);
        _dotRoot.localPosition = new Vector3(0f, labelHeight, 0f);
        _dotRoot.localRotation = Quaternion.identity;
        _dotRoot.localScale = Vector3.one * previewDotSize;

        Collider dotCollider = dotObject.GetComponent<Collider>();
        if (dotCollider != null)
        {
            Destroy(dotCollider);
        }

        _dotRenderer = dotObject.GetComponent<Renderer>();
        if (_dotRenderer != null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                _dotMaterial = new Material(shader);
                _dotRenderer.material = _dotMaterial;
            }

            _dotRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _dotRenderer.receiveShadows = false;
            _dotRenderer.lightProbeUsage = LightProbeUsage.Off;
            _dotRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    void CreateButtonPlate(Transform parent)
    {
        GameObject plateObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plateObject.name = "ButtonPlate";
        plateObject.transform.SetParent(parent, false);
        plateObject.transform.localPosition = new Vector3(0f, 0.03f, 0.06f);
        plateObject.transform.localRotation = Quaternion.identity;
        plateObject.transform.localScale = new Vector3(buttonPlateSize.x, buttonPlateSize.y, 1f);

        Collider plateCollider = plateObject.GetComponent<Collider>();
        if (plateCollider != null)
        {
            Destroy(plateCollider);
        }

        _buttonPlateRenderer = plateObject.GetComponent<Renderer>();
        if (_buttonPlateRenderer != null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader != null)
            {
                _buttonPlateMaterial = new Material(shader);
                _buttonPlateMaterial.mainTexture = labelBackgroundTexture != null
                    ? labelBackgroundTexture
                    : Texture2D.whiteTexture;
                _buttonPlateRenderer.material = _buttonPlateMaterial;
                ApplyPanelMaterialColor(1f);
            }

            _buttonPlateRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _buttonPlateRenderer.receiveShadows = false;
            _buttonPlateRenderer.lightProbeUsage = LightProbeUsage.Off;
            _buttonPlateRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    void EnsureHitCollider()
    {
        _hitCollider = GetComponent<BoxCollider>();
        if (_hitCollider == null)
        {
            _hitCollider = gameObject.AddComponent<BoxCollider>();
        }

        _hitCollider.center = new Vector3(0f, labelHeight + 0.05f, 0.05f);
        _hitCollider.size = new Vector3(buttonPlateSize.x, buttonPlateSize.y, 0.4f);
        _hitCollider.enabled = false;
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
        tmp.rectTransform.sizeDelta = new Vector2(24f, 3.6f);
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

        if (_distanceText != null)
        {
            bool hasDistance = !string.IsNullOrWhiteSpace(_distance);
            _distanceText.text = hasDistance ? _distance : string.Empty;
            _distanceText.gameObject.SetActive(hasDistance);
        }
    }

    void ApplyColor(Color color)
    {
        if (_titleText != null)
        {
            _titleText.color = new Color(titleTextColor.r, titleTextColor.g, titleTextColor.b, Mathf.Clamp01(color.a));
        }

        if (_subtitleText != null)
        {
            _subtitleText.color = new Color(subtitleTextColor.r, subtitleTextColor.g, subtitleTextColor.b, Mathf.Clamp01(color.a * 0.96f));
        }

        if (_distanceText != null)
        {
            _distanceText.color = new Color(distanceTextColor.r, distanceTextColor.g, distanceTextColor.b, Mathf.Clamp01(color.a));
        }

        if (_buttonPlateMaterial != null)
        {
            ApplyPanelMaterialColor(Mathf.Clamp01(color.a));
        }
    }

    void ApplyPanelMaterialColor(float alphaMultiplier)
    {
        if (_buttonPlateMaterial == null)
        {
            return;
        }

        Color tint = new Color(
            panelColor.r,
            panelColor.g,
            panelColor.b,
            Mathf.Clamp01(panelColor.a * alphaMultiplier));

        _buttonPlateMaterial.color = tint;

        if (_buttonPlateMaterial.HasProperty("_Color"))
        {
            _buttonPlateMaterial.SetColor("_Color", tint);
        }

        if (_buttonPlateMaterial.HasProperty("_BaseColor"))
        {
            _buttonPlateMaterial.SetColor("_BaseColor", tint);
        }
    }

    void ApplyDotColor(Color color)
    {
        if (_dotMaterial != null)
        {
            _dotMaterial.color = color;
        }
    }

    void UpdateVisualState()
    {
        bool showSelectedText = _isInfoVisible && _currentState == MarkerVisualState.Selected;
        bool showPreviewDot = _isInfoVisible && _currentState == MarkerVisualState.Preview;

        if (_visualRoot != null)
        {
            _visualRoot.gameObject.SetActive(showSelectedText);
        }

        if (_dotRoot != null)
        {
            _dotRoot.gameObject.SetActive(showPreviewDot);
        }

        if (_hitCollider != null)
        {
            _hitCollider.enabled = showSelectedText;
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
        ApplyDotColor(targetColor);
        _currentState = state;
        UpdateVisualState();

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

        return configuredValue;
    }

    public void SetInfoContent(string title, string subtitle, string distance)
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
        _distance = distance;
        RefreshText();
    }

    public void BindBuilding(BuildingData building)
    {
        _boundBuilding = building;
    }

    public BuildingData GetBoundBuilding()
    {
        return _boundBuilding;
    }

    public bool IsSelectedTextVisible()
    {
        return _isInitialized &&
               _isInfoVisible &&
               _currentState == MarkerVisualState.Selected &&
               _visualRoot != null &&
               _visualRoot.gameObject.activeInHierarchy;
    }

    public Vector3 GetTextAnchorWorldPosition()
    {
        if (_visualRoot != null)
        {
            return _visualRoot.position;
        }

        return transform.position + (Vector3.up * labelHeight);
    }

    public float GetVisualHeightOffset()
    {
        return labelHeight;
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

        _isInfoVisible = visible;
        UpdateVisualState();
    }
}
