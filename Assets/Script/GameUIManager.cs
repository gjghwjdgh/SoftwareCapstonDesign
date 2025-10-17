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

    [Header("곡선 경로 설정 (1번 타겟용)")]
    public Transform target1_ControlPoint1;
    public Transform target1_ControlPoint2;
    [Tooltip("곡선을 얼마나 부드럽게 만들지 결정합니다.")]
    public int curveSegmentCount = 30;

    [Header("입력 판별 설정")]
    [Range(0f, 1f)] public float shapeWeight = 0.8f;
    [Range(0f, 1f)] public float velocityWeight = 0.2f;
    [Range(0f, 1f)] public float perceptionCoefficient = 0.8f;

    private Coroutine analysisCoroutine;

    private bool isTarget1CurveMode = false;
    public bool IsTarget1CurveMode => isTarget1CurveMode;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) pathVisualizer.SwitchViewMode();
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (analysisCoroutine != null) StopCoroutine(analysisCoroutine);
            analysisCoroutine = StartCoroutine(RunAllPursuitsAndDrawAndAnalyze());
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            isTarget1CurveMode = !isTarget1CurveMode;
            pathVisualizer.GenerateAndShowAllPaths();
            Debug.Log("1번 타겟 경로 모드 변경: " + (isTarget1CurveMode ? "베지어 곡선" : "직선"));
        }
    }

    private IEnumerator RunAllPursuitsAndDrawAndAnalyze()
    {
        pathVisualizer.HighlightPath(-1);

        // ✨ --- 핵심 변경: 경로 개수는 이제 targets 리스트의 개수입니다 ---
        int pathCount = pathVisualizer.targets.Count;
        var targetData = new List<(List<Vector2> path, List<float> times)>(new (List<Vector2>, List<float>)[pathCount]);
        int finishedCount = 0;
        List<Vector2> userDrawnPath = new List<Vector2>();
        List<float> userDrawnTimes = new List<float>();

        System.Action<int, List<Vector2>, List<float>> onPursuitComplete = (index, path, times) =>
        {
            if (path != null) targetData[index] = (path, times);
            finishedCount++;
        };

        for (int i = 0; i < pathCount; i++)
        {
            int pathIndex = i;

            // ✨ --- 핵심 변경: 단일 시작점과 각 인덱스의 타겟을 사용합니다 ---
            Transform startPoint = pathVisualizer.startPoint;
            Transform target = pathVisualizer.targets[pathIndex];
            if (startPoint == null || target == null) continue;

            List<Vector3> fullPath;
            bool isCurve = (i == 0 && isTarget1CurveMode && target1_ControlPoint1 != null && target1_ControlPoint2 != null);
            if (isCurve)
            {
                fullPath = PathUtilities.GenerateBezierCurvePath(
                    startPoint.position, target1_ControlPoint1.position,
                    target1_ControlPoint2.position, target.position, curveSegmentCount);
            }
            else
            {
                fullPath = new List<Vector3>();
                for (int j = 0; j <= 100; j++)
                {
                    fullPath.Add(Vector3.Lerp(startPoint.position, target.position, j / 100f));
                }
            }

            // ✨ --- 핵심 변경: 1/(N+2) 규칙에 따라 시작 비율을 자동으로 계산 ---
            float startFraction = (float)(pathIndex + 1) / (pathCount + 2);

            int startIndex = Mathf.RoundToInt((fullPath.Count - 1) * startFraction);
            List<Vector3> partialPath = fullPath.GetRange(startIndex, fullPath.Count - startIndex);

            if (pathVisualizer.Is3DMode)
            {
                pursuitMover3D.StartMovement(partialPath, travelDuration, (p, t) => onPursuitComplete(pathIndex, p, t));
            }
            else
            {
                pursuitMover2D.StartMovement(partialPath, travelDuration, (p, t) => onPursuitComplete(pathIndex, p, t));
            }
        }

        // (이하 분석 로직은 모두 동일)
        float drawingTimer = 0f;
        while (drawingTimer < travelDuration)
        {
            userDrawnPath.Add(Input.mousePosition);
            userDrawnTimes.Add(Time.time);
            drawingTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => finishedCount == pathCount);
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
        for (int i = 0; i < pathCount; i++)
        {
            var (path, _) = targetData[i];
            if (path == null || path.Count == 0) { frechetValues.Add(float.MaxValue); continue; }
            frechetValues.Add(GestureAnalyser.CalculateFrechetDistance(userDrawnPath, path));
        }
        float maxFrechet = frechetValues.Where(f => f < float.MaxValue).DefaultIfEmpty(0).Max();
        if (maxFrechet <= float.Epsilon) maxFrechet = 1.0f;
        for (int i = 0; i < pathCount; i++)
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
            Debug.Log($"<color=cyan><입력 성공> '경로 {bestMatch.index + 1}'이(가) 최종 입력으로 판별되었습니다. (최저 점수: {bestMatch.combinedScore:F3})</color>");
            pathVisualizer.HighlightPath(bestMatch.index);
        }
        else
        {
            Debug.LogWarning("--- 최종 판별 실패 --- 유효한 분석 결과를 얻지 못했습니다.");
        }
        analysisCoroutine = null;
    }
}