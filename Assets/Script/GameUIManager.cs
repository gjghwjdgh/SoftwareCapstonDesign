using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro; // TextMeshPro 사용
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    [Header("UI 연결")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI detailText;
    public GameObject userTrailPrefab;

    [Header("로그 UI")]
    public GameObject logPanel;
    public TextMeshProUGUI logContentText; // TMP로 변경됨
    public ScrollRect logScrollView;

    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;

    [Header("씬 담당자")]
    public ARPlacementManager placementManager;
    public ARMarkerManager markerManager;

    [Header("Solver 자동 연결")]
    public SmartPathSolver pathSolver;
    public Transform targetParent; // 타겟들이 모이는 부모 객체

    [Header("설정")]
    public float travelDuration = 3.0f;

    [Header("입력 판별 설정")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Range(0f, 1f)] public float velocityWeight = 0.2f;

    private bool isAnalyzing = false;
    private LineRenderer currentUserTrail;
    private RectTransform canvasRectTransform;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        ClearLog();
        if (logPanel != null) logPanel.SetActive(false);
    }

    void Start()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null) canvasRectTransform = canvas.GetComponent<RectTransform>();

        if (pathSolver == null) pathSolver = FindFirstObjectByType<SmartPathSolver>();
        if (pathVisualizer == null) pathVisualizer = FindFirstObjectByType<PathVisualizer>();
    }

    // =================================================================================
    // [기능 1] 완전 초기화 (Reset)
    // =================================================================================
    public void OnClick_ResetAll()
    {
        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            arSession.Reset();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // =================================================================================
    // [기능 2] 로그 시스템
    // =================================================================================
    public void OnClick_ToggleLog(bool show)
    {
        if (logPanel != null) logPanel.SetActive(show);
    }

    public void Log(string message)
    {
        if (logContentText != null)
        {
            // 시간 포함해서 로그 남기기
            logContentText.text += $"<color=yellow>[{System.DateTime.Now:HH:mm:ss}]</color> {message}\n";
        }
        Debug.Log(message); // 콘솔에도 출력

        // 자동 스크롤
        if (logScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            logScrollView.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearLog()
    {
        if (logContentText != null) logContentText.text = "";
    }

    // =================================================================================
    // [기능 3] 분석 프로세스
    // =================================================================================
    public void StartAnalysis()
    {
        if (isAnalyzing) return;
        StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        isAnalyzing = true;

        // ★ 로그: 시작 알림
        Log(">>> [분석 시작] 버튼이 눌렸습니다.");

        if (placementManager != null) placementManager.EnterAnalysisState();
        if (markerManager != null) markerManager.EnterAnalysisState();

        pathVisualizer.HighlightPath(-1);
        if (infoText != null) infoText.text = "경로 계산 중...";

        // 타겟 없으면 취소
        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            Log("오류: 타겟이 하나도 없습니다. 분석 취소.");
            if (infoText != null) infoText.text = "타겟이 없습니다.";
            yield return new WaitForSeconds(1.0f);
            GoToIdleState();
            yield break;
        }

        // Solver 실행
        if (pathSolver == null) pathSolver = FindFirstObjectByType<SmartPathSolver>();

        // ★ 로그: Solver에게 넘기기 직전
        Log($"Solver 실행... (대상: {pathVisualizer.targets.Count}개)");

        List<PathResultData> solvedPaths = pathSolver.Solve(
            pathVisualizer.startPoint,
            pathVisualizer.targets,
            Camera.main
        );

        // ★ 로그: Solver 계산 끝
        Log($"계산 완료. {solvedPaths.Count}개의 경로가 생성되었습니다.");

        // 시각화
        pathVisualizer.DrawSolvedPaths(solvedPaths);

        // 도우미 이동
        var targetDataMap = new Dictionary<int, (List<Vector2>, List<float>)>();
        int finishedCount = 0;

        foreach (var data in solvedPaths)
        {
            pursuitMover3D.StartMovementWithPhase(data.pathPoints, travelDuration, data.phaseValue, data.overrideColor,
                (screenPath, times) => {
                    if (screenPath != null) targetDataMap[data.targetIndex] = (screenPath, times);
                    finishedCount++;
                });
        }

        // 사용자 입력 대기
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

        if (infoText != null) infoText.text = "화면을 눌러 선을 따라 그리세요.";
        Log("사용자 입력 대기 중...");

        yield return new WaitUntil(() => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

        // 트레일 그리기 시작
        if (userTrailPrefab != null && canvasRectTransform != null)
        {
            GameObject trailObj = Instantiate(userTrailPrefab, canvasRectTransform);
            currentUserTrail = trailObj.GetComponent<LineRenderer>();
            currentUserTrail.positionCount = 0;
        }

        if (infoText != null) infoText.text = "드로잉 중...";

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

        if (infoText != null) infoText.text = "결과 분석 중...";
        yield return new WaitUntil(() => finishedCount >= solvedPaths.Count);
        pursuitMover3D.StopAllMovements();

        // 결과 분석
        if (userDrawnPath.Count < 5)
        {
            Log("결과: 입력이 너무 짧아서 판별 불가.");
            if (infoText != null) infoText.text = "입력이 너무 짧습니다.";
        }
        else
        {
            var results = new List<(int targetIdx, float combinedScore, float frechet, float velocity)>();
            float userAverageSpeed = GestureAnalyser.GetAverageSpeed(userDrawnPath, userDrawnTimes);

            foreach (var kvp in targetDataMap)
            {
                int idx = kvp.Key;
                var (helperPath, helperTimes) = kvp.Value;

                if (helperPath == null || helperPath.Count < 2) continue;

                float frechetCost = GestureAnalyser.CalculateFrechetDistance(userDrawnPath, helperPath);
                float targetAvgSpeed = GestureAnalyser.GetAverageSpeed(helperPath, helperTimes);
                float velocitySim = GestureAnalyser.CalculateVelocitySimilarity(userAverageSpeed, targetAvgSpeed);

                float diagLen = Vector2.Distance(helperPath.First(), helperPath.Last());
                float normFrechet = (diagLen > 1f) ? frechetCost / diagLen : frechetCost;

                float score = (shapeWeight * normFrechet) + (velocityWeight * (1.0f - velocitySim));

                results.Add((idx, score, frechetCost, velocitySim));
            }

            if (results.Any())
            {
                var best = results.OrderBy(r => r.combinedScore).First();

                int visualNumber = -1;
                for (int k = 0; k < solvedPaths.Count; k++)
                {
                    if (solvedPaths[k].targetIndex == best.targetIdx)
                    {
                        visualNumber = k + 1;
                        break;
                    }
                }

                // ★ 로그: 최종 결과
                Log($"판별 성공! -> {visualNumber}번 경로 (원래인덱스: {best.targetIdx})");
                Log($"상세 점수: 오차({best.frechet:F2}), 속도일치({best.velocity:P0})");

                if (infoText != null) infoText.text = $"판별 성공: {visualNumber}번 경로";
                if (detailText != null) detailText.text = $"오차: {best.frechet:F2}, 속도: {best.velocity:P0}";

                pathVisualizer.HighlightPath(best.targetIdx);
            }
            else
            {
                Log("결과: 매칭되는 경로를 찾지 못함.");
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

        if (infoText != null && !isAnalyzing) infoText.text = "목표 지점을 추가하거나 다시 분석하세요.";
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