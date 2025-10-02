using UnityEngine;

public class PathDrawer : MonoBehaviour
{
    public LineRenderer lineRenderer3D;
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    private Transform startPoint;
    private Transform target;

    public void Initialize(Transform start, Transform target)
    {
        this.startPoint = start;
        this.target = target;
        lineRenderer3D.positionCount = 2;
        lineRenderer3D.widthMultiplier = 0.05f;
        SetHighlight(false);
    }

    void Update()
    {
        if (startPoint != null && target != null)
        {
            // 타겟이 보일 때만 선을 그림 (2D와 동일한 조건 적용)
            Vector3 viewportPoint = Camera.main.WorldToViewportPoint(target.position);
            bool isTargetVisible = viewportPoint.z > 0 && viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1;
            lineRenderer3D.enabled = isTargetVisible;

            if(isTargetVisible)
            {
                lineRenderer3D.SetPosition(0, startPoint.position);
                lineRenderer3D.SetPosition(1, target.position);
            }
        }
    }

    public void SetHighlight(bool highlighted)
    {
        lineRenderer3D.startColor = lineRenderer3D.endColor = highlighted ? highlightColor : normalColor;
    }
}