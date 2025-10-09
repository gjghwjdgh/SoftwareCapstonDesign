using UnityEngine;

public class PathDrawer : MonoBehaviour
{
    public LineRenderer lineRenderer3D;
    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;
    private Transform startPoint, target;

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
            lineRenderer3D.SetPosition(0, startPoint.position);
            lineRenderer3D.SetPosition(1, target.position);
        }
    }

    public void SetHighlight(bool highlighted)
    {
        lineRenderer3D.startColor = lineRenderer3D.endColor = highlighted ? highlightColor : normalColor;
    }
}