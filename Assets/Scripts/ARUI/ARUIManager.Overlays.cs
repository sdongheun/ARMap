using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ARUIManager
{
    // 화면 중앙 조준점 UI를 필요 시 생성하고 최상단에 유지한다.
    void EnsureCenterReticle()
    {
        if (!showCenterReticle)
        {
            if (_centerReticleRoot != null)
            {
                _centerReticleRoot.gameObject.SetActive(false);
            }
            return;
        }

        if (_centerReticleRoot == null)
        {
            GameObject rootObject = new GameObject("CenterReticle", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            _centerReticleRoot = rootObject.GetComponent<RectTransform>();
            _centerReticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.pivot = new Vector2(0.5f, 0.5f);
            _centerReticleRoot.anchoredPosition = Vector2.zero;
            _centerReticleRoot.sizeDelta = Vector2.zero;

            CreateCenterReticleBar("TopBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, centerReticleGap), false);
            CreateCenterReticleBar("BottomBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -centerReticleGap), false);
            CreateCenterReticleBar("LeftBar", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-centerReticleGap, 0f), true);
            CreateCenterReticleBar("RightBar", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(centerReticleGap, 0f), true);
        }

        _centerReticleRoot.gameObject.SetActive(true);
        _centerReticleRoot.SetAsLastSibling();
    }

    // 스크롤 가능한 디버그 오버레이를 런타임에 생성한다.
    void EnsureDebugOverlay()
    {
        if (_debugOverlayRoot != null) return;

        GameObject rootObject = new GameObject("DebugOverlay", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        rootObject.transform.SetParent(transform, false);
        _debugOverlayRoot = rootObject.GetComponent<RectTransform>();
        _debugOverlayRoot.anchorMin = new Vector2(0f, 1f);
        _debugOverlayRoot.anchorMax = new Vector2(0f, 1f);
        _debugOverlayRoot.pivot = new Vector2(0f, 1f);
        _debugOverlayRoot.anchoredPosition = new Vector2(24f, -180f);
        _debugOverlayRoot.sizeDelta = new Vector2(560f, 220f);

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0.05f, 0.08f, 0.12f, 0.72f);
        background.raycastTarget = true;

        _debugOverlayScrollRect = rootObject.GetComponent<ScrollRect>();
        _debugOverlayScrollRect.horizontal = false;
        _debugOverlayScrollRect.vertical = true;
        _debugOverlayScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _debugOverlayScrollRect.scrollSensitivity = 30f;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(rootObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(18f, 18f);
        viewportRect.offsetMax = new Vector2(-18f, -18f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        _debugOverlayContent = contentObject.GetComponent<RectTransform>();
        _debugOverlayContent.anchorMin = new Vector2(0f, 1f);
        _debugOverlayContent.anchorMax = new Vector2(1f, 1f);
        _debugOverlayContent.pivot = new Vector2(0.5f, 1f);
        _debugOverlayContent.anchoredPosition = Vector2.zero;
        _debugOverlayContent.sizeDelta = Vector2.zero;

        GameObject textObject = new GameObject("DebugText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(contentObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, 0f);

        _debugOverlayText = textObject.GetComponent<TextMeshProUGUI>();
        _debugOverlayText.fontSize = 20f;
        _debugOverlayText.textWrappingMode = TextWrappingModes.Normal;
        _debugOverlayText.overflowMode = TextOverflowModes.Overflow;
        _debugOverlayText.color = new Color(0.92f, 0.97f, 1f, 1f);
        _debugOverlayText.alignment = TextAlignmentOptions.TopLeft;
        _debugOverlayText.raycastTarget = false;
        _debugOverlayText.text = string.Empty;

        ApplySharedTextStyle(_debugOverlayText);

        _debugOverlayScrollRect.viewport = viewportRect;
        _debugOverlayScrollRect.content = _debugOverlayContent;

        _debugOverlayRoot.gameObject.SetActive(false);
        _debugOverlayRoot.SetAsLastSibling();
    }

    // 디버그 메시지를 오버레이에 표시하고 최신 줄이 보이도록 스크롤을 맞춘다.
    public void SetDebugOverlay(string message)
    {
        EnsureDebugOverlay();

        if (_debugOverlayRoot == null || _debugOverlayText == null)
        {
            return;
        }

        bool visible = !string.IsNullOrWhiteSpace(message);
        _debugOverlayRoot.gameObject.SetActive(visible);
        if (visible)
        {
            _debugOverlayText.text = message;
            _debugOverlayText.ForceMeshUpdate();
            Vector2 preferredSize = _debugOverlayText.GetPreferredValues(
                message,
                Mathf.Max(0f, _debugOverlayRoot.sizeDelta.x - 36f),
                0f);

            if (_debugOverlayContent != null)
            {
                _debugOverlayContent.sizeDelta = new Vector2(0f, preferredSize.y);
            }

            if (_debugOverlayScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _debugOverlayScrollRect.verticalNormalizedPosition = 0f;
            }

            _debugOverlayRoot.SetAsLastSibling();
        }
    }

    // 디버그 오버레이를 숨기고 내부 표시 내용을 초기화한다.
    public void ClearDebugOverlay()
    {
        if (_debugOverlayRoot != null)
        {
            _debugOverlayRoot.gameObject.SetActive(false);
        }

        if (_debugOverlayText != null)
        {
            _debugOverlayText.text = string.Empty;
        }

        if (_debugOverlayContent != null)
        {
            _debugOverlayContent.sizeDelta = Vector2.zero;
        }
    }

    // 런타임 생성 TMP 텍스트가 공통 폰트와 머티리얼을 재사용하게 만든다.
    void ApplySharedTextStyle(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        if (SharedTMPFont != null)
        {
            text.font = SharedTMPFont;
        }

        if (SharedTMPMaterial != null)
        {
            text.fontSharedMaterial = SharedTMPMaterial;
        }
    }

    // 중앙 레티클을 구성하는 막대 하나를 생성한다.
    void CreateCenterReticleBar(string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, bool horizontal)
    {
        if (_centerReticleRoot == null)
        {
            return;
        }

        GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(_centerReticleRoot, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = anchor;
        barRect.anchorMax = anchor;
        barRect.pivot = pivot;
        barRect.anchoredPosition = anchoredPosition;
        barRect.sizeDelta = horizontal
            ? new Vector2(centerReticleBarLength, centerReticleBarThickness)
            : new Vector2(centerReticleBarThickness, centerReticleBarLength);

        Image barImage = barObject.GetComponent<Image>();
        barImage.color = centerReticleColor;
        barImage.raycastTarget = false;
    }

    // 중앙 레티클의 크기와 투명도를 시간에 따라 변화시켜 시각 효과를 준다.
    void UpdateCenterReticleAnimation()
    {
        if (_centerReticleRoot == null || !_centerReticleRoot.gameObject.activeSelf)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, centerReticlePulseDuration);
        float cycle = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / duration)) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(centerReticlePulseAlphaMin, centerReticlePulseAlphaMax, cycle);
        float scale = Mathf.Lerp(1f, centerReticlePulseScale, cycle);

        _centerReticleRoot.localScale = Vector3.one * scale;

        for (int i = 0; i < _centerReticleRoot.childCount; i++)
        {
            Image barImage = _centerReticleRoot.GetChild(i).GetComponent<Image>();
            if (barImage == null)
            {
                continue;
            }

            Color c = centerReticleColor;
            c.a *= alpha;
            barImage.color = c;
        }
    }
}
