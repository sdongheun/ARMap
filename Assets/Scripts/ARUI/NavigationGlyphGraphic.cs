using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 길찾기 HUD에서 사용하는 방향/상태 아이콘을 벡터 형태로 그린다.
/// 폰트 글리프에 의존하지 않아 특수문자 누락 문제를 피할 수 있다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class NavigationGlyphGraphic : MaskableGraphic
{
    public enum GlyphKind
    {
        Straight,
        TurnLeft,
        TurnRight,
        SlightLeft,
        SlightRight,
        UTurn,
        Crosswalk,
        Stairs,
        Elevator,
        Ramp,
        Bridge,
        Underpass,
        Arrival
    }

    [SerializeField] private GlyphKind glyphKind = GlyphKind.Straight;
    [SerializeField, Range(0.06f, 0.24f)] private float normalizedThickness = 0.12f;
    [SerializeField, Range(8, 32)] private int circleSegments = 18;

    public GlyphKind Kind => glyphKind;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetGlyph(GlyphKind newGlyphKind)
    {
        if (glyphKind == newGlyphKind)
        {
            return;
        }

        glyphKind = newGlyphKind;
        SetVerticesDirty();
    }

    public void SetThickness(float value)
    {
        float clamped = Mathf.Clamp(value, 0.06f, 0.24f);
        if (Mathf.Approximately(normalizedThickness, clamped))
        {
            return;
        }

        normalizedThickness = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        Color32 tint = color;

        switch (glyphKind)
        {
            case GlyphKind.TurnLeft:
                DrawTurnLeft(vh, rect, tint);
                break;
            case GlyphKind.TurnRight:
                DrawTurnRight(vh, rect, tint);
                break;
            case GlyphKind.SlightLeft:
                DrawSlightLeft(vh, rect, tint);
                break;
            case GlyphKind.SlightRight:
                DrawSlightRight(vh, rect, tint);
                break;
            case GlyphKind.UTurn:
                DrawUTurn(vh, rect, tint);
                break;
            case GlyphKind.Crosswalk:
                DrawCrosswalk(vh, rect, tint);
                break;
            case GlyphKind.Stairs:
                DrawStairs(vh, rect, tint);
                break;
            case GlyphKind.Elevator:
                DrawElevator(vh, rect, tint);
                break;
            case GlyphKind.Ramp:
                DrawRamp(vh, rect, tint);
                break;
            case GlyphKind.Bridge:
                DrawBridge(vh, rect, tint);
                break;
            case GlyphKind.Underpass:
                DrawUnderpass(vh, rect, tint);
                break;
            case GlyphKind.Arrival:
                DrawArrival(vh, rect, tint);
                break;
            default:
                DrawStraight(vh, rect, tint);
                break;
        }
    }

    void DrawStraight(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.78f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.78f), new Vector2(0.34f, 0.6f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.78f), new Vector2(0.66f, 0.6f), normalizedThickness, tint);
    }

    void DrawTurnLeft(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.68f, 0.18f), new Vector2(0.68f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.68f, 0.56f), new Vector2(0.28f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.28f, 0.56f), new Vector2(0.46f, 0.72f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.28f, 0.56f), new Vector2(0.46f, 0.4f), normalizedThickness, tint);
    }

    void DrawTurnRight(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.32f, 0.18f), new Vector2(0.32f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.32f, 0.56f), new Vector2(0.72f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.72f, 0.56f), new Vector2(0.54f, 0.72f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.72f, 0.56f), new Vector2(0.54f, 0.4f), normalizedThickness, tint);
    }

    void DrawSlightLeft(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.64f, 0.18f), new Vector2(0.36f, 0.78f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.36f, 0.78f), new Vector2(0.28f, 0.58f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.36f, 0.78f), new Vector2(0.52f, 0.66f), normalizedThickness, tint);
    }

    void DrawSlightRight(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.36f, 0.18f), new Vector2(0.64f, 0.78f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.64f, 0.78f), new Vector2(0.48f, 0.66f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.64f, 0.78f), new Vector2(0.72f, 0.58f), normalizedThickness, tint);
    }

    void DrawUTurn(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.58f, 0.18f), new Vector2(0.58f, 0.66f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.58f, 0.66f), new Vector2(0.28f, 0.66f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.28f, 0.66f), new Vector2(0.28f, 0.36f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.28f, 0.36f), new Vector2(0.14f, 0.5f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.28f, 0.36f), new Vector2(0.42f, 0.5f), normalizedThickness, tint);
    }

    void DrawCrosswalk(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddRect(vh, rect, 0.18f, 0.2f, 0.28f, 0.8f, tint);
        AddRect(vh, rect, 0.36f, 0.2f, 0.46f, 0.8f, tint);
        AddRect(vh, rect, 0.54f, 0.2f, 0.64f, 0.8f, tint);
        AddRect(vh, rect, 0.72f, 0.2f, 0.82f, 0.8f, tint);
    }

    void DrawStairs(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.18f, 0.22f), new Vector2(0.18f, 0.78f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.18f, 0.22f), new Vector2(0.42f, 0.22f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.42f, 0.22f), new Vector2(0.42f, 0.48f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.42f, 0.48f), new Vector2(0.66f, 0.48f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.66f, 0.48f), new Vector2(0.66f, 0.72f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.66f, 0.72f), new Vector2(0.84f, 0.72f), normalizedThickness, tint);
    }

    void DrawElevator(VertexHelper vh, Rect rect, Color32 tint)
    {
        float outline = normalizedThickness * 0.75f;
        AddRect(vh, rect, 0.22f, 0.72f, 0.78f, 0.8f, tint);
        AddRect(vh, rect, 0.22f, 0.2f, 0.78f, 0.28f, tint);
        AddRect(vh, rect, 0.22f, 0.28f, 0.3f, 0.72f, tint);
        AddRect(vh, rect, 0.7f, 0.28f, 0.78f, 0.72f, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.66f), outline, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.72f), new Vector2(0.4f, 0.58f), outline, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.72f), new Vector2(0.6f, 0.58f), outline, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.28f), new Vector2(0.4f, 0.42f), outline, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.28f), new Vector2(0.6f, 0.42f), outline, tint);
    }

    void DrawRamp(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.18f, 0.22f), new Vector2(0.82f, 0.22f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.3f, 0.22f), new Vector2(0.68f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.68f, 0.56f), new Vector2(0.52f, 0.56f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.68f, 0.56f), new Vector2(0.64f, 0.4f), normalizedThickness, tint);
    }

    void DrawBridge(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.16f, 0.36f), new Vector2(0.34f, 0.58f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.34f, 0.58f), new Vector2(0.5f, 0.72f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.5f, 0.72f), new Vector2(0.66f, 0.58f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.66f, 0.58f), new Vector2(0.84f, 0.36f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.2f, 0.46f), new Vector2(0.8f, 0.46f), normalizedThickness * 0.8f, tint);
    }

    void DrawUnderpass(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddLine(vh, rect, new Vector2(0.16f, 0.66f), new Vector2(0.84f, 0.66f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.22f, 0.66f), new Vector2(0.4f, 0.3f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.4f, 0.3f), new Vector2(0.58f, 0.66f), normalizedThickness, tint);
        AddLine(vh, rect, new Vector2(0.58f, 0.66f), new Vector2(0.76f, 0.3f), normalizedThickness, tint);
    }

    void DrawArrival(VertexHelper vh, Rect rect, Color32 tint)
    {
        AddCircle(vh, rect, new Vector2(0.5f, 0.5f), 0.18f, circleSegments, tint);
    }

    void AddRect(VertexHelper vh, Rect rect, float xMin, float yMin, float xMax, float yMax, Color32 tint)
    {
        Vector2 a = ToPoint(rect, new Vector2(xMin, yMin));
        Vector2 b = ToPoint(rect, new Vector2(xMin, yMax));
        Vector2 c = ToPoint(rect, new Vector2(xMax, yMax));
        Vector2 d = ToPoint(rect, new Vector2(xMax, yMin));

        int startIndex = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero);
        vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero);
        vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    void AddLine(VertexHelper vh, Rect rect, Vector2 start01, Vector2 end01, float thickness01, Color32 tint)
    {
        Vector2 start = ToPoint(rect, start01);
        Vector2 end = ToPoint(rect, end01);
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float halfThickness = Mathf.Min(rect.width, rect.height) * thickness01 * 0.5f;
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * halfThickness;

        int startIndex = vh.currentVertCount;
        vh.AddVert(start - normal, tint, Vector2.zero);
        vh.AddVert(start + normal, tint, Vector2.zero);
        vh.AddVert(end + normal, tint, Vector2.zero);
        vh.AddVert(end - normal, tint, Vector2.zero);
        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    void AddCircle(VertexHelper vh, Rect rect, Vector2 center01, float radius01, int segments, Color32 tint)
    {
        Vector2 center = ToPoint(rect, center01);
        float radius = Mathf.Min(rect.width, rect.height) * radius01;
        int startIndex = vh.currentVertCount;

        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(point, tint, Vector2.zero);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
        }
    }

    Vector2 ToPoint(Rect rect, Vector2 normalizedPoint)
    {
        return new Vector2(
            rect.xMin + rect.width * normalizedPoint.x,
            rect.yMin + rect.height * normalizedPoint.y
        );
    }
}
