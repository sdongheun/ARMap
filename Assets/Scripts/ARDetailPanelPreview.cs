using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
[RequireComponent(typeof(ARDetailPanelDocumentController))]
public class ARDetailPanelPreview : MonoBehaviour
{
    public bool hideLegacyCanvasInEditor = true;
    public string previewBuildingName = "CU 인제대정문점";
    public string previewCategory = "편의점";
    [TextArea(2, 4)]
    public string previewAddress = "경남 김해시 인제로 262";
    public string previewPhoneNumber = "055-321-1234";
    public string previewZipCode = "우편번호 50834";
    public string previewPlaceUrl = "https://place.map.kakao.com/";

    private ARDetailPanelDocumentController _controller;
    private UIDocument _document;
    private BuildingData _previewData;
    private Canvas[] _hiddenCanvases;

    void OnEnable()
    {
        ApplyPreview();
    }

    void OnValidate()
    {
        ApplyPreview();
    }

    public void ApplyPreviewFromEditor()
    {
        ApplyPreview();
    }

    void OnDisable()
    {
        RestoreLegacyCanvas();
    }

    private void ApplyPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (_controller == null)
        {
            _controller = GetComponent<ARDetailPanelDocumentController>();
        }

        if (_document == null)
        {
            _document = GetComponent<UIDocument>();
        }

        if (_controller == null)
        {
            return;
        }

        if (_document != null)
        {
            _document.sortingOrder = 1000;
        }

        if (hideLegacyCanvasInEditor)
        {
            HideLegacyCanvas();
        }

        _previewData ??= new BuildingData();
        _previewData.buildingName = previewBuildingName;
        _previewData.description = previewCategory;
        _previewData.fetchedAddress = previewAddress;
        _previewData.phoneNumber = previewPhoneNumber;
        _previewData.zipCode = previewZipCode;
        _previewData.placeUrl = previewPlaceUrl;

        _controller.ShowPreview(_previewData);
    }

    private void HideLegacyCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        _hiddenCanvases = canvases;
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
            {
                continue;
            }

            if (canvas.GetComponentInParent<ARDetailPanelPreview>() != null)
            {
                continue;
            }

            canvas.enabled = false;
        }
    }

    private void RestoreLegacyCanvas()
    {
        if (_hiddenCanvases == null)
        {
            return;
        }

        foreach (Canvas canvas in _hiddenCanvases)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }
    }
}
