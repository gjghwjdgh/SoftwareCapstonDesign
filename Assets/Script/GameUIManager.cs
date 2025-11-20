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

    // ★★★ 여기가 바뀐 부분: 헷갈리지 않게 전용 슬롯 2개를 만듭니다 ★★★
    [Header("씬 담당자 (현재 씬에 맞는 것 하나만 연결, 나머지는 None)")]
    public ARPlacementManager placementManager;
    public ARMarkerManager markerManager;

    [Header("Solver 자동 연결")]
    public SmartPathSolver pathSolver;

    [Header("설정")]
    public float travelDuration = 3.0f;
    [Header("자동 곡률 설정")]
    public float curvatureStrength = 0.5f;
    [Header("페이즈(Phase) 배분 설정")]
    [Range(1, 10)] public int phaseCount = 4;
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

        // ★★★ 연결된 매니저가 있으면 실행 (둘 중 하나만 연결되어 있을 테니까요) ★★★
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

        // 1. Solver 계산 (이미 PathVisualizer가 실시간으로 하고 있음)
        // 데이터를 가져오기만 하면 됨
        List<PathResultData> solvedPaths = pathVisualizer.LatestSolvedPaths;

        // 만약 데이터가 없다면(혹시 모르니) 강제 계산
        if (solvedPaths == null || solvedPaths.Count == 0)
        {
            solvedPaths = pathSolver.Solve(pathVisualizer.startPoint, pathVisualizer.targets, Camera.main);
        }

        // 2. 시각화 (이미 되어있음, 생략 가능하거나 Highlight 초기화)
        pathVisualizer.HighlightPath(-1);

        // 3. 도우미 출발
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

        // 4. 사용자 입력
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

        // 5. 분석
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

                float diagLen = Vector2.Distance(path.First(), path.Last());
                float normFrechet = (diagLen > 1f) ? frechetCost / diagLen : frechetCost;
                float score = (shapeWeight * normFrechet) + (velocityWeight * (1.0f - velocitySim));

                results.Add((idx, score, frechetCost, velocitySim));
            }

            if (results.Any())
            {
                var best = results.OrderBy(r => r.combinedScore).First();
                if (infoText != null) infoText.text = $"판별 성공: 경로 {best.index + 1}";
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

        // ★★★ 연결된 매니저를 다시 깨웁니다 ★★★
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
}