using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class UILineConnector : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Camera mainCamera;

    // 경로 데이터를 저장하기 위한 변수
    private Transform startPoint3D;
    private Transform target3D;
    private List<Vector3> worldCurvePath; // 곡선 원본 경로(월드 좌표)

    public Color normalColor = Color.gray;
    public Color highlightColor = Color.cyan;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.sortingOrder = 1;
    }

    public void InitializeLine(Transform start, Transform target, Camera camera)
    {
        this.startPoint3D = start;
        this.target3D = target;
        this.mainCamera = camera;
        this.worldCurvePath = null;
        lineRenderer.positionCount = 2;
        SetHighlight(false);
    }

    public void InitializeCurve(List<Vector3> worldPath, Camera camera)
    {
        this.worldCurvePath = worldPath;
        this.mainCamera = camera;
        this.startPoint3D = null;
        this.target3D = null;
        lineRenderer.positionCount = worldPath.Count;
        SetHighlight(false);
    }

    void Update()
    {
        if (mainCamera == null) return;

        // 곡선 모드일 경우
        if (worldCurvePath != null && worldCurvePath.Any())
        {
            // 매 프레임마다 월드 경로를 UI 로컬 경로로 새로 변환하여 다시 그립니다.
            var localPath = worldCurvePath.Select(p => WorldToCanvasLocal(p)).ToArray();
            lineRenderer.SetPositions(localPath);
        }
        // 직선 모드일 경우
        else if (startPoint3D != null && target3D != null)
        {
            // 매 프레임마다 시작점과 끝점의 위치를 새로 계산하여 다시 그립니다.
            lineRenderer.SetPosition(0, WorldToCanvasLocal(startPoint3D.position));
            lineRenderer.SetPosition(1, WorldToCanvasLocal(target3D.position));
        }
    }

    private Vector3 WorldToCanvasLocal(Vector3 worldPosition)
    {
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform.parent as RectTransform, screenPoint, mainCamera, out Vector2 localPoint);
        return localPoint;
    }

    public void SetHighlight(bool highlighted)
    {
        if (lineRenderer != null) lineRenderer.startColor = lineRenderer.endColor = highlighted ? highlightColor : normalColor;
    }
}