using UnityEngine;

[RequireComponent(typeof(ARUIManager))]
public class QuickInfoPreview : MonoBehaviour
{
    public string previewBuildingName = "인제대학교 대학원";
    public string previewCategory = "키워드 검색";
    public float previewDistanceMeters = 130f;

    private ARUIManager _uiManager;

    void Start()
    {
        _uiManager = GetComponent<ARUIManager>();
        ApplyPreview();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_uiManager == null)
        {
            _uiManager = GetComponent<ARUIManager>();
        }

        ApplyPreview();
    }

    public void ApplyPreview()
    {
        if (_uiManager == null)
        {
            return;
        }

        BuildingData previewData = new BuildingData
        {
            buildingName = string.IsNullOrWhiteSpace(previewBuildingName) ? "건물 이름" : previewBuildingName,
            description = string.IsNullOrWhiteSpace(previewCategory) ? "카테고리" : previewCategory
        };

        _uiManager.ShowQuickInfo(previewData, previewDistanceMeters);
    }
}
