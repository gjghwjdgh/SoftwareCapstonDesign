using UnityEngine;
using System.Collections.Generic;

public class GameUIManager : MonoBehaviour
{
    [Header("관리자 스크립트 연결")]
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover3D;
    // UIPursuitMover 참조 삭제

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
        Transform target = pathVisualizer.GetTargetTransform(targetIndex);
        if (target == null) return;

        pathVisualizer.HighlightPath(targetIndex);

        if (pathVisualizer.Is3DMode)
        {
            List<Vector3> pathPoints = new List<Vector3> { pathVisualizer.startPoint.position, target.position };
            pursuitMover3D.StartMovement(pathPoints, travelDuration);
        }
        else // 2D 모드일 때
        {
            // PathVisualizer에게 targetIndex에 해당하는 2D 라인(UILineConnector)을 물어봄
            UILineConnector lineToFollow = pathVisualizer.GetUILine(targetIndex);
            if (lineToFollow != null)
            {
                // UILineConnector에게 직접 퍼슈트 시작을 명령
                lineToFollow.StartPursuit(travelDuration);
            }
        }
    }
    
    public void OnSwitchViewClicked()
    {
        pathVisualizer.SwitchViewMode();
    }
}