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

    // ★★★ 여기가 수정된 부분: 두 개의 전용 슬롯을 만듭니다 ★★★
    [Header("씬 담당자 (해당 씬에 맞는 것 하나만 연결)")]
    public ARPlacementManager placementManager;
    public ARMarkerManager markerManager;

    // --- (이하 모든 설정 변수는 동일) ---
    [Header("설정")]
    public float travelDuration = 3.0f;
    [Header("자동 곡률 설정")]
    public float curvatureStrength = 0.5f;
    [Header("페이즈(Phase) 배분 설정")]
    [Range(1, 10)]
    public int phaseCount = 4;
    [Header("입력 판별 설정")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Range(0f, 1f)] public float velocityWeight = 0.2f;

    private bool isAnalyzing = false;
    private LineRenderer currentUserTrail;
    private RectTransform canvasRectTransform;

    void Start()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
        }
    }

    public void StartAnalysis()
    {
        if (isAnalyzing) return;
        StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        isAnalyzing = true;

        // ★★★ 이제 직접 확인하고 호출합니다 ★★★
        if (placementManager != null) placementManager.EnterAnalysisState();
        if (markerManager != null) markerManager.EnterAnalysisState();

        pathVisualizer.HighlightPath(-1);
        if (infoText != null) infoText.text = "반복되는 객체를 보고 경로를 따라 그려보세요!";
        if (detailText != null) detailText.text = "";

        // --- (이하 분석 로직은 모두 이전과 동일합니다. 끝까지 확인했습니다.) ---
        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            GoToIdleState();
            yield break;
        }
        int pathCount = pathVisualizer.targets.Count;
        var targetData = new List<(List<Vector2> path, List<float> times)>(new (List<Vector2>, List<float>)[pathCount]);
        int finishedCount = 0;
        System.Action<int, List<Vector2>, List<float>> onPursuitComplete = (index, path, times) =>
        {
            if (index >= 0 && index < targetData.Count)
            {
                if (path != null) targetData[index] = (path, times);
            }
            finishedCount++;
        };
        for (int i = 0; i < pathCount; i++)
        {
            int capturedIndex = i;
            Transform startPoint = pathVisualizer.startPoint;
            Transform target = pathVisualizer.targets[capturedIndex];
            if (startPoint == null || target == null) continue;
            Vector3 p0 = startPoint.position;
            Vector3 p2 = target.position;
            Vector3 midPoint = (p0 + p2) / 2f;
            Vector3 direction = (p2 - p0).normalized;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            float curveDirection = (i % 2 == 0) ? 1f : -1f;
            float curveMagnitude = (i / 2 + 1) * curvatureStrength;
            Vector3 controlPoint = midPoint + (perpendicular * curveDirection * curveMagnitude);
            List<Vector3> fullPath = PathUtilities.GenerateQuadraticBezierCurvePath(p0, controlPoint, p2, 50);
            int phaseGroupIndex = capturedIndex % phaseCount;
            float startFraction = (float)(phaseGroupIndex + 1) / (phaseCount + 1);
            int startIndex = Mathf.RoundToInt((fullPath.Count - 1) * startFraction);
            List<Vector3> partialPath = fullPath.GetRange(startIndex, fullPath.Count - startIndex);
            pursuitMover3D.StartMovement(partialPath, travelDuration, (p, t) => onPursuitComplete(capturedIndex, p, t));
        }
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
        if (infoText != null) infoText.text = "그리는 중... 손을 떼면 분석이 시작됩니다.";
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
        yield return new WaitUntil(() => finishedCount >= pathCount);
        pursuitMover3D.StopAllMovements();

        if (userDrawnPath.Count < 5)
        {
            if (infoText != null) infoText.text = "입력이 너무 짧습니다. 다시 시도하세요.";
        }
        else
        {
            var results = new List<(int index, float frechet, float velocitySim, float combinedScore)>();
            float userAverageSpeed = GestureAnalyser.GetAverageSpeed(userDrawnPath, userDrawnTimes);
            for (int i = 0; i < pathCount; i++)
            {
                if (i < targetData.Count)
                {
                    var (path, times) = targetData[i];
                    if (path == null || path.Count < 2) continue;
                    float frechetCost = GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path);
                    float targetAverageSpeed = GestureAnalyser.GetAverageSpeed(path, times);
                    float velocitySimilarity = GestureAnalyser.CalculateVelocitySimilarity(userAverageSpeed, targetAverageSpeed);
                    float pathDiagonalLength = Vector2.Distance(path.First(), path.Last());
                    float normalizedShapeCost = (pathDiagonalLength > 1f) ? (frechetCost / pathDiagonalLength) : frechetCost;
                    float velocityCost = 1.0f - velocitySimilarity;
                    float combinedScore = (shapeWeight * normalizedShapeCost) + (velocityWeight * velocityCost);
                    if (float.IsNaN(combinedScore)) continue;
                    results.Add((i, frechetCost, velocitySimilarity, combinedScore));
                }
            }
            if (results.Any())
            {
                var bestMatch = results.OrderBy(r => r.combinedScore).First();
                if (infoText != null) infoText.text = $"판별 성공: 경로 {bestMatch.index + 1}";
                if (detailText != null)
                {
                    detailText.text = $"프레셰 거리: {bestMatch.frechet:F2}\n속도 유사도: {bestMatch.velocitySim:P0}";
                }
                pathVisualizer.HighlightPath(bestMatch.index);
            }
            else
            {
                if (infoText != null) infoText.text = "판별 실패. 다시 시도해 주세요.";
            }
        }

        yield return new WaitForSeconds(3.0f);
        GoToIdleState();
    }

    private void GoToIdleState()
    {
        if (currentUserTrail != null)
        {
            Destroy(currentUserTrail.gameObject);
            currentUserTrail = null;
        }
        if (pursuitMover3D != null) pursuitMover3D.StopAllMovements();

        // ★★★ 이제 직접 확인하고 호출합니다 ★★★
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