using UnityEngine;
using System.Collections.Generic;

public class PathDrawer : MonoBehaviour
{
    [Header("���� ������")]
    public LineRenderer lineRenderer3D;
    public LineRenderer lineRenderer2D;

    [Header("���º� ����")]
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    private int pathResolution = 100;

    /// <summary>
    /// [������] ��θ� �׸��� ���� �� ���� ������ �޽��ϴ�.
    /// </summary>
    public void DrawPath(List<Vector3> pathPoints3D, Vector3 startPos3D, Vector3 endPos3D, int targetIndex, int totalTargets, float spreadFactor, Camera cam, float projectionDepth, int resolution)
    {
        this.pathResolution = resolution;

        // 1. 3D ������ ������ �����ϰ� 3D ��� �����ͷ� �׸��ϴ�.
        lineRenderer3D.positionCount = pathPoints3D.Count;
        lineRenderer3D.SetPositions(pathPoints3D.ToArray());

        // 2. 2D ������ 3D ��ο� ������, ȭ�� �������� ���� ����Ͽ� �׸��ϴ�.
        DrawStable2DLine(startPos3D, endPos3D, targetIndex, totalTargets, spreadFactor, cam, projectionDepth);

        SetHighlight(false);
    }

    /// <summary>
    /// [���� �߰���] ȭ�� �������� �������� 2D ������ ��� ����ϰ� �׸��ϴ�.
    /// </summary>
    private void DrawStable2DLine(Vector3 startPos3D, Vector3 endPos3D, int targetIndex, int totalTargets, float spreadFactor, Camera cam, float projectionDepth)
    {
        // 3D �������� ������ 2D ȭ�� ��ǥ�� ��ȯ�մϴ�.
        Vector2 p0 = cam.WorldToScreenPoint(startPos3D);
        Vector2 p3 = cam.WorldToScreenPoint(endPos3D);

        // 2D ȭ�� ��ǥ�迡�� ����� ���� ���͸� ����մϴ�.
        Vector2 directionVector = p3 - p0;
        Vector2 sideVector = new Vector2(-directionVector.y, directionVector.x).normalized;

        // ȭ�� �������� ��� �л� �������� ����մϴ�. (�������� ���ʹ� ȭ�� ũ�⿡ ���� ������ �ʿ��� �� �����Ƿ� 50 ������ ����)
        float distributionOffset = (targetIndex - (totalTargets - 1) / 2.0f) * (spreadFactor * 50f);
        Vector2 offset = sideVector * distributionOffset;

        // 2D ȭ�� ��ǥ�迡�� �������� ����մϴ�.
        Vector2 p1 = p0 + (directionVector * 0.25f) + offset;
        Vector2 p2 = p3 - (directionVector * 0.25f) + offset;

        // ���� 2D ��� ������ �����մϴ�.
        Vector3[] projectedPoints = new Vector3[pathResolution + 1];
        for (int i = 0; i <= pathResolution; i++)
        {
            float t = i / (float)pathResolution;
            Vector2 screenPoint2D = CalculateCubicBezierPoint(t, p0, p1, p2, p3);

            // ���������� 2D ȭ�� ��ǥ�� �ٽ� 3D ���� ��ǥ(ī�޶� ��)�� ��ȯ�Ͽ� �����մϴ�.
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

    // 2D Vector2�� ������ ��� �Լ�
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