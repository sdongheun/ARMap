using System.Collections;
using UnityEngine;

public partial class ARUIManager
{
    #region Detail View Control
    public void SetWorldInfoDetailButtonState(BuildingData data, bool active)
    {
        EnsureWorldInfoDetailButton();

        _worldInfoButtonData = active ? data : null;

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

    public void OpenDetailView(BuildingData data)
    {
        if (uiToolkitDetailPanel == null)
        {
            Debug.LogWarning("UI Toolkit detail panel is not assigned.");
            return;
        }

        _currentDetailData = data;
        uiToolkitDetailPanel.Show(data);
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
        OnDetailClosed?.Invoke();
    }
    #endregion

    #region UI Utilities
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
