using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
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
    public void GenerateAndShowAllPaths()
    {
        if (startPoint == null || targets == null || targets.Count == 0 || pathSolver == null) return;

        // 1. 즉시 Solver 계산
        LatestSolvedPaths = pathSolver.Solve(startPoint, targets, Camera.main);

        // 2. 계산된 데이터로 그리기 (여기서 DrawSolvedPaths를 호출)
        DrawSolvedPaths(LatestSolvedPaths);
    }

    // ★★★ 이 함수가 없어서 오류가 났던 것입니다. 추가했습니다. ★★★
    public void DrawSolvedPaths(List<PathResultData> solvedData)
    {
        ClearLines();
        LatestSolvedPaths = solvedData;

        // solvedData는 이미 [왼쪽 -> 오른쪽] 등으로 정렬된 상태입니다.
        for (int i = 0; i < solvedData.Count; i++)
        {
            var data = solvedData[i];

            // 1. 선 그리기 (색상 포함)
            CreateDrawer(data.targetIndex, data.pathPoints, data.overrideColor);

            // 2. 타겟에 번호표 붙이기 (1번부터 시작)
            // data.targetIndex는 targets 리스트의 원래 인덱스입니다.
            if (data.targetIndex < targets.Count && targets[data.targetIndex] != null)
            {
                TargetLabel label = targets[data.targetIndex].GetComponentInChildren<TargetLabel>();
                if (label != null)
                {
                    // i는 0부터 시작하므로 1을 더해서 1, 2, 3... 으로 표시
                    label.SetNumber(i + 1);
                }
            }
        }
    }

    // 색상까지 받는 버전으로 수정
    private void CreateDrawer(int index, List<Vector3> points, Color? color)
    {
        PathDrawer drawer = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
        drawer.InitializeCurve(points);

        // 그룹 색상 적용
        drawer.SetColor(color);

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