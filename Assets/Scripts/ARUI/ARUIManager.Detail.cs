using System.Collections;
using UnityEngine;

public partial class ARUIManager
{
    #region Detail View Control
    // 월드 선택 대상에 맞춰 상세 버튼의 활성 상태와 시각 스타일을 갱신한다.
    public void SetWorldInfoDetailButtonState(BuildingData data, bool active)
    {
        _worldInfoButtonData = active ? data : null;
        UpdateToolkitDetailButtonState(_worldInfoButtonData, active);

        if (worldInfoDetailButton == null)
        {
            return;
        }

        worldInfoDetailButton.interactable = active && data != null;

        if (_worldInfoDetailButtonImage != null)
        {
            _worldInfoDetailButtonImage.color = worldInfoDetailButton.interactable
                ? new Color(0.08f, 0.78f, 0.96f, 1f)
                : new Color(0.28f, 0.32f, 0.38f, 0.9f);
        }

        if (_worldInfoDetailButtonText != null)
        {
            _worldInfoDetailButtonText.color = worldInfoDetailButton.interactable
                ? Color.white
                : new Color(0.82f, 0.86f, 0.9f, 0.9f);
        }
    }

    // 상세 패널에 건물 데이터를 바인딩하고 열림 이벤트를 외부에 알린다.
    public void OpenDetailView(BuildingData data)
    {
        if (uiToolkitDetailPanel == null)
        {
            Debug.LogWarning("UI Toolkit detail panel is not assigned.");
            return;
        }

        _currentDetailData = data;
        SetBottomActionBarToolkitVisible(false);
        uiToolkitDetailPanel.Show(data);
        OnDetailOpened?.Invoke();
    }

    // 현재 표시 중인 상세 패널을 닫는다.
    public void CloseDetailView()
    {
        if (uiToolkitDetailPanel != null && uiToolkitDetailPanel.IsVisible)
        {
            uiToolkitDetailPanel.Hide();
        }
    }

    // UI Toolkit 패널의 닫힘 콜백을 일반 상세 닫힘 이벤트로 중계한다.
    void HandleUIToolkitDetailClosed()
    {
        SetBottomActionBarToolkitVisible(true);
        RefreshBottomActionBarToolkitLayout();
        OnDetailClosed?.Invoke();
    }
    #endregion

    #region UI Utilities
    // 이전 토스트를 중단하고 새 토스트 표시 코루틴을 시작한다.
    public void ShowToast(string message)
    {
        if (_toastRoutine != null) StopCoroutine(_toastRoutine);
        _toastRoutine = StartCoroutine(ToastProcess(message));
    }

    // 토스트를 페이드 인, 유지, 페이드 아웃 순서로 표시한다.
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

    // 현재 상세 정보의 전화번호로 시스템 전화 앱을 연다.
    void OnCallPhone()
    {
        if (_currentDetailData == null)
        {
            return;
        }

        string phoneNumber = uiToolkitDetailPanel != null
            ? uiToolkitDetailPanel.CurrentDisplayedPhoneNumber
            : _currentDetailData.phoneNumber;

        if (!string.IsNullOrEmpty(phoneNumber))
        {
            Application.OpenURL("tel:" + phoneNumber);
        }
    }

    // 현재 상세 정보의 지도 URL을 시스템 브라우저로 연다.
    void OnOpenMap()
    {
        if (_currentDetailData == null)
        {
            return;
        }

        string placeUrl = uiToolkitDetailPanel != null
            ? uiToolkitDetailPanel.CurrentDisplayedPlaceUrl
            : _currentDetailData.placeUrl;

        if (!string.IsNullOrEmpty(placeUrl))
        {
            Application.OpenURL(placeUrl);
        }
    }
    #endregion
}
