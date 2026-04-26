using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IEnumerator = System.Collections.IEnumerator;

// 상단 상태 배지("주변을 바라보세요", "건물 3개 탐지됨" 등) 관련 기능을 분리한 ARUIManager의 partial 클래스다.
public partial class ARUIManager
{
    #region Main Card State Control
    // 스캔 대기 상태에서 상단 배지에 기본 안내 문구를 표시한다.
    public void SetScanningMode()
    {
        if (currentState == UIState.Scanning) return;

        currentState = UIState.Scanning;
        HideAllLegacyCards();
        SetStatusBadgeMessage("주변을 바라보세요");
    }

    // 화면에 후보 앵커는 있지만 중앙 선택이 없을 때 상단 배지에 안내 문구를 표시한다.
    public void SetDetectedMode()
    {
        if (currentState == UIState.Detected) return;

        currentState = UIState.Detected;
        HideAllLegacyCards();
        SetStatusBadgeMessage("건물을 바라보세요");
    }

    // 현재 위치 기준으로 가져온 건물 개수를 상단 배지에 표시한다.
    public void SetBuildingCountStatus(int buildingCount)
    {
        currentState = UIState.Detected;
        HideAllLegacyCards();
        SetStatusBadgeMessage($"건물 {Mathf.Max(0, buildingCount)}개 탐지됨");
    }

    // 데이터 새로고침 또는 재클러스터링 중임을 상단 배지에 표시한다.
    public void SetTrackingStabilizingMode()
    {
        currentState = UIState.Scanning;
        HideAllLegacyCards();
        SetStatusBadgeMessage("트래킹 안정화 중");
    }

    // 거리 정보 없이 퀵 인포 상태로 진입시키는 오버로드다.
    public void ShowQuickInfo(BuildingData data)
    {
        ShowQuickInfo(data, -1f);
    }

    // 선택된 건물 텍스트가 보일 때는 상태 배지를 숨긴다.
    public void ShowQuickInfo(BuildingData data, float distanceMeters)
    {
        if (data == null)
        {
            return;
        }

        bool enteringQuickInfo = currentState != UIState.QuickInfo;
        currentState = UIState.QuickInfo;

        if (enteringQuickInfo)
        {
            HideAllLegacyCards();
        }

        SetStatusBadgeMessage(null);
    }

    // 상단 중앙에 단일 상태 배지를 생성한다.
    void EnsureStatusBadge()
    {
        if (_statusBadgeRoot != null)
        {
            _statusBadgeRoot.gameObject.SetActive(true);
            RefreshStatusBadgeLayout();
            return;
        }

        GameObject rootObject = new GameObject("StatusBadge", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rootObject.transform.SetParent(transform, false);
        _statusBadgeRoot = rootObject.GetComponent<RectTransform>();
        _statusBadgeBackground = rootObject.GetComponent<Image>();
        _statusBadgeGroup = rootObject.GetComponent<CanvasGroup>();
        _statusBadgeBackground.color = new Color(0.06f, 0.09f, 0.14f, 0.78f);
        _statusBadgeBackground.raycastTarget = false;
        _statusBadgeGroup.alpha = 0f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rootObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 8f);
        textRect.offsetMax = new Vector2(-18f, -8f);

        _statusBadgeText = textObject.GetComponent<TextMeshProUGUI>();
        _statusBadgeText.fontSize = 20f;
        _statusBadgeText.alignment = TextAlignmentOptions.Center;
        _statusBadgeText.color = new Color(0.92f, 0.97f, 1f, 1f);
        _statusBadgeText.textWrappingMode = TextWrappingModes.NoWrap;
        _statusBadgeText.overflowMode = TextOverflowModes.Ellipsis;
        _statusBadgeText.raycastTarget = false;
        ApplySharedTextStyle(_statusBadgeText);

        RefreshStatusBadgeLayout();
        SetStatusBadgeHiddenImmediate();
        _statusBadgeRoot.SetAsLastSibling();
    }

    // 상태 배지 문구를 갱신한다.
    public void SetStatusBadgeMessage(string message)
    {
        EnsureStatusBadge();
        if (_statusBadgeRoot == null || _statusBadgeText == null)
        {
            return;
        }

        bool shouldShow = !string.IsNullOrWhiteSpace(message);
        _statusBadgeText.text = message ?? string.Empty;

        if (shouldShow)
        {
            ShowStatusBadge();
        }
        else
        {
            HideStatusBadge();
        }

        _statusBadgeRoot.SetAsLastSibling();
    }

    // 상태 배지를 화면 상단 중앙에 유지한다.
    void RefreshStatusBadgeLayout()
    {
        if (_statusBadgeRoot == null)
        {
            return;
        }

        bool isLandscapeLike = Screen.width > Screen.height;
        Vector2 appliedSize = isLandscapeLike
            ? new Vector2(statusBadgeSize.x * 0.4f, statusBadgeSize.y * 0.4f)
            : statusBadgeSize;
        Vector2 appliedOffset = isLandscapeLike
            ? new Vector2(0f, -24f)
            : statusBadgeTopOffset;

        _statusBadgeRoot.anchorMin = new Vector2(0.5f, 1f);
        _statusBadgeRoot.anchorMax = new Vector2(0.5f, 1f);
        _statusBadgeRoot.pivot = new Vector2(0.5f, 1f);
        _statusBadgeRoot.sizeDelta = appliedSize;

        if (_statusBadgeText != null)
        {
            _statusBadgeText.fontSize = isLandscapeLike ? 10f : 20f;
            _statusBadgeText.rectTransform.localScale = Vector3.one;
            _statusBadgeText.rectTransform.offsetMin = isLandscapeLike
                ? new Vector2(8f, 4f)
                : new Vector2(18f, 8f);
            _statusBadgeText.rectTransform.offsetMax = isLandscapeLike
                ? new Vector2(-8f, -4f)
                : new Vector2(-18f, -8f);
        }

        if (_statusBadgeRoot.gameObject.activeSelf && _statusBadgeGroup != null && _statusBadgeGroup.alpha > 0.001f)
        {
            _statusBadgeRoot.anchoredPosition = appliedOffset;
        }
    }

    // 기존 상단 카드 오브젝트는 숨겨두고 더 이상 상태 표시로 사용하지 않는다.
    void HideAllLegacyCards()
    {
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
    }

    // 상태 배지를 위에서 아래로 슬라이드 인시켜 화면 안으로 보여준다.
    void ShowStatusBadge()
    {
        if (_statusBadgeRoot == null || _statusBadgeGroup == null)
        {
            return;
        }

        if (_statusBadgeRoutine != null)
        {
            StopCoroutine(_statusBadgeRoutine);
        }

        _statusBadgeRoot.gameObject.SetActive(true);
        _statusBadgeRoutine = StartCoroutine(AnimateStatusBadge(GetVisibleStatusBadgePosition(), 1f, null));
    }

    // 상태 배지를 위로 슬라이드 아웃시켜 화면 밖으로 숨긴다.
    void HideStatusBadge()
    {
        if (_statusBadgeRoot == null || _statusBadgeGroup == null)
        {
            return;
        }

        if (_statusBadgeRoutine != null)
        {
            StopCoroutine(_statusBadgeRoutine);
        }

        _statusBadgeRoot.gameObject.SetActive(true);
        _statusBadgeRoutine = StartCoroutine(AnimateStatusBadge(GetHiddenStatusBadgePosition(), 0f, SetStatusBadgeHiddenImmediate));
    }

    // 배지를 현재 위치에서 목표 위치와 투명도로 보간한다.
    IEnumerator AnimateStatusBadge(Vector2 targetPosition, float targetAlpha, System.Action onComplete)
    {
        Vector2 startPosition = _statusBadgeRoot.anchoredPosition;
        float startAlpha = _statusBadgeGroup.alpha;
        float time = 0f;

        while (time < animDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, animDuration));
            t = t * t * (3f - 2f * t);
            _statusBadgeRoot.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            _statusBadgeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _statusBadgeRoot.anchoredPosition = targetPosition;
        _statusBadgeGroup.alpha = targetAlpha;
        _statusBadgeRoutine = null;
        onComplete?.Invoke();
    }

    // 배지의 숨김 위치를 계산한다.
    Vector2 GetHiddenStatusBadgePosition()
    {
        Vector2 visiblePosition = GetVisibleStatusBadgePosition();
        float appliedHeight = (Screen.width > Screen.height ? statusBadgeSize.y * 0.5f : statusBadgeSize.y);
        return visiblePosition + new Vector2(0f, -(appliedHeight + 20f));
    }

    // 현재 화면 방향에 맞는 배지 표시 위치를 계산한다.
    Vector2 GetVisibleStatusBadgePosition()
    {
        return Screen.width > Screen.height
            ? new Vector2(0f, -18f)
            : statusBadgeTopOffset;
    }

    // 배지를 즉시 화면 밖으로 옮기고 비활성화한다.
    void SetStatusBadgeHiddenImmediate()
    {
        if (_statusBadgeRoot == null)
        {
            return;
        }

        _statusBadgeRoot.anchoredPosition = GetHiddenStatusBadgePosition();
        if (_statusBadgeGroup != null)
        {
            _statusBadgeGroup.alpha = 0f;
        }
        _statusBadgeRoot.gameObject.SetActive(false);
    }
    #endregion
}
