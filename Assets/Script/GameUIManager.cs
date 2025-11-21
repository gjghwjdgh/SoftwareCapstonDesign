using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;

public class GameUIManager : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI detailText;
    public GameObject userTrailPrefab;

    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;

    [Header("씬 담당자 (현재 씬에 맞는 것 하나만 연결, 나머지는 None)")]
    public ARPlacementManager placementManager;
    public ARMarkerManager markerManager;

    [Header("Solver 자동 연결")]
    public SmartPathSolver pathSolver;

    [Header("설정")]
    public float travelDuration = 3.0f;

    // (사용되지 않는 변수는 제거하여 혼란 방지)
    [Header("입력 판별 설정")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Range(0f, 1f)] public float velocityWeight = 0.2f;

    private bool isAnalyzing = false;
    private LineRenderer currentUserTrail;
    private RectTransform canvasRectTransform;

    void Start()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) canvasRectTransform = canvas.GetComponent<RectTransform>();
        if (pathSolver == null) pathSolver = FindAnyObjectByType<SmartPathSolver>();
    }

    public void StartAnalysis()
    {
        if (isAnalyzing) return;
        StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        isAnalyzing = true;

        // 1. 분석 시작 즉시 매니저 비활성화 (추가 생성 차단)
        if (placementManager != null) placementManager.EnterAnalysisState();
        if (markerManager != null) markerManager.EnterAnalysisState();

        pathVisualizer.HighlightPath(-1);
        if (infoText != null) infoText.text = "도우미를 따라 그려보세요!";
        if (detailText != null) detailText.text = "";

        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            GoToIdleState();
            yield break;
        }

        // ★★★ [중요] 분석 직전에 타겟들을 화면 좌측->우측 순서로 재정렬 ★★★
        SortTargetsLeftToRight();

        // 2. Solver에게 계산 요청 (정렬된 타겟 리스트 사용)
        // (PathVisualizer가 실시간으로 하고 있지만, 정렬 후 확정된 데이터를 얻기 위해 다시 호출)
        List<PathResultData> solvedPaths = pathSolver.Solve(
            pathVisualizer.startPoint,
            pathVisualizer.targets,
            Camera.main
        );

        // 3. 시각화 업데이트 (계산된 곡선 및 번호 갱신)
        pathVisualizer.DrawSolvedPaths(solvedPaths);

        // 4. 도우미 객체 출발 (계산된 페이즈 위치로 즉시 이동)
        var targetDataMap = new Dictionary<int, (List<Vector2>, List<float>)>();
        int finishedCount = 0;

        foreach (var data in solvedPaths)
        {
            pursuitMover3D.StartMovementWithPhase(data.pathPoints, travelDuration, data.phaseValue, data.overrideColor,
                (p, t) => {
                    if (p != null) targetDataMap[data.targetIndex] = (p, t);
                    finishedCount++;
                });
        }

        // 5. 사용자 입력 (터치)
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

        if (infoText != null) infoText.text = "준비되면 화면을 눌러 그리기 시작하세요.";
        yield return new WaitUntil(() => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

        if (userTrailPrefab != null && canvasRectTransform != null)
        {
            GameObject trailObj = Instantiate(userTrailPrefab, canvasRectTransform);
            currentUserTrail = trailObj.GetComponent<LineRenderer>();
            currentUserTrail.positionCount = 0;
        }

        if (infoText != null) infoText.text = "그리는 중...";

        while (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            userDrawnPath.Add(touchPosition);
            userDrawnTimes.Add(Time.time);
            if (currentUserTrail != null)
            {
                int pointCount = currentUserTrail.positionCount;
                currentUserTrail.positionCount = pointCount + 1;
                currentUserTrail.SetPosition(pointCount, ScreenToCanvasPosition(touchPosition));
            }
            yield return null;
        }

        if (infoText != null) infoText.text = "분석 중...";
        yield return new WaitUntil(() => finishedCount >= solvedPaths.Count);
        pursuitMover3D.StopAllMovements();

        // 6. 분석 및 결과
        if (userDrawnPath.Count < 5)
        {
            if (infoText != null) infoText.text = "입력이 너무 짧습니다.";
        }
        else
        {
            var results = new List<(int index, float combinedScore, float frechet, float velocity)>();
            float userAverageSpeed = GestureAnalyser.GetAverageSpeed(userDrawnPath, userDrawnTimes);

            foreach (var kvp in targetDataMap)
            {
                int idx = kvp.Key;
                var (path, times) = kvp.Value;
                if (path == null || path.Count < 2) continue;

                float frechetCost = GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path);
                float targetAvgSpeed = GestureAnalyser.GetAverageSpeed(path, times);
                float velocitySim = GestureAnalyser.CalculateVelocitySimilarity(userAverageSpeed, targetAvgSpeed);

                // 정규화 (대각선 길이)
                float diagLen = Vector2.Distance(path.First(), path.Last());
                float normFrechet = (diagLen > 1f) ? frechetCost / diagLen : frechetCost;

                // 점수 계산
                float score = (shapeWeight * normFrechet) + (velocityWeight * (1.0f - velocitySim));

                results.Add((idx, score, frechetCost, velocitySim));
            }

            if (results.Any())
            {
                var best = results.OrderBy(r => r.combinedScore).First();

                // ★★★ [결과 표시] 시각적 번호(1, 2, 3...) 찾기 ★★★
                int visualNumber = -1;
                for (int k = 0; k < solvedPaths.Count; k++)
                {
                    if (solvedPaths[k].targetIndex == best.index)
                    {
                        visualNumber = k + 1; // 0번부터 시작하니까 +1
                        break;
                    }
                }

                if (infoText != null) infoText.text = $"판별 성공: 경로 {visualNumber}";
                if (detailText != null) detailText.text = $"오차율: {best.frechet:F2}, 속도: {best.velocity:P0}";

                pathVisualizer.HighlightPath(best.index);
            }
            else
            {
                if (infoText != null) infoText.text = "판별 실패.";
            }
        }

        yield return new WaitForSeconds(3.0f);
        GoToIdleState();
    }

    private void GoToIdleState()
    {
        if (currentUserTrail != null) { Destroy(currentUserTrail.gameObject); currentUserTrail = null; }
        if (pursuitMover3D != null) pursuitMover3D.StopAllMovements();

        if (placementManager != null) placementManager.EnterIdleState();
        if (markerManager != null) markerManager.EnterIdleState();

        if (infoText != null) infoText.text = "목표 지점을 추가하거나 다시 분석하세요.";
        if (detailText != null) detailText.text = "";
        pathVisualizer.HighlightPath(-1);
        isAnalyzing = false;
    }

    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
    {
        if (canvasRectTransform == null) return Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, null, out Vector2 localPoint);
        return localPoint;
    }

    // ★★★ 타겟을 화면 X좌표 기준으로 정렬하는 함수 ★★★
    private void SortTargetsLeftToRight()
    {
        Camera cam = Camera.main;
        if (cam == null || pathVisualizer.targets == null) return;

        pathVisualizer.targets.Sort((a, b) => {
            if (a == null || b == null) return 0;
            float screenX_A = cam.WorldToScreenPoint(a.position).x;
            float screenX_B = cam.WorldToScreenPoint(b.position).x;
            return screenX_A.CompareTo(screenX_B);
        });
    }
}