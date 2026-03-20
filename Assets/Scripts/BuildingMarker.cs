using UnityEngine;
using System.Collections;

public class BuildingMarker : MonoBehaviour
{
    [Header("Settings")]
    public float defaultScale = 0.5f; // 평소 크기 
    public float activeScale = 0.8f;  // 인식됐을 때 크기
    public float animSpeed = 5.0f;    // 커지는 속도

    private Transform _cameraTransform;
    private Vector3 _targetScaleVec;

    void Start()
    {
        _cameraTransform = Camera.main.transform;
        // 시작할 때는 기본 크기로 설정
        transform.localScale = Vector3.one * defaultScale;
        _targetScaleVec = Vector3.one * defaultScale;
    }

    void Update()
    {
        // 1. 항상 카메라를 바라보게 함 (Billboard)
        if (_cameraTransform != null)
        {
            transform.LookAt(transform.position + _cameraTransform.rotation * Vector3.forward,
                             _cameraTransform.rotation * Vector3.up);
        }

        // 2. 부드러운 크기 변화 (Lerp)
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScaleVec, Time.deltaTime * animSpeed);
    }

    // 외부에서 호출할 함수 (애니메이션)
    public void SetSelected(bool isSelected)
    {
        if (isSelected)
        {
            _targetScaleVec = Vector3.one * activeScale;
        }
        else
        {
            _targetScaleVec = Vector3.one * defaultScale;
        }
    }
}
