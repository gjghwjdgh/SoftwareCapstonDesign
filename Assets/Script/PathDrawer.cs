using UnityEngine;
using System.Collections.Generic;

public class PathDrawer : MonoBehaviour
{
    public LineRenderer lineRenderer3D;
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;
    private Transform startPoint, target;

    public void InitializeLine(Transform start, Transform target)
    {
        this.startPoint = start;
        this.target = target;
        lineRenderer3D.positionCount = 2;
        lineRenderer3D.widthMultiplier = 0.05f;
        SetHighlight(false);
    }

    public void InitializeCurve(List<Vector3> pathPoints)
    {
        this.startPoint = null; // Update에서 자동 갱신되지 않도록 null로 설정
        this.target = null;
        lineRenderer3D.positionCount = pathPoints.Count;
        lineRenderer3D.SetPositions(pathPoints.ToArray());
        lineRenderer3D.widthMultiplier = 0.05f;
        SetHighlight(false);
    }

    void Update()
    {
        // startPoint가 할당된 '직선' 모드일 때만 매 프레임 위치를 강제로 다시 잡아줍니다.
        if (startPoint != null && target != null)
        {
            lineRenderer3D.SetPosition(0, startPoint.position);
            lineRenderer3D.SetPosition(1, target.position);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        lineRenderer3D.startColor = lineRenderer3D.endColor = highlighted ? highlightColor : normalColor;
    }
}