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

    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;
    public UIPursuitMover pursuitMover2D;
    public ARPlacementManager placementManager;

    [Header("설정")]
    public float travelDuration = 3.0f;

    [Header("페이즈(Phase) 배분 설정")]
    [Tooltip("사용할 총 페이즈(출발점 종류)의 개수입니다. 이 개수만큼 출발점이 순환 배정됩니다.")]
    [Range(1, 10)]
    public int phaseCount = 4;

    [Header("입력 판별 설정")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Range(0f, 1f)] public float velocityWeight = 0.2f;
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

        if (pathVisualizer.targets == null || pathVisualizer.targets.Count == 0)
        {
            GoToIdleState();
            yield break;
        }

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
            // ★★★ 바로 이 부분이 제가 빼먹었던 핵심 '경로 인식 시스템' 로직입니다 ★★★
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            var results = new List<(int index, float combinedScore)>();

            // 모든 타겟 경로에 대해 프레셰 거리를 계산합니다.
            for (int i = 0; i < pathCount; i++)
            {
                if (i < targetData.Count)
                {
                    var (path, _) = targetData[i];
                    if (path == null || path.Count == 0) continue;

                    // GestureAnalyser를 사용하여 형태 유사도를 계산합니다.
                    float frechetCost = GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path);

                    // (속도 유사도 등 다른 분석 로직도 여기에 추가할 수 있습니다)
                    // float combinedScore = (shapeWeight * normalizedFrechet) + (velocityWeight * velocityCost);

                    // 우선은 프레셰 거리만으로 점수를 매깁니다.
                    results.Add((i, frechetCost));
                }
            }

            // 가장 점수가 낮은(가장 유사한) 경로를 찾습니다.
            if (results.Any())
            {
                var bestMatch = results.OrderBy(r => r.combinedScore).First();
                if (infoText != null) infoText.text = $"판별 성공: 경로 {bestMatch.index + 1}";

                // PathVisualizer에게 최종 결과를 알려 하이라이트하도록 합니다.
                pathVisualizer.HighlightPath(bestMatch.index);
            }
            else
            {
                if (infoText != null) infoText.text = "판별 실패. 다시 시도해 주세요.";
            }
        }

        yield return new WaitForSeconds(2.0f);
        GoToIdleState();
    }

    // GoToIdleState는 오브젝트를 '청소'하지 않습니다.
    private void GoToIdleState()
    {
        if (placementManager != null) placementManager.SetPlacementMode(true);
        if (infoText != null) infoText.text = "목표 지점을 추가하거나 다시 분석하세요.";
        pathVisualizer.HighlightPath(-1);
        isAnalyzing = false;
    }
}