using UnityEngine;
using System.Collections.Generic;

public class GameUIManager : MonoBehaviour
{
    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;
    public UIPursuitMover pursuitMover2D;
    public GazeRecorder gazeRecorder; // GazeRecorder 참조 추가

    [Header("설정")]
    public float travelDuration = 2.0f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) OnStartPursuitClicked(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) OnStartPursuitClicked(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) OnStartPursuitClicked(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) OnStartPursuitClicked(3);
        
        if (Input.GetKeyDown(KeyCode.Tab)) OnSwitchViewClicked();
    }
    
    public void OnStartPursuitClicked(int targetIndex)
    {
        Debug.Log(targetIndex + 1 + "번 키 입력 감지됨!");
        
        Transform target = pathVisualizer.GetTargetTransform(targetIndex);
            if (target == null)
            {
                // 찾지 못했을 경우 빨간색 오류 메시지를 띄움
                Debug.LogError("오류: 타겟 " + (targetIndex + 1) + "을(를) 찾을 수 없습니다! PathVisualizer의 Targets 리스트 Size와 할당 상태를 다시 확인하세요."); // <-- 추가 2
                return;
            }
        pathVisualizer.HighlightPath(targetIndex);
        
        // 1. 시선 기록 시작
        gazeRecorder.StartRecording();

        // 2. 퍼슈트가 완료되었을 때 실행될 행동(분석 및 결과 출력)을 정의
        System.Action<List<Vector2>, List<float>> onPursuitComplete = (targetPath, targetTimes) =>
        {
            // 3. 시선 기록 중지 및 사용자 경로 데이터 가져오기
            var (userPath, userTimes) = gazeRecorder.StopRecording();

            // 4. GestureAnalyser로 분석 실행
            float frechetDist = GestureAnalyser.CalculateFrechetDistance(userPath, targetPath);
            float velocitySim = GestureAnalyser.CalculateVelocitySimilarity(userPath, userTimes, targetPath, targetTimes);
            
            // 5. 콘솔에 최종 결과 출력
            Debug.Log($"--- 분석 결과 (타겟 {targetIndex}) ---");
            Debug.Log($"프레셰 거리: {frechetDist}"); // 낮을수록 좋음
            Debug.Log($"속도 유사도: {velocitySim}"); // 1에 가까울수록 좋음
            Debug.Log("------------------------------------");
        };
        
        // 6. 현재 모드에 맞는 Mover를 실행하고, 완료 시 실행될 행동(onPursuitComplete)을 전달
        if (pathVisualizer.Is3DMode)
        {
            List<Vector3> pathPoints = new List<Vector3> { pathVisualizer.startPoint.position, target.position };
            pursuitMover3D.StartMovement(pathPoints, travelDuration, onPursuitComplete);
        }
        else // 2D 모드일 때
        {
            UILineConnector lineToFollow = pathVisualizer.GetUILine(targetIndex);
            if (lineToFollow != null)
            {
                pursuitMover2D.StartMovement(lineToFollow, travelDuration, onPursuitComplete);
            }
        }
    }
    
    public void OnSwitchViewClicked()
    {
        pathVisualizer.SwitchViewMode();
    }
}