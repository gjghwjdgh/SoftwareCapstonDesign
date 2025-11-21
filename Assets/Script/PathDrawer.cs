using UnityEngine;
using System.Collections.Generic;

public class PathDrawer : MonoBehaviour
{
    public LineRenderer lineRenderer3D;
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    // ★★★ 외부에서 강제로 지정하는 색상 (그룹 색상 등) ★★★
    private Color? overrideColor = null;

    public void InitializeCurve(List<Vector3> pathPoints)
    {
        lineRenderer3D.positionCount = pathPoints.Count;
        lineRenderer3D.SetPositions(pathPoints.ToArray());
        lineRenderer3D.widthMultiplier = 0.05f;
        SetHighlight(false);
    }

    // ★★★ 이 함수가 없어서 오류가 났습니다. 추가해주세요! ★★★
    public void SetColor(Color? color)
    {
        overrideColor = color;
        UpdateColor();
    }

    public void SetHighlight(bool highlighted)
    {
        if (highlighted)
        {
            // 하이라이트 될 때는 무조건 형광색(Cyan)
            lineRenderer3D.startColor = lineRenderer3D.endColor = highlightColor;
        }
        else
        {
            // 평소에는 지정된 그룹 색상 또는 회색
            UpdateColor();
        }
    }

    private void UpdateColor()
    {
        // overrideColor가 있으면(그룹 색상) 그걸 쓰고, 없으면 기본 회색(normalColor) 사용
        Color finalColor = overrideColor.HasValue ? overrideColor.Value : normalColor;
        lineRenderer3D.startColor = lineRenderer3D.endColor = finalColor;
    }
}