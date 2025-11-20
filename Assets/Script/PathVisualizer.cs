using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    public Transform startPoint;
    public List<Transform> targets = new List<Transform>();
    public GameObject lineDrawer3DPrefab;

    // Solver 자동 연결
    public SmartPathSolver pathSolver;

    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();
    private Dictionary<int, PathDrawer> indexToDrawerMap = new Dictionary<int, PathDrawer>();

    // 계산된 최신 경로 데이터를 저장 (나중에 GameUIManager가 씀)
    public List<PathResultData> LatestSolvedPaths { get; private set; }

    void Start()
    {
        if (pathSolver == null) pathSolver = FindAnyObjectByType<SmartPathSolver>();
    }

    // ★★★ 타겟이 추가될 때마다 호출될 함수 ★★★
    // 직선이 아니라, Solver를 돌려서 바로 곡선을 그립니다.
    public void GenerateAndShowAllPaths()
    {
        ClearLines();
        if (startPoint == null || targets == null || targets.Count == 0 || pathSolver == null) return;

        // 1. 즉시 Solver 계산
        LatestSolvedPaths = pathSolver.Solve(startPoint, targets, Camera.main);

        // 2. 계산된 곡선 그리기
        foreach (var data in LatestSolvedPaths)
        {
            CreateDrawer(data.targetIndex, data.pathPoints);
        }
    }

    private void CreateDrawer(int index, List<Vector3> points)
    {
        PathDrawer drawer = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
        drawer.InitializeCurve(points);
        pathDrawers3D.Add(drawer);
        indexToDrawerMap[index] = drawer;
    }

    public void HighlightPath(int targetIndex)
    {
        foreach (var kvp in indexToDrawerMap) kvp.Value.SetHighlight(kvp.Key == targetIndex);
    }

    private void ClearLines()
    {
        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        indexToDrawerMap.Clear();
    }
}