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

    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;
    public UIPursuitMover pursuitMover2D;
    public ARPlacementManager placementManager;

    [Header("설정")]
    public float travelDuration = 3.0f;

    [Header("페이즈(Phase) 배분 설정")]
    [Range(1, 10)]
    public int phaseCount = 4;

    [Header("입력 판별 설정")]
    [Tooltip("형태(모양)의 중요도")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Tooltip("속도의 중요도")]
    [Range(0f, 1f)] public float velocityWeight = 0.2f;
    [Tooltip("사용자가 인식하는 도우미의 속도 보정 계수")]
    [Range(0f, 1f)] public float perceptionCoefficient = 0.8f;

    private bool isAnalyzing = false;

    public void StartAnalysis()
    {
        if (isAnalyzing) return;
        StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        isAnalyzing = true;

        if (placementManager != null) placementManager.SetPlacementMode(false);
        pathVisualizer.HighlightPath(-1);
        if (infoText != null) infoText.text = "경로를 따라 그려보세요!";
        if (detailText != null) detailText.text = "";

        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            GoToIdleState();
            yield break;
        }

        // --- (이하 도우미 객체 출발까지의 코드는 이전과 동일) ---
        int pathCount = pathVisualizer.targets.Count;
        var targetData = new List<(List<Vector2> path, List<float> times)>(new (List<Vector2>, List<float>)[pathCount]);
        int finishedCount = 0;
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

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
            List<Vector3> fullPath = new List<Vector3>();
            for (int j = 0; j <= 100; j++)
            {
                fullPath.Add(Vector3.Lerp(startPoint.position, target.position, j / 100f));
            }
            int phaseGroupIndex = capturedIndex % phaseCount;
            float startFraction = (float)(phaseGroupIndex + 1) / (phaseCount + 1);
            int startIndex = Mathf.RoundToInt((fullPath.Count - 1) * startFraction);
            List<Vector3> partialPath = fullPath.GetRange(startIndex, fullPath.Count - startIndex);
            pursuitMover3D.StartMovement(partialPath, travelDuration, (p, t) => onPursuitComplete(capturedIndex, p, t));
        }

        float drawingTimer = 0f;
        yield return new WaitUntil(() => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        while (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            userDrawnPath.Add(Touchscreen.current.primaryTouch.position.ReadValue());
            userDrawnTimes.Add(Time.time);
            if (drawingTimer >= travelDuration) break;
            drawingTimer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitUntil(() => finishedCount >= pathCount);

        if (userDrawnPath.Count < 5)
        {
            if (infoText != null) infoText.text = "입력이 너무 짧습니다. 다시 시도하세요.";
        }
        else
        {
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            // ★★★ 여기가 기존 시스템을 '보완'하는 새로운 분석 로직입니다 ★★★
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            var results = new List<(int index, float frechet, float velocitySim, float combinedScore)>();

            float userAverageSpeed = GestureAnalyser.GetAverageSpeed(userDrawnPath, userDrawnTimes);
            Debug.Log($"사용자 평균 속도: {userAverageSpeed:F2}");

            for (int i = 0; i < pathCount; i++)
            {
                if (i < targetData.Count)
                {
                    var (path, times) = targetData[i];
                    if (path == null || path.Count < 2) continue;

                    // 1. 프레셰 거리 계산 (기존과 동일)
                    float frechetCost = GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path);

                    // 2. 속도 유사도 계산 (기존과 동일)
                    float targetAverageSpeed = GestureAnalyser.GetAverageSpeed(path, times);
                    Debug.Log($"[타겟 {i + 1}] 도우미 평균 속도: {targetAverageSpeed:F2}");
                    float correctedTargetSpeed = targetAverageSpeed * perceptionCoefficient;
                    float velocitySimilarity = GestureAnalyser.CalculateVelocitySimilarity(userAverageSpeed, correctedTargetSpeed);

                    // 3. 최종 점수 계산 (보완된 방식)
                    // 프레셰 거리를 경로의 대각선 길이로 나누어 '정규화'합니다.
                    float pathDiagonalLength = Vector2.Distance(path.First(), path.Last());
                    float normalizedShapeCost = (pathDiagonalLength > 1f) ? (frechetCost / pathDiagonalLength) : frechetCost;

                    // 속도 유사도는 '비용(Cost)' 개념으로 변환합니다.
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
                    // 이제 원래의 '프레셰 거리'와 '속도 유사도'를 그대로 표시합니다.
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
        if (placementManager != null) placementManager.SetPlacementMode(true);
        if (infoText != null) infoText.text = "목표 지점을 추가하거나 다시 분석하세요.";
        if (detailText != null) detailText.text = "";
        pathVisualizer.HighlightPath(-1);
        isAnalyzing = false;
    }
}