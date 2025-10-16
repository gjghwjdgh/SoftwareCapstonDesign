using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameUIManager : MonoBehaviour
{
    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;
    public UIPursuitMover pursuitMover2D;

    [Header("설정")]
    public float travelDuration = 3.0f;

    [Header("입력 판별 설정")]
    [Tooltip("형태 유사도 가중치 (0~1)")]
    [Range(0f, 1f)]
    public float shapeWeight = 0.8f;

    [Tooltip("속도 유사도 가중치 (0~1)")]
    [Range(0f, 1f)]
    public float velocityWeight = 0.2f;

    [Tooltip("사람의 지각 특성을 반영한 속도 보정 계수")]
    [Range(0f, 1f)]
    public float perceptionCoefficient = 0.8f;

    private Coroutine analysisCoroutine;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) pathVisualizer.SwitchViewMode();

        // --- 변경점 1: 시작 트리거를 '1'번 키로 변경 ---
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (analysisCoroutine != null) StopCoroutine(analysisCoroutine);
            analysisCoroutine = StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
        }
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        pathVisualizer.HighlightPath(-1);

        int targetCount = pathVisualizer.targets.Count;
        var targetData = new List<(List<Vector2> path, List<float> times)>(new (List<Vector2>, List<float>)[targetCount]);
        int finishedCount = 0;
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

        System.Action<int, List<Vector2>, List<float>> onPursuitComplete = (index, path, times) =>
        {
            if (path != null) targetData[index] = (path, times);
            finishedCount++;
        };

        for (int i = 0; i < targetCount; i++)
        {
            int targetIndex = i;
            if (pathVisualizer.Is3DMode)
            {
                Transform target = pathVisualizer.GetTargetTransform(targetIndex);
                List<Vector3> pathPoints = new List<Vector3> { pathVisualizer.startPoint.position, target.position };
                pursuitMover3D.StartMovement(pathPoints, travelDuration, (p, t) => onPursuitComplete(targetIndex, p, t));
            }
            else
            {
                UILineConnector lineToFollow = pathVisualizer.GetUILine(targetIndex);
                pursuitMover2D.StartMovement(lineToFollow, travelDuration, (p, t) => onPursuitComplete(targetIndex, p, t));
            }
        }

        // --- 변경점 2: 'travelDuration' 동안만 마우스 경로를 기록 ---
        float drawingTimer = 0f;
        while (drawingTimer < travelDuration)
        {
            userDrawnPath.Add(Input.mousePosition);
            userDrawnTimes.Add(Time.time);
            drawingTimer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitUntil(() => finishedCount == targetCount);

        // (이하 분석 로직은 모두 동일합니다)
        if (userDrawnPath.Count < 5)
        {
            Debug.LogWarning("사용자 경로가 너무 짧아 분석을 중단합니다.");
            analysisCoroutine = null;
            yield break;
        }

        float userAverageSpeed = GestureAnalyser.GetAverageSpeed(userDrawnPath, userDrawnTimes);

        Debug.Log($"--- 통합 분석 결과 (모드: {(pathVisualizer.Is3DMode ? "3D" : "2D")}) ---");
        var results = new List<(int index, float frechet, float velocitySim, float combinedScore)>();

        var frechetValues = new List<float>();
        for (int i = 0; i < targetCount; i++)
        {
            var (path, _) = targetData[i];
            if (path == null || path.Count == 0) { frechetValues.Add(float.MaxValue); continue; }
            frechetValues.Add(GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path));
        }
        float maxFrechet = frechetValues.Where(f => f < float.MaxValue).DefaultIfEmpty(0).Max();
        if (maxFrechet <= float.Epsilon) maxFrechet = 1.0f;

        for (int i = 0; i < targetCount; i++)
        {
            var (path, times) = targetData[i];
            if (path == null || path.Count == 0) continue;

            float frechetCost = frechetValues[i];
            float normalizedFrechet = frechetCost / maxFrechet;

            float targetAverageSpeed = GestureAnalyser.GetAverageSpeed(path, times);
            float correctedTargetSpeed = targetAverageSpeed * perceptionCoefficient;
            float velocitySimilarity = GestureAnalyser.CalculateVelocitySimilarity(userAverageSpeed, correctedTargetSpeed);
            float velocityCost = 1.0f - velocitySimilarity;

            float combinedScore = (shapeWeight * normalizedFrechet) + (velocityWeight * velocityCost);

            if (float.IsNaN(combinedScore)) continue;

            results.Add((i, frechetCost, velocitySimilarity, combinedScore));
            Debug.Log($"[타겟 {i + 1}] 프레셰: {frechetCost:F2}, 속도유사도: {velocitySimilarity:P2}, 최종점수: {combinedScore:F3}");
        }

        if (results.Any())
        {
            var bestMatch = results.OrderBy(r => r.combinedScore).First();
            Debug.Log($"--- 최종 판별 ---");
            Debug.Log($"<color=cyan><입력 성공> '타겟 {bestMatch.index + 1}'이(가) 최종 입력으로 판별되었습니다. (최저 점수: {bestMatch.combinedScore:F3})</color>");
            pathVisualizer.HighlightPath(bestMatch.index);
        }
        else
        {
            Debug.LogWarning("--- 최종 판별 실패 --- 유효한 분석 결과를 얻지 못했습니다.");
        }
        analysisCoroutine = null;
    }
}