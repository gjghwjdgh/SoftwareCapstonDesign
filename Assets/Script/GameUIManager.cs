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
    public GazeRecorder gazeRecorder;

    [Header("설정")]
    public float travelDuration = 2.0f;

    private Coroutine analysisCoroutine;

    void Update()
    {
        // '1' 키를 누르면 모든 타겟에 대한 동시 추적 및 분석 시작
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (analysisCoroutine != null)
            {
                StopCoroutine(analysisCoroutine);
            }
            analysisCoroutine = StartCoroutine(RunAllPursuitsAndAnalyze());
        }

        // 'Tab' 키로 3D/2D 뷰 전환
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnSwitchViewClicked();
        }
    }

    /// <summary>
    /// 모든 타겟의 움직임을 동시에 시작하고, 끝난 후 사용자의 시선 경로와 비교 분석합니다.
    /// </summary>
    private IEnumerator RunAllPursuitsAndAnalyze()
    {
        int targetCount = pathVisualizer.targets.Count;
        if (targetCount == 0)
        {
            Debug.LogWarning("분석할 타겟이 설정되지 않았습니다.");
            yield break;
        }

        // 각 타겟의 실제 이동 경로를 저장할 리스트
        var targetPaths = new List<(List<Vector2> path, List<float> times)>(new (List<Vector2>, List<float>)[targetCount]);
        int finishedCount = 0;

        // 시선 기록 시작
        gazeRecorder.StartRecording();
        pathVisualizer.HighlightPath(-1); // 모든 하이라이트 초기화

        // 모든 타겟에 대해 움직임 시작
        for (int i = 0; i < targetCount; i++)
        {
            int targetIndex = i; // 클로저를 위한 인덱스 복사

            // 추적이 완료되면 호출될 콜백 함수
            System.Action<List<Vector2>, List<float>> onPursuitComplete = (targetPath, targetTimes) =>
            {
                if (targetPath != null)
                {
                    targetPaths[targetIndex] = (targetPath, targetTimes);
                }
                finishedCount++;
            };

            // 현재 뷰 모드에 맞춰 추적 시작
            if (pathVisualizer.Is3DMode)
            {
                Transform target = pathVisualizer.GetTargetTransform(targetIndex);
                if (target != null)
                {
                    List<Vector3> pathPoints = new List<Vector3> { pathVisualizer.startPoint.position, target.position };
                    pursuitMover3D.StartMovement(pathPoints, travelDuration, onPursuitComplete);
                }
            }
            else
            {
                UILineConnector lineToFollow = pathVisualizer.GetUILine(targetIndex);
                if (lineToFollow != null)
                {
                    pursuitMover2D.StartMovement(lineToFollow, travelDuration, onPursuitComplete);
                }
            }
        }

        // 모든 타겟의 움직임이 끝날 때까지 대기
        yield return new WaitUntil(() => finishedCount == targetCount);

        // 시선 기록 중지 및 데이터 가져오기
        var (userPath, userTimes) = gazeRecorder.StopRecording();

        // --- 분석 결과 출력 ---
        Debug.Log($"--- 동시 추적 분석 결과 (뷰 모드: {(pathVisualizer.Is3DMode ? "3D" : "2D")}) ---");

        var results = new List<(int index, float frechet, float velocity)>();
        for (int i = 0; i < targetCount; i++)
        {
            var (targetPath, targetTimes) = targetPaths[i];
            if (targetPath == null || targetPath.Count == 0) continue;

            float frechetDist = GestureAnalyser.CalculateFrechetDistance(userPath, targetPath);
            float velocitySim = GestureAnalyser.CalculateVelocitySimilarity(userPath, userTimes, targetPath, targetTimes);
            results.Add((i, frechetDist, velocitySim));

            Debug.Log($"[타겟 {i + 1}] 프레셰 거리(유사도): {frechetDist:F2} / 속도 유사도(리듬): {velocitySim:F2}");
        }

        // 가장 잘 따라간 타겟 찾기 (프레셰 거리가 가장 작은 값)
        if (results.Any())
        {
            var bestMatch = results.OrderBy(r => r.frechet).First();
            Debug.Log($"<결론> 사용자는 '타겟 {bestMatch.index + 1}'의 경로를 가장 유사하게 따라갔습니다.");
            pathVisualizer.HighlightPath(bestMatch.index); // 가장 유사한 경로 하이라이트
        }

        Debug.Log("--------------------------------------------------");
        analysisCoroutine = null;
    }

    public void OnSwitchViewClicked()
    {
        pathVisualizer.SwitchViewMode();
        Debug.Log($"뷰 모드가 {(pathVisualizer.Is3DMode ? "3D" : "2D")}(으)로 전환되었습니다.");
    }
}