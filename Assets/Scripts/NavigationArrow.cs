using UnityEngine;

/// <summary>
/// AR 네비게이션 화살표 프리팹에 부착하는 컴포넌트.
/// 바닥에 평평하게 놓이며, 부드러운 위치/회전 전환과 페이드 인/아웃을 지원한다.
/// </summary>
public class NavigationArrow : MonoBehaviour
{
    [Header("스무딩 설정")]
    public float positionLerpSpeed = 5.0f;  // 위치 보간 속도
    public float rotationLerpSpeed = 8.0f;  // 회전 보간 속도 (빌보드 반응성)
    public float fadeDuration = 0.3f;       // 페이드 시간

    [Header("애니메이션 설정")]
    public float pulseSpeed = 2.0f;         // 펄스 주파수 (rad/s)
    public float pulseAmount = 0.08f;       // 펄스 진폭 (스케일 비율)

    [Header("코너 강조 설정")]
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public Color cornerColor = new Color(1f, 0.7f, 0.1f, 1f);  // 황금색
    public Color nextTurnColor = new Color(1f, 0.45f, 0.05f, 1f); // 주황색

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _isActive;
    private float _currentAlpha;
    private float _targetAlpha;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private bool _initialized;

    // 강조 레벨 (0: 약함, 1: 강함)
    private float _emphasisLevel = 0.5f;
    private float _targetEmphasis = 0.5f;
    private Vector3 _baseScale;
    private float _pulsePhaseOffset;

    // 코너/다음 턴 모드
    private bool _isCornerMode;
    private bool _isNextTurnMode;
    private float _modeScaleMultiplier = 1f;
    private float _modePulseSpeedMul = 1f;
    private float _modePulseAmountMul = 1f;
    private Color _currentTint = Color.white;

    [Header("빌보드 설정")]
    public bool autoTiltBillboard = true;       // 자동 틸트 빌보드 활성화
    public float meshBackHalfLength = 0.5f;     // 메시 뒷쪽 절반 길이 (Z=-0.5 ~ 0)

    private Transform _cameraTransform;
    private Vector3 _currentBasePos;            // yLift를 포함하지 않은 Lerp 기준 위치

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _propBlock = new MaterialPropertyBlock();
        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
        _currentAlpha = 0f;
        _targetAlpha = 0f;
        _baseScale = transform.localScale;
        _pulsePhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        _currentTint = normalColor;
        SetAlpha(0f);
    }

    void Update()
    {
        if (!_initialized) return;

        // 위치 스무딩 (베이스 위치만 Lerp, yLift는 표시 단계에서 적용)
        _currentBasePos = Vector3.Lerp(_currentBasePos, _targetPosition, Time.deltaTime * positionLerpSpeed);
        Vector3 smoothedPos = _currentBasePos;

        // 카메라 참조 지연 캐싱 (BuildingMarker 패턴 참조)
        if (_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;

        Quaternion targetRotationFinal;
        float tiltFactor = 0f;

        if (autoTiltBillboard && _cameraTransform != null)
        {
            // 진행 방향(Y-yaw만 유지)
            Vector3 travelEuler = _targetRotation.eulerAngles;
            Quaternion travelYaw = Quaternion.Euler(0f, travelEuler.y, 0f);
            Vector3 travelDir = travelYaw * Vector3.forward;

            // 카메라 방향을 travelDir에 수직인 평면에 투영
            Vector3 toCamera = _cameraTransform.position - smoothedPos;
            Vector3 projected = toCamera - Vector3.Dot(toCamera, travelDir) * travelDir;

            if (projected.sqrMagnitude < 0.0001f)
                projected = Vector3.up;  // 퇴화 케이스: 카메라가 travel 축 위
            else
                projected.Normalize();

            targetRotationFinal = Quaternion.LookRotation(travelDir, projected);

            // 틸트 계수: 0(플랫) ~ 1(수직)
            tiltFactor = 1f - Mathf.Clamp01(Vector3.Dot(projected, Vector3.up));
        }
        else
        {
            // 폴백: 기존 Y축만 회전
            Vector3 euler = _targetRotation.eulerAngles;
            targetRotationFinal = Quaternion.Euler(0f, euler.y, 0f);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotationFinal, Time.deltaTime * rotationLerpSpeed);

        // 틸트 시 지면 클리핑 방지용 Y 오프셋
        float currentScaleZ = (_baseScale.z > 0.001f ? _baseScale.z : 1f)
                              * _modeScaleMultiplier
                              * Mathf.Lerp(0.7f, 1.15f, _emphasisLevel);
        float yLift = meshBackHalfLength * currentScaleZ * tiltFactor;
        transform.position = smoothedPos + Vector3.up * yLift;

        // 강조 레벨 보간
        _emphasisLevel = Mathf.MoveTowards(_emphasisLevel, _targetEmphasis, Time.deltaTime * 2f);

        // 펄스 + 강조 + 모드(코너/다음 턴) 기반 스케일
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * _modePulseSpeedMul + _pulsePhaseOffset)
                              * pulseAmount * _modePulseAmountMul * _emphasisLevel;
        float emphasisScale = Mathf.Lerp(0.7f, 1.15f, _emphasisLevel);
        transform.localScale = _baseScale * pulse * emphasisScale * _modeScaleMultiplier;

        // 알파 페이드 (강조 레벨 반영)
        float displayAlpha = _currentAlpha * Mathf.Lerp(0.45f, 1f, _emphasisLevel);
        if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            float fadeSpeed = 1.0f / Mathf.Max(fadeDuration, 0.01f);
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, Time.deltaTime * fadeSpeed);

            if (_currentAlpha <= 0.01f && _targetAlpha <= 0f)
            {
                gameObject.SetActive(false);
            }
        }
        SetAlpha(displayAlpha);
    }

    /// <summary>
    /// 화살표 강조 레벨 설정 (0~1). 가까운/다음 화살표는 높게, 먼 화살표는 낮게.
    /// </summary>
    public void SetEmphasis(float level)
    {
        _targetEmphasis = Mathf.Clamp01(level);
    }

    /// <summary>
    /// 코너(회전) 지점 화살표 모드. 황금색, 펄스 증폭, 약간 큰 스케일.
    /// </summary>
    public void SetCornerMode(bool isCorner)
    {
        _isCornerMode = isCorner;
        ApplyModeState();
    }

    /// <summary>
    /// 다음에 사용자가 수행할 턴 위치의 화살표 모드. 주황색, 가장 큰 강조.
    /// </summary>
    public void SetNextTurnMode(bool isNextTurn)
    {
        _isNextTurnMode = isNextTurn;
        ApplyModeState();
    }

    void ApplyModeState()
    {
        if (_isNextTurnMode)
        {
            _currentTint = nextTurnColor;
            _modeScaleMultiplier = 1.35f;
            _modePulseSpeedMul = 2.2f;
            _modePulseAmountMul = 2.5f;
        }
        else if (_isCornerMode)
        {
            _currentTint = cornerColor;
            _modeScaleMultiplier = 1.15f;
            _modePulseSpeedMul = 1.8f;
            _modePulseAmountMul = 2.0f;
        }
        else
        {
            _currentTint = normalColor;
            _modeScaleMultiplier = 1f;
            _modePulseSpeedMul = 1f;
            _modePulseAmountMul = 1f;
        }
    }

    /// <summary>
    /// 화살표의 목표 위치와 회전을 설정한다.
    /// </summary>
    public void SetTarget(Vector3 position, Quaternion rotation)
    {
        _targetPosition = position;
        // Y축 회전만 유지
        Vector3 euler = rotation.eulerAngles;
        _targetRotation = Quaternion.Euler(0f, euler.y, 0f);

        if (!_initialized)
        {
            // 최초 설정 시 즉시 적용 (Lerp 없이)
            transform.position = position;
            transform.rotation = _targetRotation;
            _currentBasePos = position;
            _initialized = true;
        }
    }

    /// <summary>
    /// 화살표 목표를 즉시 적용한다 (Lerp 건너뜀).
    /// 재활용 시 먼 슬롯으로 이동할 때 호출하여 헤엄치는 시각을 방지한다.
    /// </summary>
    public void SetTargetImmediate(Vector3 position, Quaternion rotation)
    {
        _targetPosition = position;
        Vector3 euler = rotation.eulerAngles;
        _targetRotation = Quaternion.Euler(0f, euler.y, 0f);
        transform.position = position;
        transform.rotation = _targetRotation;
        _currentBasePos = position;
        _initialized = true;
    }

    /// <summary>
    /// 화살표를 페이드 인하여 활성화한다.
    /// </summary>
    public void Activate()
    {
        _isActive = true;
        _targetAlpha = 1f;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 화살표를 페이드 아웃하여 비활성화한다.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _targetAlpha = 0f;
        _initialized = false;
    }

    /// <summary>
    /// 즉시 비활성화 (풀 반환 시 사용)
    /// </summary>
    public void DeactivateImmediate()
    {
        _isActive = false;
        _targetAlpha = 0f;
        _currentAlpha = 0f;
        _initialized = false;
        _emphasisLevel = 0.5f;
        _targetEmphasis = 0.5f;
        _isCornerMode = false;
        _isNextTurnMode = false;
        ApplyModeState();
        if (_baseScale != Vector3.zero) transform.localScale = _baseScale;
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    public bool IsActive => _isActive;

    void SetAlpha(float alpha)
    {
        if (_renderers == null) return;

        Color tinted = new Color(_currentTint.r, _currentTint.g, _currentTint.b, alpha);

        foreach (Renderer rend in _renderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", tinted);
            rend.SetPropertyBlock(_propBlock);

            // 머티리얼 직접 접근 (URP Unlit 셰이더 호환)
            foreach (Material mat in rend.materials)
            {
                if (mat != null)
                {
                    Color color = tinted;
                    mat.color = color;
                }
            }
        }
    }
}
