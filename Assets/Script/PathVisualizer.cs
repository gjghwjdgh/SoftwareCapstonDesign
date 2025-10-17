using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
    // ✨ --- 핵심 변경: 여러 개의 startPoints가 아닌, 단 하나의 startPoint로 복귀 ---
    public Transform startPoint;
    public List<Transform> targets;
    public Transform uiCanvas;

    [Header("프리팹")]
    public GameObject lineDrawer3DPrefab;
    public GameObject uiLinePrefab;

    private GameUIManager gameUIManager;
    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();
    private List<UILineConnector> uiLines2D = new List<UILineConnector>();
    private bool is3DMode = true;
    public bool Is3DMode => is3DMode;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        gameUIManager = FindAnyObjectByType<GameUIManager>();
        GenerateAndShowAllPaths();
    }

    public void GenerateAndShowAllPaths()
    {
        if (gameUIManager == null) gameUIManager = FindAnyObjectByType<GameUIManager>();

        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        foreach (var line in uiLines2D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        uiLines2D.Clear();

        // ✨ --- 핵심 변경: targets 리스트를 기준으로 루프를 실행 ---
        for (int i = 0; i < targets.Count; i++)
        {
            if (startPoint == null || targets[i] == null) continue;

            PathDrawer drawer3D = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
            UILineConnector uiLine = Instantiate(uiLinePrefab, uiCanvas).GetComponent<UILineConnector>();

            // 1번 타겟(인덱스 0)에만 곡선/직선 토글 기능 적용
            bool isCurve = (i == 0 && gameUIManager.IsTarget1CurveMode && gameUIManager.target1_ControlPoint1 != null && gameUIManager.target1_ControlPoint2 != null);

            if (isCurve)
            {
                var pathPoints = PathUtilities.GenerateBezierCurvePath(
                    startPoint.position, // 공통 시작점
                    gameUIManager.target1_ControlPoint1.position,
                    gameUIManager.target1_ControlPoint2.position,
                    targets[i].position, // 각자의 도착점
                    gameUIManager.curveSegmentCount
                );
                drawer3D.InitializeCurve(pathPoints);
                uiLine.InitializeCurve(pathPoints, mainCamera);
            }
            else
            {
                drawer3D.InitializeLine(startPoint, targets[i]); // 공통 시작점과 각자의 도착점
                uiLine.InitializeLine(startPoint, targets[i], mainCamera);
            }
            pathDrawers3D.Add(drawer3D);
            uiLines2D.Add(uiLine);
        }
        UpdateViewMode();
    }

    // 이 함수들은 이제 거의 사용되지 않지만, 호환성을 위해 유지
    public Transform GetTargetTransform(int index) { return (index < 0 || index >= targets.Count) ? null : targets[index]; }
    public UILineConnector GetUILine(int index) { return (index < 0 || index >= uiLines2D.Count) ? null : uiLines2D[index]; }
    public void SwitchViewMode() { is3DMode = !is3DMode; UpdateViewMode(); }

    void UpdateViewMode()
    {
        foreach (var line in pathDrawers3D) if (line != null) line.gameObject.SetActive(is3DMode);
        foreach (var line in uiLines2D) if (line != null) line.gameObject.SetActive(!is3DMode);
    }

    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            bool isHighlighted = (i == targetIndex);
            if (i < pathDrawers3D.Count && pathDrawers3D[i] != null) pathDrawers3D[i].SetHighlight(isHighlighted);
            if (i < uiLines2D.Count && uiLines2D[i] != null) uiLines2D[i].SetHighlight(isHighlighted);
        }
    }
}