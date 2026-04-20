using UnityEngine;

public partial class ARUIManager
{
    #region Main Card State Control
    public void SetScanningMode()
    {
        if (currentState == UIState.Scanning) return;

        currentState = UIState.Scanning;
        ShowCard(scanningCard, _scanRect, _scanGroup, statusCardPosY, ref _scanRoutine);
        HideCard(detectedCard, _detectRect, _detectGroup, ref _detectRoutine);
    }

    public void SetDetectedMode()
    {
        if (currentState == UIState.Detected) return;

        currentState = UIState.Detected;
        HideCard(scanningCard, _scanRect, _scanGroup, ref _scanRoutine);
        ShowCard(detectedCard, _detectRect, _detectGroup, statusCardPosY, ref _detectRoutine);
    }

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
