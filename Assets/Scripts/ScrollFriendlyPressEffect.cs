using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ScrollRect 내부 아이템에 프레스 스케일 효과를 주되,
/// 드래그 이벤트는 부모 ScrollRect로 전파하여 스크롤을 방해하지 않는다.
///
/// EventTrigger와 달리 IBeginDragHandler / IDragHandler / IEndDragHandler를
/// 부모에게 위임하여 ScrollRect가 정상적으로 작동한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScrollFriendlyPressEffect : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IInitializePotentialDragHandler
{
    [SerializeField] private float pressedScale  = 0.94f;
    [SerializeField] private float animDuration  = 0.08f;
    [SerializeField] private float releaseDuration = 0.12f;

    private ScrollRect _parentScrollRect;
    private Coroutine  _scaleRoutine;
    private bool       _isDragging;

    void Awake()
    {
        // 가장 가까운 부모 ScrollRect를 캐시
        _parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    // ── 프레스 피드백 ──────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging = false;
        ScaleTo(pressedScale, animDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isDragging)
            ScaleTo(1f, releaseDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ScaleTo(1f, releaseDuration);
    }

    // ── 드래그 이벤트는 부모 ScrollRect에 위임 ────────────────────

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (_parentScrollRect != null)
            ExecuteEvents.Execute(_parentScrollRect.gameObject, eventData,
                ExecuteEvents.initializePotentialDrag);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        ScaleTo(1f, releaseDuration); // 드래그 시작 시 스케일 복원
        if (_parentScrollRect != null)
            ExecuteEvents.Execute(_parentScrollRect.gameObject, eventData,
                ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_parentScrollRect != null)
            ExecuteEvents.Execute(_parentScrollRect.gameObject, eventData,
                ExecuteEvents.dragHandler);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        if (_parentScrollRect != null)
            ExecuteEvents.Execute(_parentScrollRect.gameObject, eventData,
                ExecuteEvents.endDragHandler);
    }

    // ── 스케일 애니메이션 ─────────────────────────────────────────

    void ScaleTo(float target, float duration)
    {
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(ScaleCoroutine(target, duration));
    }

    IEnumerator ScaleCoroutine(float targetScale, float duration)
    {
        Vector3 from = transform.localScale;
        Vector3 to   = Vector3.one * targetScale;
        float   t    = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - p, 2f); // ease-out quad
            transform.localScale = Vector3.Lerp(from, to, e);
            yield return null;
        }
        transform.localScale = to;
    }
}
