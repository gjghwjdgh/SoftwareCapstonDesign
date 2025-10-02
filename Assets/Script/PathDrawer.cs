using UnityEngine;
using System.Collections.Generic;

public class PathDrawer : MonoBehaviour
{
    [Header("라인 렌더러")]
    public LineRenderer lineRenderer3D;
    public LineRenderer lineRenderer2D;

    [Header("상태별 색상")]
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    private int pathResolution = 100;

    /// <summary>
    /// [수정됨] 경로를 그리기 위해 더 많은 정보를 받습니다.
    /// </summary>
    public void DrawPath(List<Vector3> pathPoints3D, Vector3 startPos3D, Vector3 endPos3D, int targetIndex, int totalTargets, float spreadFactor, Camera cam, float projectionDepth, int resolution)
    {
        this.pathResolution = resolution;

        // 1. 3D 라인은 이전과 동일하게 3D 경로 데이터로 그립니다.
        lineRenderer3D.positionCount = pathPoints3D.Count;
        lineRenderer3D.SetPositions(pathPoints3D.ToArray());

        // 2. 2D 라인은 3D 경로와 별개로, 화면 기준으로 새로 계산하여 그립니다.
        DrawStable2DLine(startPos3D, endPos3D, targetIndex, totalTargets, spreadFactor, cam, projectionDepth);

        SetHighlight(false);
    }

    /// <summary>
    /// [새로 추가됨] 화면 기준으로 안정적인 2D 베지어 곡선을 계산하고 그립니다.
    /// </summary>
    private void DrawStable2DLine(Vector3 startPos3D, Vector3 endPos3D, int targetIndex, int totalTargets, float spreadFactor, Camera cam, float projectionDepth)
    {
        // 3D 시작점과 끝점을 2D 화면 좌표로 변환합니다.
        Vector2 p0 = cam.WorldToScreenPoint(startPos3D);
        Vector2 p3 = cam.WorldToScreenPoint(endPos3D);

        // 2D 화면 좌표계에서 방향과 수직 벡터를 계산합니다.
        Vector2 directionVector = p3 - p0;
        Vector2 sideVector = new Vector2(-directionVector.y, directionVector.x).normalized;

        // 화면 기준으로 경로 분산 오프셋을 계산합니다. (스프레드 팩터는 화면 크기에 따라 조정이 필요할 수 있으므로 50 정도로 나눔)
        float distributionOffset = (targetIndex - (totalTargets - 1) / 2.0f) * (spreadFactor * 50f);
        Vector2 offset = sideVector * distributionOffset;

        // 2D 화면 좌표계에서 제어점을 계산합니다.
        Vector2 p1 = p0 + (directionVector * 0.25f) + offset;
        Vector2 p2 = p3 - (directionVector * 0.25f) + offset;

        // 계산된 2D 곡선의 점들을 생성합니다.
        Vector3[] projectedPoints = new Vector3[pathResolution + 1];
        for (int i = 0; i <= pathResolution; i++)
        {
            float t = i / (float)pathResolution;
            Vector2 screenPoint2D = CalculateCubicBezierPoint(t, p0, p1, p2, p3);

            // 최종적으로 2D 화면 좌표를 다시 3D 월드 좌표(카메라 앞)로 변환하여 저장합니다.
            projectedPoints[i] = cam.ScreenToWorldPoint(new Vector3(screenPoint2D.x, screenPoint2D.y, projectionDepth));
        }

        lineRenderer2D.positionCount = projectedPoints.Length;
        lineRenderer2D.SetPositions(projectedPoints);
    }

    public void SetViewMode(bool is3D)
    {
        lineRenderer3D.gameObject.SetActive(is3D);
        lineRenderer2D.gameObject.SetActive(!is3D);
    }

    public void SetHighlight(bool highlighted)
    {
        Color color = highlighted ? highlightColor : normalColor;
        lineRenderer3D.startColor = lineRenderer3D.endColor = color;
        lineRenderer2D.startColor = lineRenderer2D.endColor = color;
    }

    // 2D Vector2용 베지어 계산 함수
    private Vector2 CalculateCubicBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector2 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;
        return p;
    }
}