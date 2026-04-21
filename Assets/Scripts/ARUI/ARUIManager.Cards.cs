using UnityEngine;

public partial class ARUIManager
{
    #region Main Card State Control
    // 스캔 대기 상태 카드만 보이도록 전환한다.
    public void SetScanningMode()
    {
        if (currentState == UIState.Scanning) return;

        currentState = UIState.Scanning;
        ShowCard(scanningCard, _scanRect, _scanGroup, statusCardPosY, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
    }

    // 건물 감지 상태 카드만 보이도록 전환한다.
    public void SetDetectedMode()
    {
        if (currentState == UIState.Detected) return;

        currentState = UIState.Detected;
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        ShowCard(detectedCard, _detectRect, _detectGroup, statusCardPosY, ref _detectRoutine);
    }

    // 거리 정보 없이 퀵 인포 상태로 진입시키는 오버로드다.
    public void ShowQuickInfo(BuildingData data)
    {
        ShowQuickInfo(data, -1f);
    }

    // 상태 카드를 숨기고 퀵 인포 표시 상태로 전환한다.
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
            HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
            HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
        }
    }
    #endregion
}
