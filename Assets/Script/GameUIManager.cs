using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
    public TextMeshProUGUI logContentText;
    public ScrollRect logScrollView;

    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;

    [Header("씬 담당자")]
    public ARPlacementManager placementManager;
    public ARMarkerManager markerManager;

    [Header("설정")]
    public SmartPathSolver pathSolver;
    public Transform targetParent; // ★ 여기가 TargetGroup이 들어갈 곳
    public float travelDuration = 3.0f;

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

    // ★★★ [추가된 기능] 타겟이 생길 때마다 호출됨
    public void NotifyTargetSpawned()
    {
        ClearLog(); // 로그 화면 지우기
        Log("=== [새로운 타겟 생성됨] 로그 대기 중 ===");
        Log("타겟이 추가되었습니다. [분석 시작]을 누르면 상세 계산이 표시됩니다.");

        // 안내 문구 변경
        if (infoText != null) infoText.text = "타겟 배치 중...";
    }

    // [기능] 완전 초기화 (Reset 버튼)
    public void OnClick_ResetAll()
    {
        // 1. AR 세션 리셋 (공간 인식 정보 날리기)
        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null) arSession.Reset();

        // 2. 씬 새로고침 (가장 확실한 초기화)
        // TargetGroup 아래 있는 애들도 씬이 다시 로드되면서 다 사라집니다.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // [기능] 로그 창 열기/닫기
    public void OnClick_ToggleLog(bool show)
    {
        if (logPanel != null) logPanel.SetActive(show);
    }

    public void Log(string message)
    {
        if (logContentText != null)
        {
            logContentText.text += $"<color=yellow>[{System.DateTime.Now:HH:mm:ss}]</color> {message}\n";
        }
        Debug.Log(message);

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

    // [기능] 분석 시작
    public void StartAnalysis()
    {
        if (isAnalyzing) return;
        StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        isAnalyzing = true;

        // 로그 출력 시작
        ClearLog();
        Log(">>> [분석 시작] 버튼 클릭됨");

        if (placementManager != null) placementManager.EnterAnalysisState();
        if (markerManager != null) markerManager.EnterAnalysisState();

        pathVisualizer.HighlightPath(-1);
        if (infoText != null) infoText.text = "경로 계산 중...";

        // 타겟 확인
        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            Log("<color=red>오류: 타겟이 없습니다.</color>");
            if (infoText != null) infoText.text = "타겟이 없습니다.";
            yield return new WaitForSeconds(1.0f);
            GoToIdleState();
            yield break;
        }

        if (pathSolver == null) pathSolver = FindFirstObjectByType<SmartPathSolver>();

        Log($"Solver 알고리즘 실행 (타겟 수: {pathVisualizer.targets.Count}개)");

        // 계산 실행
        List<PathResultData> solvedPaths = pathSolver.Solve(
            pathVisualizer.startPoint,
            pathVisualizer.targets,
            Camera.main
        );

        Log($"계산 완료. {solvedPaths.Count}개의 경로 생성됨.");

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

        // 드로잉 대기
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

        if (infoText != null) infoText.text = "화면을 눌러 선을 따라 그리세요.";

        yield return new WaitUntil(() => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

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

        if (userDrawnPath.Count < 5)
        {
            Log("결과: 입력이 너무 짧습니다.");
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

                Log($"<color=green>▶ 판별 성공! 경로 {visualNumber}</color>");
                if (infoText != null) infoText.text = $"판별 성공: {visualNumber}번 경로";
                if (detailText != null) detailText.text = $"오차: {best.frechet:F2}, 속도: {best.velocity:P0}";

                pathVisualizer.HighlightPath(best.targetIdx);
            }
            else
            {
                Log("판별 실패.");
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
        Log("--- 대기 상태 ---");
    }

    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
    {
        if (canvasRectTransform == null) return Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, null, out Vector2 localPoint);
        return localPoint;
    }
}